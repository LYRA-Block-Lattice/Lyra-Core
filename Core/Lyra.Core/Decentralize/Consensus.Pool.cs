using Lyra.Core.API;
using Lyra.Core.Authorizers;
using Lyra.Core.Blocks;
using Lyra.Data.API;
using Lyra.Data.Crypto;
using Lyra.Data.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize
{
    public partial class ConsensusService
    {
        private readonly ServiceTxQueue _svcQueue = new ServiceTxQueue();
        private async Task QueueBlockForPoolAsync(TransactionBlock block, ServiceTx tx)
        {
            if (block == null || tx == null)
                throw new ArgumentNullException();

            if(IsThisNodeLeader)
            {
                await SendBlockToConsensusAndWaitResultAsync(block);
            }
        }

        private async Task QueueTxActionBlockAsync(TransactionBlock actionBlock)
        {
            if (actionBlock == null)
                throw new ArgumentNullException();

            if (IsThisNodeLeader)
            {
                await SendBlockToConsensusAndWaitResultAsync(actionBlock);
            }
        }

        private async Task PoolFactoryRecvActionAsync(Block block, ConsensusResult? result)
        {
            if (result == ConsensusResult.Yea)
            {
                var recvBlock = block as ReceiveTransferBlock;
                var send = await _sys.Storage.FindBlockByHashAsync(recvBlock.SourceHash) as SendTransferBlock;
                if (send.Tags[Block.REQSERVICETAG] == "")
                {
                    // then create pool for it.
                    _log.LogInformation("Creating pool ...");
                    await CreateLiquidatePoolAsync(send, recvBlock, send.Tags["token0"], send.Tags["token1"]);
                    //if (poolCreateResult == ConsensusResult.Yea)
                    //    _log.LogInformation($"Pool created successfully.");
                    //else
                    //    _log.LogWarning("Can't create pool.");
                }
                else
                {
                    _log.LogError("should not happen.");
                }
            }
            else
            {
                _log.LogWarning("Pool factory not receive funds properly.");
            }
        }

        private async Task PoolRecvSwapInConsensusActionAsync(Block block, ConsensusResult? result)
        {
            if (result == ConsensusResult.Yea)
            {
                var swapInBlock = block as PoolSwapInBlock;
                var recvBlockPrev = await _sys.Storage.FindBlockByHashAsync(block.PreviousHash) as TransactionBlock;
                var recvChgs = swapInBlock.GetBalanceChanges(recvBlockPrev);
                var kvp = recvChgs.Changes.First();
                var poolGenesis = await _sys.Storage.FindFirstBlockAsync(swapInBlock.AccountID) as PoolGenesisBlock;

                var send = await _sys.Storage.FindBlockByHashAsync(swapInBlock.SourceHash) as TransactionBlock;
                //_log.LogInformation($"Got swap in token amount: {kvp.Value} Result: {swapInResult}");

                var cfg = new SwapCalculator(poolGenesis.Token0, poolGenesis.Token1, recvBlockPrev,
                    kvp.Key, kvp.Value, 0);

                _log.LogInformation($"Sending swap out {cfg.SwapOutAmount} {cfg.SwapOutToken}");
                await SendPoolSwapOutTokenAsync(swapInBlock, send.AccountID, cfg);
            }
        }

        /// <summary>
        /// receive fee and create a new liquidate pool
        /// </summary>
        /// <param name="sendBlock"></param>
        /// <returns></returns>
        private async Task ReceivePoolFactoryFeeAsync(SendTransferBlock sendBlock, string actionType)
        {
            var lsb = await _sys.Storage.GetLastServiceBlockAsync();
            var receiveBlock = new ReceiveTransferBlock
            {
                AccountID = sendBlock.DestinationAccountId,
                VoteFor = null,
                ServiceHash = lsb.Hash,
                SourceHash = sendBlock.Hash,
                Balances = new Dictionary<string, long>(),
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee
            };

            receiveBlock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored
            receiveBlock.AddTag("relhash", sendBlock.Hash);  // pool factory recv 
            receiveBlock.AddTag("type", actionType);       // pool factory receive

            TransactionBlock prevSend = await _sys.Storage.FindBlockByHashAsync(sendBlock.PreviousHash) as TransactionBlock;
            var txInfo = sendBlock.GetBalanceChanges(prevSend);

            TransactionBlock latestPoolBlock = await _sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;

            // ignore any token but LYR. keep the block clean.
            if (!txInfo.Changes.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
                return;

            var latestBalances = latestPoolBlock.Balances.ToDecimalDict();
            var recvBalances = latestPoolBlock.Balances.ToDecimalDict();
            foreach (var chg in txInfo.Changes)
            {
                if (chg.Key != LyraGlobal.OFFICIALTICKERCODE)
                    continue;

                if (recvBalances.ContainsKey(chg.Key))
                    recvBalances[chg.Key] += chg.Value;
                else
                    recvBalances.Add(chg.Key, chg.Value);
            }

            receiveBlock.Balances = recvBalances.ToLongDict();

            receiveBlock.InitializeBlock(latestPoolBlock, (hash) => Signatures.GetSignature(_sys.PosWallet.PrivateKey, hash, _sys.PosWallet.AccountId));

            var tx = new ServiceWithActionTx(sendBlock.Hash)
            {
                PoolId = latestPoolBlock.AccountID
            };
            await QueueBlockForPoolAsync(receiveBlock, tx);  // create pool / withdraw
        }

        /// <summary>
        /// receive pool send (swap in) and do send other token out (swap out)
        /// </summary>
        /// <param name="sendBlock"></param>
        /// <returns></returns>
        private async Task ReceivePoolSwapInAsync(SendTransferBlock sendBlock)
        {
            // assume all send variables are legal
            // token0/1, amount, etc.

            var lsb = await _sys.Storage.GetLastServiceBlockAsync();
            var swapInBlock = new PoolSwapInBlock
            {
                AccountID = sendBlock.DestinationAccountId,
                VoteFor = null,
                ServiceHash = lsb.Hash,
                SourceHash = sendBlock.Hash,
                Balances = new Dictionary<string, long>(),
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee
            };

            swapInBlock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored
            swapInBlock.AddTag("relhash", sendBlock.Hash);  // pool swap in
            swapInBlock.AddTag("type", "plswapin");       // pool swap in

            TransactionBlock prevSend = await _sys.Storage.FindBlockByHashAsync(sendBlock.PreviousHash) as TransactionBlock;
            var txInfo = sendBlock.GetBalanceChanges(prevSend);

            TransactionBlock latestPoolBlock = await _sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;
            PoolGenesisBlock poolGenesis = await _sys.Storage.FindFirstBlockAsync(latestPoolBlock.AccountID) as PoolGenesisBlock;

            var depositBalance = new Dictionary<string, decimal>();
            if (latestPoolBlock.Balances.Any())
            {
                var lastBalance = latestPoolBlock.Balances.ToDecimalDict();

                // the rito must be preserved for every deposition
                var poolRito = lastBalance[poolGenesis.Token0] / lastBalance[poolGenesis.Token1];
                foreach (var oldBalance in lastBalance)
                {
                    if(txInfo.Changes.ContainsKey(oldBalance.Key))
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
            swapInBlock.InitializeBlock(latestPoolBlock, (hash) => Signatures.GetSignature(_sys.PosWallet.PrivateKey, hash, _sys.PosWallet.AccountId));

            var tx = new ServiceWithActionTx(sendBlock.Hash)
            {
                PoolId = latestPoolBlock.AccountID
            };
            await QueueBlockForPoolAsync(swapInBlock, tx);   // pool swap in
        }

        /// <summary>
        /// send token out (swap out)
        /// </summary>
        /// <param name="swapInBlock"></param>
        /// <param name="poolId"></param>
        /// <param name="targetAccountId"></param>
        /// <param name="token"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        private async Task SendPoolSwapOutTokenAsync(PoolSwapInBlock swapInBlock, string targetAccountId, SwapCalculator cfg)
        {
            var lsb = await _sys.Storage.GetLastServiceBlockAsync();
            var swapOutBlock = new PoolSwapOutBlock()
            {
                AccountID = swapInBlock.AccountID,
                ServiceHash = lsb.Hash,
                DestinationAccountId = targetAccountId,
                Balances = new Dictionary<string, long>(),
                Tags = null,
                Fee = cfg.PayToAuthorizer,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.Regular,
                RelatedTx = swapInBlock.Hash
            };

            var sendBlock = await _sys.Storage.FindBlockByHashAsync(swapInBlock.SourceHash) as SendTransferBlock;

            swapOutBlock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored
            swapOutBlock.AddTag("relhash", sendBlock.Hash);  // pool swap out action
            swapOutBlock.AddTag("type", "plswapout");       // pool swap in

            var poolGenesisBlock = await _sys.Storage.FindFirstBlockAsync(swapInBlock.AccountID) as PoolGenesisBlock;
            var poolLatestBlock = await _sys.Storage.FindLatestBlockAsync(swapInBlock.AccountID) as TransactionBlock;

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
                // tokenOut == LYR
                nextBalance[tokenIn] = curBalance[tokenIn];  // pool fee leave in the pool
                nextBalance[tokenOut] = curBalance[tokenOut] - cfg.SwapOutAmount - cfg.PayToAuthorizer;
            }

            swapOutBlock.Balances = nextBalance.ToLongDict();
            swapOutBlock.Shares = (poolLatestBlock as IPool).Shares;
            swapOutBlock.InitializeBlock(poolLatestBlock, (hash) => Signatures.GetSignature(_sys.PosWallet.PrivateKey, hash, _sys.PosWallet.AccountId));

            // verify 
            var chgs = swapOutBlock.GetBalanceChanges(poolLatestBlock);
            if (chgs.Changes[cfg.SwapOutToken] != cfg.SwapOutAmount)
                _log.LogError($"In swap out block gen: Swap out should be {cfg.SwapOutAmount} {cfg.SwapOutToken} but {chgs.Changes[cfg.SwapOutToken]}");
            if(chgs.FeeAmount != cfg.PayToAuthorizer)
                _log.LogError($"In swap out block gen: Fee should be {cfg.PayToAuthorizer} but {chgs.FeeAmount} ");

            await QueueTxActionBlockAsync(swapOutBlock);
        }

        /// <summary>
        /// receive send (depositon) and update shares
        /// </summary>
        /// <param name="sendBlock"></param>
        /// <returns></returns>
        private async Task ReceivePoolDepositionAsync(SendTransferBlock sendBlock)
        {
            // assume all send variables are legal
            // token0/1, amount, etc.

            var lsb = await _sys.Storage.GetLastServiceBlockAsync();
            var depositBlock = new PoolDepositBlock
            {
                AccountID = sendBlock.DestinationAccountId,
                VoteFor = null,
                ServiceHash = lsb.Hash,
                SourceHash = sendBlock.Hash,
                Balances = new Dictionary<string, long>(),
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee
            };

            depositBlock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored
            depositBlock.AddTag("relhash", sendBlock.Hash);  // pool deposit
            depositBlock.AddTag("type", "pladdin");       // pool add liquidate

            TransactionBlock prevSend = await _sys.Storage.FindBlockByHashAsync(sendBlock.PreviousHash) as TransactionBlock;
            var txInfo = sendBlock.GetBalanceChanges(prevSend);

            TransactionBlock latestPoolBlock = await _sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;
            PoolGenesisBlock poolGenesis = await _sys.Storage.FindFirstBlockAsync(latestPoolBlock.AccountID) as PoolGenesisBlock;

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

            depositBlock.InitializeBlock(latestPoolBlock, (hash) => Signatures.GetSignature(_sys.PosWallet.PrivateKey, hash, _sys.PosWallet.AccountId));

            var tx = new ServiceTx(sendBlock.Hash)
            {
                PoolId = latestPoolBlock.AccountID
            };
            await QueueBlockForPoolAsync(depositBlock, tx);  // pool deposition
        }

        /// <summary>
        /// send 1LYR (as fee) to pool and pool send share of token out and update shares
        /// </summary>
        /// <param name="recvBlock"></param>
        /// <param name="poolId"></param>
        /// <param name="targetAccountId"></param>
        /// <returns></returns>
        private async Task SendWithdrawFundsAsync(ReceiveTransferBlock recvBlock, string poolId, string targetAccountId)
        {
            var lsb = await _sys.Storage.GetLastServiceBlockAsync();
            PoolWithdrawBlock withdrawBlock = new PoolWithdrawBlock()
            {
                AccountID = poolId,
                ServiceHash = lsb.Hash,
                DestinationAccountId = targetAccountId,
                Balances = new Dictionary<string, long>(),
                Tags = null,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,
                RelatedTx = recvBlock.Hash
            };

            var sendBlock = await _sys.Storage.FindBlockByHashAsync(recvBlock.SourceHash) as SendTransferBlock;

            withdrawBlock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored
            withdrawBlock.AddTag("relhash", sendBlock.Hash);  // pool withdraw action
            withdrawBlock.AddTag("type", "plrmout");       // pool remove liquidate

            var poolGenesisBlock = await _sys.Storage.FindFirstBlockAsync(poolId) as PoolGenesisBlock;
            var poolLatestBlock = await _sys.Storage.FindLatestBlockAsync(poolId) as TransactionBlock;

            var curBalance = poolLatestBlock.Balances.ToDecimalDict();
            var curShares = (poolLatestBlock as IPool).Shares.ToRitoDecimalDict();

            var nextBalance = poolLatestBlock.Balances.ToDecimalDict();
            var nextShares = (poolLatestBlock as IPool).Shares.ToRitoDecimalDict();

            var usersShare = curShares[targetAccountId];
            var amountsToSend = new Dictionary<string, decimal>
            {
                { poolGenesisBlock.Token0, curBalance[poolGenesisBlock.Token0] * usersShare },
                { poolGenesisBlock.Token1, curBalance[poolGenesisBlock.Token1] * usersShare }
            };

            nextBalance[poolGenesisBlock.Token0] -= amountsToSend[poolGenesisBlock.Token0];
            nextBalance[poolGenesisBlock.Token1] -= amountsToSend[poolGenesisBlock.Token1];
            nextShares.Remove(targetAccountId);

            foreach (var share in curShares)
            {
                if (share.Key == targetAccountId)
                    continue;

                nextShares[share.Key] = (share.Value * curBalance[poolGenesisBlock.Token0]) / nextBalance[poolGenesisBlock.Token0];
            }

            withdrawBlock.Balances = nextBalance.ToLongDict();
            withdrawBlock.Shares = nextShares.ToRitoLongDict();

            withdrawBlock.InitializeBlock(poolLatestBlock, (hash) => Signatures.GetSignature(_sys.PosWallet.PrivateKey, hash, _sys.PosWallet.AccountId));

            await QueueTxActionBlockAsync(withdrawBlock);
        }

        /// <summary>
        /// create pool on receive fee
        /// </summary>
        /// <param name="recvBlock"></param>
        /// <param name="token0"></param>
        /// <param name="token1"></param>
        /// <returns></returns>
        private async Task CreateLiquidatePoolAsync(SendTransferBlock send, ReceiveTransferBlock recvBlock, string token0, string token1)
        {
            var sb = await _sys.Storage.GetLastServiceBlockAsync();
            var pf = await _sys.Storage.GetPoolFactoryAsync();

            // get token gensis to make the token name proper
            var token0Gen = await _sys.Storage.FindTokenGenesisBlockAsync(null, token0);
            var token1Gen = await _sys.Storage.FindTokenGenesisBlockAsync(null, token1);

            if(token0Gen == null || token1Gen == null)
            {
                return;
            }

            var arrStr = new[] { token0Gen.Ticker, token1Gen.Ticker };
            Array.Sort(arrStr);

            // create a semi random account for pool.
            // it can be verified by other nodes.
            var keyStr = $"{pf.Height},{arrStr[0]},{arrStr[1]},{pf.Hash}";
            var (_, AccountId) = Signatures.GenerateWallet(Encoding.ASCII.GetBytes(keyStr).Take(32).ToArray());

            var poolGenesis = new PoolGenesisBlock
            {
                Height = 1,
                AccountType = AccountTypes.Standard,
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
                RelatedTx = recvBlock.Hash
            };

            poolGenesis.AddTag(Block.MANAGEDTAG, "");   // value is always ignored
            poolGenesis.AddTag("relhash", send.Hash);  // pool withdraw action
            poolGenesis.AddTag("type", "pfcreat");       // pool remove liquidate

            // pool blocks are service block so all service block signed by leader node
            poolGenesis.InitializeBlock(null, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);

            await QueueTxActionBlockAsync(poolGenesis);
        }

        private void ProcessSendBlock(SendTransferBlock send)
        {
            if (send.DestinationAccountId == PoolFactoryBlock.FactoryAccount)
            {
                _log.LogInformation("Pool operation requested...");
                _ = Task.Run(async () =>
                {
                    try
                    {
                        switch (send.Tags[Block.REQSERVICETAG])
                        {
                            case "":
                                await ReceivePoolFactoryFeeAsync(send, "pfrecv");
                                break;
                            case "poolwithdraw":
                                await ReceivePoolFactoryFeeAsync(send, "pfwithdraw");
                                break;
                            default:
                                _log.LogError("pool factory not allow such action: " + send.Tags[Block.REQSERVICETAG]);
                                break;
                        }

                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning("Pool factory error: " + ex.ToString());
                    }
                });
            }
            else
            {
                // check which pool
                _log.LogInformation($"Action to pool ... svcreq = {send.Tags[Block.REQSERVICETAG]}");
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var pool = await _sys.Storage.FindLatestBlockAsync(send.DestinationAccountId) as TransactionBlock;
                        if (pool == null)
                        {
                            _log.LogWarning($"destination pool {send.DestinationAccountId} not exists.");
                            return;
                        }

                        var poolGenesis = await _sys.Storage.FindFirstBlockAsync(send.DestinationAccountId) as PoolGenesisBlock;
                        if (send.Tags[Block.REQSERVICETAG] == "swaptoken")
                        {
                            await ReceivePoolSwapInAsync(send);
                        }
                        else
                        {
                            await ReceivePoolDepositionAsync(send);

                            //if (result == ConsensusResult.Yea)
                            //{
                            //    _log.LogInformation($"Adding liquidate to pool {send.DestinationAccountId} is successfully.");
                            //}
                            //else
                            //{
                            //    _log.LogWarning($"Adding liquidate to pool {send.DestinationAccountId} is failed.");
                            //}
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning("Pool action error: " + ex.ToString());
                    }
                });
            }
        }

        private void ProcessManagedBlock(Block block, ConsensusResult? result)
        {
            if (!block.ContainsTag("type"))
            {
                _log.LogWarning("A MANAGEDTAG block not have type.");
                return;
            }

            if (!block.ContainsTag("relhash"))
            {
                _log.LogWarning("A MANAGEDTAG block not have related hash.");
                return;
            }

            var blockRelHash = block.Tags["relhash"];
            var blockType = block.Tags["type"];
            var poolBlock = block as TransactionBlock;

            _ = Task.Run(async () =>
            {

                //_svcQueue[poolBlock.AccountID].
                switch (blockType)
                {
                    case "pfrecv":      // pool factory receive
                        _svcQueue.Finish(poolBlock.AccountID, blockRelHash, poolBlock.Hash, null);

                        await PoolFactoryRecvActionAsync(block as ReceiveTransferBlock, result);
                        break;
                    case "pfwithdraw":    // pool factory receive
                        _svcQueue.Finish(poolBlock.AccountID, blockRelHash, poolBlock.Hash, null);

                        var send = await _sys.Storage.FindBlockByHashAsync((poolBlock as ReceiveTransferBlock).SourceHash) as SendTransferBlock;
                        var poolId = send.Tags["poolid"];
                        _log.LogInformation($"Withdraw from pool {poolId}...");
                        await SendWithdrawFundsAsync(block as ReceiveTransferBlock, poolId, send.AccountID);
                        break;

                    case "pladdin":  // pool deposition
                        _svcQueue.Finish(poolBlock.AccountID, blockRelHash, poolBlock.Hash, null);

                        break;
                    case "plswapin":  // pool swapin
                        _svcQueue.Finish(poolBlock.AccountID, blockRelHash, poolBlock.Hash, null);

                        await PoolRecvSwapInConsensusActionAsync(block as ReceiveTransferBlock, result);
                        break;

                    case "pfcreat":
                    case "plswapout":
                    case "plrmout":
                        _svcQueue.Finish(poolBlock.AccountID, blockRelHash, null, poolBlock.Hash);
                        break;
                    default:
                        _log.LogWarning($"MANAGEDTAG Unsupported type: {block.Tags["type"]}");
                        break;
                }

                _log.LogInformation($"svcqueue has {_svcQueue.AllTx.Count} items.");
            });
        }
    }
}
