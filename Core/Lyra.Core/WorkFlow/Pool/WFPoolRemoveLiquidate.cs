using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.API;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.WorkFlow.Pool
{
    [LyraWorkFlow]
    public class WFPoolRemoveLiquidate : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_POOL_RMLQ,
                RecvVia = BrokerRecvType.GuildRecv,
            };
        }

        #region BRK_POOL_RMLQ
        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, LyraContext context)
        {
            var block = context.Send;
            if (block.Tags.Count != 2 || !block.Tags.ContainsKey("poolid"))
                return APIResultCodes.InvalidBlockTags;

            var poolGenesis = sys.Storage.GetPoolByID(block.Tags["poolid"]);
            if (poolGenesis == null)
                return APIResultCodes.PoolNotExists;

            TransactionBlock lastBlock = await DagSystem.Singleton.Storage.FindBlockByHashAsync(block.PreviousHash) as TransactionBlock;

            var chgs = block.GetBalanceChanges(lastBlock);

            if (chgs.Changes[LyraGlobal.OFFICIALTICKERCODE] != 1m)
                return APIResultCodes.InvalidFeeAmount;

            if (!(await sys.Storage.FindLatestBlockAsync(poolGenesis.AccountID) is IPool pool))
                return APIResultCodes.PoolNotExists;

            if (!pool.Shares.ContainsKey(block.AccountID))
                return APIResultCodes.PoolShareNotExists;

            return APIResultCodes.Success;
        }
        public override async Task<TransactionBlock> BrokerOpsAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);
            if (blocks.Any(a => a is PoolWithdrawBlock))
                return null;

            var lsb = await sys.Storage.GetLastServiceBlockAsync();
            var recvBlock = await sys.Storage.FindBlockBySourceHashAsync(send.Hash);

            var poolGenesis = sys.Storage.GetPoolByID(send.Tags["poolid"]);
            var poolId = poolGenesis.AccountID;

            PoolWithdrawBlock withdrawBlock = new PoolWithdrawBlock
            {
                AccountID = poolId,
                ServiceHash = lsb.Hash,
                DestinationAccountId = send.AccountID,
                Balances = new Dictionary<string, long>(),
                Tags = null,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,
                RelatedTx = send.Hash
            };

            var sendBlock = await sys.Storage.FindBlockByHashAsync(recvBlock.SourceHash) as SendTransferBlock;

            withdrawBlock.AddTag(Block.MANAGEDTAG, context.State.ToString());

            var poolGenesisBlock = await sys.Storage.FindFirstBlockAsync(poolId) as PoolGenesisBlock;
            var poolLatestBlock = await sys.Storage.FindLatestBlockAsync(poolId) as TransactionBlock;

            var curBalance = poolLatestBlock.Balances.ToDecimalDict();
            var curShares = (poolLatestBlock as IPool).Shares.ToRitoDecimalDict();

            var nextBalance = poolLatestBlock.Balances.ToDecimalDict();
            var nextShares = (poolLatestBlock as IPool).Shares.ToRitoDecimalDict();

            var usersShare = curShares[send.AccountID];
            var amountsToSend = new Dictionary<string, decimal>
            {
                { poolGenesisBlock.Token0, curBalance[poolGenesisBlock.Token0] * usersShare },
                { poolGenesisBlock.Token1, curBalance[poolGenesisBlock.Token1] * usersShare }
            };

            nextBalance[poolGenesisBlock.Token0] -= amountsToSend[poolGenesisBlock.Token0];
            nextBalance[poolGenesisBlock.Token1] -= amountsToSend[poolGenesisBlock.Token1];
            nextShares.Remove(send.AccountID);

            foreach (var share in curShares)
            {
                if (share.Key == send.AccountID)
                    continue;

                nextShares[share.Key] = (share.Value * curBalance[poolGenesisBlock.Token0]) / nextBalance[poolGenesisBlock.Token0];
            }

            withdrawBlock.Balances = nextBalance.ToLongDict();
            withdrawBlock.Shares = nextShares.ToRitoLongDict();

            await withdrawBlock.InitializeBlockAsync(poolLatestBlock, (hash) => Task.FromResult(Signatures.GetSignature(sys.PosWallet.PrivateKey, hash, sys.PosWallet.AccountId)));
            return withdrawBlock;
        }
        #endregion
    }
}
