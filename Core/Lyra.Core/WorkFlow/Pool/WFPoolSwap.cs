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
    public class WFPoolSwap : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_POOL_SWAP,
                RecvVia = BrokerRecvType.None,
            };
        }


        #region BRK_POOL_SWAP
        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, LyraContext context)
        {
            var block = context.Send;
            TransactionBlock lastBlock = await DagSystem.Singleton.Storage.FindBlockByHashAsync(block.PreviousHash) as TransactionBlock;

            if (block.Tags.Count > 3 || !block.Tags.ContainsKey("poolid"))
                return APIResultCodes.InvalidBlockTags;

            if (block.Tags.Count == 3 && !block.Tags.ContainsKey("minrecv"))
                return APIResultCodes.InvalidBlockTags;

            var vp = await WFPoolAddLiquidate.VerifyPoolAsync(sys, block, lastBlock);
            if (vp != APIResultCodes.Success)
                return vp;

            var chgs = block.GetBalanceChanges(lastBlock);
            var poolGenesis = sys.Storage.GetPoolByID(block.Tags["poolid"]);

            if (chgs.Changes.Count != 1)
                return APIResultCodes.InvalidTokenToSwap;

            string tokenToSwap = null;
            var kvp = chgs.Changes.First();
            if (kvp.Key == poolGenesis.Token0)
                tokenToSwap = poolGenesis.Token0;
            else if (kvp.Key == poolGenesis.Token1)
                tokenToSwap = poolGenesis.Token1;

            if (tokenToSwap == null)
                return APIResultCodes.InvalidTokenToSwap;

            // check amount
            var poolLatest = await sys.Storage.FindLatestBlockAsync(block.DestinationAccountId) as TransactionBlock;
            //if (kvp.Value > poolLatest.Balances[tokenToSwap].ToBalanceDecimal() / 2)
            //    return APIResultCodes.TooManyTokensToSwap;
            // uniswap AMM don't mind how many token want to swap

            if (block.Tags.ContainsKey("minrecv"))
            {
                if (!long.TryParse(block.Tags["minrecv"], out long toGetLong))
                    return APIResultCodes.InvalidSwapSlippage;

                decimal toGet = toGetLong.ToBalanceDecimal();

                if (toGet <= 0)
                    return APIResultCodes.InvalidSwapSlippage;

                if (poolLatest.Balances.Any(a => a.Value == 0))
                {
                    // can't calculate rito
                    return APIResultCodes.PoolOutOfLiquidaty;
                }

                var cal = new SwapCalculator(poolGenesis.Token0, poolGenesis.Token1, poolLatest,
                    chgs.Changes.First().Key, chgs.Changes.First().Value, 0);

                if (cal.SwapOutAmount < toGet)
                {
                    return APIResultCodes.SwapSlippageExcceeded;
                }
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
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(sendBlock.Hash);
            if (blocks.Any(a => a is PoolSwapInBlock))
                return null;

            var lsb = await sys.Storage.GetLastServiceBlockAsync();
            var swapInBlock = new PoolSwapInBlock
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

            swapInBlock.AddTag(Block.MANAGEDTAG, context.State.ToString());

            TransactionBlock prevSend = await sys.Storage.FindBlockByHashAsync(sendBlock.PreviousHash) as TransactionBlock;
            var txInfo = sendBlock.GetBalanceChanges(prevSend);

            TransactionBlock latestPoolBlock = await sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;
            PoolGenesisBlock poolGenesis = await sys.Storage.FindFirstBlockAsync(latestPoolBlock.AccountID) as PoolGenesisBlock;

            var depositBalance = new Dictionary<string, decimal>();
            if (latestPoolBlock.Balances.Any())
            {
                var lastBalance = latestPoolBlock.Balances.ToDecimalDict();

                // the rito must be preserved for every deposition
                //var poolRito = lastBalance[poolGenesis.Token0] / lastBalance[poolGenesis.Token1];
                foreach (var oldBalance in lastBalance)
                {
                    if (txInfo.Changes.ContainsKey(oldBalance.Key))
                        depositBalance.Add(oldBalance.Key, oldBalance.Value + txInfo.Changes[oldBalance.Key]);
                    else
                        depositBalance.Add(oldBalance.Key, oldBalance.Value);
                }

                var prevBalance = lastBalance[poolGenesis.Token0];
                var curBalance = depositBalance[poolGenesis.Token0];
            }
            else
            {
                foreach (var token in txInfo.Changes)
                {
                    depositBalance.Add(token.Key, token.Value);
                }
            }

            swapInBlock.Balances = depositBalance.ToLongDict();
            swapInBlock.Shares = (latestPoolBlock as IPool).Shares;
            await swapInBlock.InitializeBlockAsync(latestPoolBlock, (hash) => Task.FromResult(Signatures.GetSignature(sys.PosWallet.PrivateKey, hash, sys.PosWallet.AccountId)));

            return swapInBlock;
        }

        public override async Task<TransactionBlock> ExtraOpsAsync(DagSystem sys, LyraContext context, string reqHash)
        {
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(reqHash);
            var swout = blocks.FirstOrDefault(a => a is PoolSwapOutBlock);
            if (swout != null)
                return null;

            var recv = blocks.FirstOrDefault(a => a is PoolSwapInBlock) as PoolSwapInBlock;

            var swapInBlock = recv as PoolSwapInBlock;
            var recvBlockPrev = await sys.Storage.FindBlockByHashAsync(recv.PreviousHash) as TransactionBlock;
            var recvChgs = swapInBlock.GetBalanceChanges(recvBlockPrev);
            var kvp = recvChgs.Changes.First();
            var poolGenesis = await sys.Storage.FindFirstBlockAsync(swapInBlock.AccountID) as PoolGenesisBlock;
            var cfg = new SwapCalculator(poolGenesis.Token0, poolGenesis.Token1, recvBlockPrev,
                    kvp.Key, kvp.Value, 0);

            var lsb = await sys.Storage.GetLastServiceBlockAsync();
            var send = await sys.Storage.FindBlockByHashAsync(swapInBlock.SourceHash) as SendTransferBlock;
            var swapOutBlock = new PoolSwapOutBlock
            {
                AccountID = send.DestinationAccountId,
                ServiceHash = lsb.Hash,
                DestinationAccountId = send.AccountID,
                Balances = new Dictionary<string, long>(),
                Tags = null,
                Fee = cfg.PayToAuthorizer,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.Dynamic,
                RelatedTx = send.Hash
            };

            swapOutBlock.AddTag(Block.MANAGEDTAG, context.State.ToString());

            var poolGenesisBlock = await sys.Storage.FindFirstBlockAsync(recv.AccountID) as PoolGenesisBlock;
            var poolLatestBlock = await sys.Storage.FindLatestBlockAsync(recv.AccountID) as TransactionBlock;

            var curBalance = poolLatestBlock.Balances.ToDecimalDict();
            var nextBalance = poolLatestBlock.Balances.ToDecimalDict();

            var tokenOut = cfg.SwapOutToken;
            var tokenIn = cfg.SwapInToken;
            if (tokenIn == LyraGlobal.OFFICIALTICKERCODE)
            {
                // tokenIn == LYR
                nextBalance[tokenIn] = curBalance[tokenIn] - cfg.PayToAuthorizer;  // pool fee leave in the pool
                nextBalance[tokenOut] = curBalance[tokenOut] - cfg.SwapOutAmount;
            }
            else
            {
                // tokenIn == other token
                nextBalance[tokenIn] = curBalance[tokenIn];  // pool fee leave in the pool
                nextBalance[tokenOut] = curBalance[tokenOut] - cfg.SwapOutAmount - cfg.PayToAuthorizer;
            }

            Console.WriteLine($"user should receive {cfg.SwapOutAmount} {cfg.SwapOutToken}");

            swapOutBlock.Balances = nextBalance.ToLongDict();
            swapOutBlock.Shares = (poolLatestBlock as IPool).Shares;
            await swapOutBlock.InitializeBlockAsync(poolLatestBlock, (hash) => Task.FromResult(Signatures.GetSignature(sys.PosWallet.PrivateKey, hash, sys.PosWallet.AccountId)));

            // verify 
            var chgs = swapOutBlock.GetBalanceChanges(poolLatestBlock);

            return swapOutBlock;
        }
        #endregion
    }
}
