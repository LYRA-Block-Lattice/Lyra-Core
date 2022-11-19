using Lyra.Core.API;
using Lyra.Core.Authorizers;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.WorkFlow
{
    [LyraWorkFlow]
    public class WFPoolCreate : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_POOL_CRPL,
                RecvVia = BrokerRecvType.GuildRecv,
            };
        }

        protected static async Task<bool> CheckTokenAsync(DagSystem sys, string tokenName)
        {
            var tokn = await sys.Storage.FindTokenGenesisBlockAsync(null, tokenName);
            return tokn != null;
        }

        protected static async Task<APIResultCodes> CheckPoolTagsAsync(DagSystem sys, Block block, int tagsCount = 3)
        {
            if (block.Tags.ContainsKey("token0") && await CheckTokenAsync(sys, block.Tags["token0"])
                && block.Tags.ContainsKey("token1") && await CheckTokenAsync(sys, block.Tags["token1"])
                && block.Tags["token0"] != block.Tags["token1"]
                && (block.Tags["token0"] == LyraGlobal.OFFICIALTICKERCODE || block.Tags["token1"] == LyraGlobal.OFFICIALTICKERCODE)
                && block.Tags.Count == tagsCount
                )
                return APIResultCodes.Success;
            else
                return APIResultCodes.InvalidBlockTags;
        }

        #region BRK_POOL_CRPL
        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            // generic pool factory
            var tgc = await CheckPoolTagsAsync(sys, send);
            if (tgc != APIResultCodes.Success)
                return tgc;

            TransactionBlock lastBlock = await DagSystem.Singleton.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock;

            var chgs = send.GetBalanceChanges(lastBlock);
            if (!chgs.Changes.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
                return APIResultCodes.InvalidFeeAmount;

            if (chgs.Changes.Count > 1)
                return APIResultCodes.InvalidFeeAmount;

            // check if pool exists
            var factory = await sys.Storage.GetPoolFactoryAsync();
            if (factory == null)
                return APIResultCodes.SystemNotReadyToServe;

            // action

            if (chgs.Changes[LyraGlobal.OFFICIALTICKERCODE] != PoolFactoryBlock.PoolCreateFee)
                return APIResultCodes.InvalidFeeAmount;

            var poolGenesis = await sys.Storage.GetPoolAsync(send.Tags["token0"], send.Tags["token1"]);
            if (poolGenesis != null)
                return APIResultCodes.PoolAlreadyExists;

            return APIResultCodes.Success;
        }

        public override async Task<TransactionBlock> BrokerOpsAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            var pool = await sys.Storage.GetPoolAsync(send.Tags["token0"], send.Tags["token1"]);
            if (pool != null)
                return null;

            // get token gensis to make the token name proper
            var token0Gen = await sys.Storage.FindTokenGenesisBlockAsync(null, send.Tags["token0"]);
            var token1Gen = await sys.Storage.FindTokenGenesisBlockAsync(null, send.Tags["token1"]);

            //if (token0Gen == null || token1Gen == null)
            //{
            //    return;
            //}

            var arrStr = new[] { token0Gen.Ticker, token1Gen.Ticker };
            Array.Sort(arrStr);

            var poole = await sys.Storage.GetPoolAsync(arrStr[0], arrStr[1]);
            if (poole != null)
                return null;

            var sb = await sys.Storage.GetLastServiceBlockAsync();

            // create a semi random account for pool.
            // it can be verified by other nodes.
            var keyStr = $"{send.Hash.Substring(0, 16)},{arrStr[0]},{arrStr[1]},{send.AccountID}";
            var AccountId = Base58Encoding.EncodeAccountId(Encoding.ASCII.GetBytes(keyStr).Take(64).ToArray());

            var poolGenesis = new PoolGenesisBlock
            {
                Height = 1,
                AccountType = AccountTypes.Pool,
                AccountID = AccountId,        // in fact we not use this account.
                Balances = new Dictionary<string, long>(),
                PreviousHash = sb.Hash,
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,

                // pool specified config
                Token0 = arrStr[0],
                Token1 = arrStr[1],
                RelatedTx = send.Hash
            };

            poolGenesis.AddTag(Block.MANAGEDTAG, WFState.Finished.ToString());

            // pool blocks are service block so all service block signed by leader node
            poolGenesis.InitializeBlock(null, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            return poolGenesis;
            //await QueueTxActionBlockAsync(poolGenesis);
        }
        #endregion
    }

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
            return await BrokerOpsAsync(sys, context) as ReceiveTransferBlock;
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

            depositBlock.AddTag(Block.MANAGEDTAG, WFState.Finished.ToString());

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

            withdrawBlock.AddTag(Block.MANAGEDTAG, WFState.Finished.ToString());           

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
            return await BrokerOpsAsync(sys, context) as ReceiveTransferBlock;
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

            swapInBlock.AddTag(Block.MANAGEDTAG, WFState.Running.ToString());

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

        public override async Task<TransactionBlock> ExtraOpsAsync(DagSystem sys, string reqHash)
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

            swapOutBlock.AddTag(Block.MANAGEDTAG, WFState.Finished.ToString());

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
