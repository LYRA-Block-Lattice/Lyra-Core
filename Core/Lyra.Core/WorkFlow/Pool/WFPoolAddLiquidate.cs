using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.API;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.WorkFlow.Pool
{
    [LyraWorkFlow]
    public class WFPoolAddLiquidate : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_POOL_ADDLQ,
                RecvVia = BrokerRecvType.None,
            };
        }

        public static async Task<APIResultCodes> VerifyPoolAsync(DagSystem sys, SendTransferBlock block, TransactionBlock lastBlock)
        {
            var poolGenesis = sys.Storage.GetPoolByID(block.Tags["poolid"]);
            if (poolGenesis == null)
                return APIResultCodes.PoolNotExists;

            var poolGenesis2 = await sys.Storage.FindFirstBlockAsync(block.DestinationAccountId);
            if (poolGenesis2 == null)
                return APIResultCodes.PoolNotExists;

            if (poolGenesis.Hash != poolGenesis2.Hash)
                return APIResultCodes.PoolNotExists;

            return APIResultCodes.Success;
        }

        #region BRK_POOL_ADDLQ
        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, LyraContext context)
        {
            var block = context.Send;
            if (block.Tags.Count != 2 || !block.Tags.ContainsKey("poolid"))
                return APIResultCodes.InvalidBlockTags;

            TransactionBlock lastBlock = await DagSystem.Singleton.Storage.FindBlockByHashAsync(block.PreviousHash) as TransactionBlock;

            var vp = await VerifyPoolAsync(sys, block, lastBlock);
            if (vp != APIResultCodes.Success)
                return vp;

            var chgs = block.GetBalanceChanges(lastBlock);
            if (chgs.Changes.Count != 2)
                return APIResultCodes.InvalidPoolDepositionAmount;

            var poolGenesis = sys.Storage.GetPoolByID(block.Tags["poolid"]);
            if (poolGenesis == null)
                return APIResultCodes.PoolNotExists;

            if (!chgs.Changes.ContainsKey(poolGenesis.Token0) || !chgs.Changes.ContainsKey(poolGenesis.Token1))
                return APIResultCodes.InvalidPoolDepositionAmount;

            var poolLatest = await sys.Storage.FindLatestBlockAsync(block.DestinationAccountId) as TransactionBlock;
            // compare rito
            if (poolLatest.Balances.ContainsKey(poolGenesis.Token0) && poolLatest.Balances.ContainsKey(poolGenesis.Token1)
                && poolLatest.Balances[poolGenesis.Token0] > 0 && poolLatest.Balances[poolGenesis.Token1] > 0
                )
            {
                var rito = (poolLatest.Balances[poolGenesis.Token0].ToBalanceDecimal() / poolLatest.Balances[poolGenesis.Token1].ToBalanceDecimal());
                var token0Amount = chgs.Changes[poolGenesis.Token0];
                var token1AmountShouldBe = Math.Round(token0Amount / rito, 8);
                if (chgs.Changes[poolGenesis.Token1] != token1AmountShouldBe
                    && Math.Abs(chgs.Changes[poolGenesis.Token1] - token1AmountShouldBe) / token1AmountShouldBe > 0.0000001m
                    )
                    return APIResultCodes.InvalidPoolDepositionRito;
            }
            return APIResultCodes.Success;
        }

        public override async Task<ReceiveTransferBlock?> NormalReceiveAsync(DagSystem sys, LyraContext context)
        {
            return await BrokerOpsAsync(sys, context) as ReceiveTransferBlock;
        }

        public override async Task<ReceiveTransferBlock?> RefundReceiveAsync(DagSystem sys, LyraContext context)
        {
            // refund receive doesn't change the pool settings. just receive.
            return await TransReceiveAsync<PoolRefundReceiveBlock>(sys, context);
        }

        public override async Task<SendTransferBlock?> RefundSendAsync(DagSystem sys, LyraContext context)
        {
            var srcAccount = context.Send.DestinationAccountId;
            var last1 = await sys.Storage.FindLatestBlockAsync(srcAccount) as TransactionBlock;
            var last2 = await sys.Storage.FindBlockByHashAsync(last1.PreviousHash) as TransactionBlock;
            var chgs = last1.GetBalanceChanges(last2);

            return await TransSendAsync<PoolRefundSendBlock>(sys,
                    context.Send.Hash, srcAccount, context.Send.AccountID,
                    chgs.Changes,
                    context.State);
        }

        public override async Task<TransactionBlock> BrokerOpsAsync(DagSystem sys, LyraContext context)
        {
            var sendBlock = context.Send;
            // assume all send variables are legal
            // token0/1, amount, etc.
            var existsAdd = await sys.Storage.FindBlockBySourceHashAsync(sendBlock.Hash);
            if (existsAdd != null)
                return null;

            var lsb = await sys.Storage.GetLastServiceBlockAsync();
            var depositBlock = new PoolDepositBlock
            {
                AccountID = sendBlock.DestinationAccountId,
                VoteFor = null,
                ServiceHash = lsb.Hash,
                SourceHash = sendBlock.Hash,
                Balances = new Dictionary<string, long>(),
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,

                RelatedTx = sendBlock.Hash
            };

            depositBlock.AddTag(Block.MANAGEDTAG, context.State.ToString());

            TransactionBlock prevSend = await sys.Storage.FindBlockByHashAsync(sendBlock.PreviousHash) as TransactionBlock;
            var txInfo = sendBlock.GetBalanceChanges(prevSend);

            TransactionBlock latestPoolBlock = await sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;
            PoolGenesisBlock poolGenesis = await sys.Storage.FindFirstBlockAsync(latestPoolBlock.AccountID) as PoolGenesisBlock;

            var depositBalance = new Dictionary<string, decimal>();
            var depositShares = new Dictionary<string, decimal>();
            if (latestPoolBlock.Balances.Any())
            {
                var lastBalance = latestPoolBlock.Balances.ToDecimalDict();
                var lastShares = ((IPool)latestPoolBlock).Shares.ToRitoDecimalDict();

                // the rito must be preserved for every deposition
                //var poolRito = lastBalance[poolGenesis.Token0] / lastBalance[poolGenesis.Token1];
                foreach (var oldBalance in lastBalance)
                {
                    depositBalance.Add(oldBalance.Key, oldBalance.Value + txInfo.Changes[oldBalance.Key]);
                }

                var prevBalance = lastBalance[poolGenesis.Token0];
                var curBalance = depositBalance[poolGenesis.Token0];

                foreach (var share in lastShares)
                {
                    depositShares.Add(share.Key, (share.Value * prevBalance / curBalance));
                }

                // merge share if any
                var r0 = txInfo.Changes[poolGenesis.Token0] / curBalance;

                if (depositShares.ContainsKey(sendBlock.AccountID))
                    depositShares[sendBlock.AccountID] += r0;
                else
                    depositShares.Add(sendBlock.AccountID, r0);
            }
            else
            {
                foreach (var token in txInfo.Changes)
                {
                    depositBalance.Add(token.Key, token.Value);
                }

                depositShares.Add(sendBlock.AccountID, 1m);   // 100%
            }

            depositBlock.Balances = depositBalance.ToLongDict();
            depositBlock.Shares = depositShares.ToRitoLongDict();

            await depositBlock.InitializeBlockAsync(latestPoolBlock, (hash) => Task.FromResult(Signatures.GetSignature(sys.PosWallet.PrivateKey, hash, sys.PosWallet.AccountId)));
            return depositBlock;
        }
        #endregion
    }
}
