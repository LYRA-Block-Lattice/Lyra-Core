using Lyra.Core.API;
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
        private async Task QueueBlockForPool(Block block, string associatedHash)
        {
            if (block == null || string.IsNullOrEmpty(associatedHash))
                throw new ArgumentNullException();

            if(IsThisNodeLeader)
            {
                await SendBlockToConsensusAndWaitResultAsync(block);
            }
        }

        private async Task PoolFactoryRecvConsensusAction(Block block, ConsensusResult? result)
        {
            if (result == ConsensusResult.Yea)
            {
                var recvBlock = block as ReceiveTransferBlock;
                var send = await _sys.Storage.FindBlockByHashAsync(recvBlock.SourceHash) as TransactionBlock;
                if (send.Tags[Block.REQSERVICETAG] == "")
                {
                    // then create pool for it.
                    _log.LogInformation("Creating pool ...");
                    await CreateLiquidatePoolAsync(recvBlock, send.Tags["token0"], send.Tags["token1"]);
                    //if (poolCreateResult == ConsensusResult.Yea)
                    //    _log.LogInformation($"Pool created successfully.");
                    //else
                    //    _log.LogWarning("Can't create pool.");
                }
                else if (send.Tags[Block.REQSERVICETAG] == "poolwithdraw")
                {
                    var poolId = send.Tags["poolid"];

                    _log.LogInformation($"Withdraw from pool {poolId}...");

                    await SendWithdrawFunds(recvBlock, poolId, send.AccountID);
                }
            }
            else
            {
                _log.LogWarning("Pool factory not receive funds properly.");
            }
        }

        private async Task PoolRecvSwapInConsensusAction(Block block, ConsensusResult? result)
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
                if (result == ConsensusResult.Yea)
                {
                    var swapRito = Math.Round(recvBlockPrev.Balances[poolGenesis.Token0].ToBalanceDecimal() / recvBlockPrev.Balances[poolGenesis.Token1].ToBalanceDecimal(), LyraGlobal.RITOPRECISION);

                    if (kvp.Key == poolGenesis.Token0)
                    {
                        var token1ToGet = Math.Round(kvp.Value / swapRito, 8);
                        _log.LogInformation($"Sending out {token1ToGet}");
                        await SendPoolSwapOutToken(swapInBlock, swapInBlock.AccountID, send.AccountID, poolGenesis.Token1, token1ToGet);
                    }

                    if (kvp.Key == poolGenesis.Token1)
                    {
                        var token0ToGet = Math.Round(kvp.Value * swapRito, 8);
                        _log.LogInformation($"Sending out {token0ToGet}");
                        await SendPoolSwapOutToken(swapInBlock, swapInBlock.AccountID, send.AccountID, poolGenesis.Token0, token0ToGet);
                    }
                }
            }
        }

        private async Task ReceivePoolFactoryFeeAsync(SendTransferBlock sendBlock)
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
                FeeType = AuthorizationFeeTypes.NoFee
            };

            receiveBlock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored
            receiveBlock.AddTag("relhash", sendBlock.Hash);  // related hash
            receiveBlock.AddTag("type", "pfrecv");       // pool factory receive

            TransactionBlock prevSend = await _sys.Storage.FindBlockByHashAsync(sendBlock.PreviousHash) as TransactionBlock;
            var txInfo = sendBlock.GetBalanceChanges(prevSend);

            TransactionBlock latestBlock = await _sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;

            // ignore any token but LYR. keep the block clean.
            if (!txInfo.Changes.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
                return;

            var latestBalances = latestBlock.Balances.ToDecimalDict();
            var recvBalances = latestBlock.Balances.ToDecimalDict();
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

            receiveBlock.InitializeBlock(latestBlock, (hash) => Signatures.GetSignature(_sys.PosWallet.PrivateKey, hash, _sys.PosWallet.AccountId));

            await QueueBlockForPool(receiveBlock, sendBlock.Hash);
        }

        private async Task ReceivePoolSwapInAsync(SendTransferBlock sendBlock)
        {
            // assume all send variables are legal
            // token0/1, amount, etc.

            ConsensusResult? result = null;

            var lsb = await _sys.Storage.GetLastServiceBlockAsync();
            var swapInBlock = new PoolSwapInBlock
            {
                AccountID = sendBlock.DestinationAccountId,
                VoteFor = null,
                ServiceHash = lsb.Hash,
                SourceHash = sendBlock.Hash,
                Balances = new Dictionary<string, long>(),
                Fee = 0,
                FeeType = AuthorizationFeeTypes.NoFee
            };

            swapInBlock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored
            swapInBlock.AddTag("relhash", sendBlock.Hash);  // related hash
            swapInBlock.AddTag("type", "plswaprecv");       // pool swap in

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

            await QueueBlockForPool(swapInBlock, sendBlock.Hash);
        }

        private async Task SendPoolSwapOutToken(PoolSwapInBlock swapInBlock, string poolId, string targetAccountId, string token, decimal amount)
        {
            var lsb = await _sys.Storage.GetLastServiceBlockAsync();
            var swapOutBlock = new PoolSwapOutBlock()
            {
                AccountID = poolId,
                ServiceHash = lsb.Hash,
                DestinationAccountId = targetAccountId,
                Balances = new Dictionary<string, long>(),
                Tags = null,
                Fee = 0,
                FeeType = AuthorizationFeeTypes.NoFee,
                RelatedTx = swapInBlock.Hash
            };

            swapOutBlock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored
            swapOutBlock.AddTag("relhash", swapInBlock.Hash);  // related hash

            var poolGenesisBlock = await _sys.Storage.FindFirstBlockAsync(poolId) as PoolGenesisBlock;
            var poolLatestBlock = await _sys.Storage.FindLatestBlockAsync(poolId) as TransactionBlock;

            var curBalance = poolLatestBlock.Balances.ToDecimalDict();
            var nextBalance = poolLatestBlock.Balances.ToDecimalDict();

            nextBalance[token] = curBalance[token] - amount;

            swapOutBlock.Balances = nextBalance.ToLongDict();
            swapOutBlock.Shares = (poolLatestBlock as IPool).Shares;
            swapOutBlock.InitializeBlock(poolLatestBlock, (hash) => Signatures.GetSignature(_sys.PosWallet.PrivateKey, hash, _sys.PosWallet.AccountId));

            await QueueBlockForPool(swapOutBlock, swapInBlock.Hash);
        }

        private async Task ReceivePoolDepositionAsync(SendTransferBlock sendBlock)
        {
            // assume all send variables are legal
            // token0/1, amount, etc.

            ConsensusResult? result = null;

            var lsb = await _sys.Storage.GetLastServiceBlockAsync();
            var depositBlock = new PoolDepositBlock
            {
                AccountID = sendBlock.DestinationAccountId,
                VoteFor = null,
                ServiceHash = lsb.Hash,
                SourceHash = sendBlock.Hash,
                Balances = new Dictionary<string, long>(),
                Fee = 0,
                FeeType = AuthorizationFeeTypes.NoFee
            };

            depositBlock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored
            depositBlock.AddTag("relhash", sendBlock.Hash);  // related hash
            depositBlock.AddTag("type", "pladd");       // pool add liquidate

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

            await QueueBlockForPool(depositBlock, sendBlock.Hash);
        }

        private async Task SendWithdrawFunds(ReceiveTransferBlock recvBlock, string poolId, string targetAccountId)
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
                FeeType = AuthorizationFeeTypes.NoFee,
                RelatedTx = recvBlock.Hash
            };

            withdrawBlock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored
            withdrawBlock.AddTag("relhash", recvBlock.Hash);  // related hash

            var poolGenesisBlock = await _sys.Storage.FindFirstBlockAsync(poolId) as PoolGenesisBlock;
            var poolLatestBlock = await _sys.Storage.FindLatestBlockAsync(poolId) as TransactionBlock;

            var curBalance = poolLatestBlock.Balances.ToDecimalDict();
            var curShares = (poolLatestBlock as IPool).Shares.ToRitoDecimalDict();

            var nextBalance = poolLatestBlock.Balances.ToDecimalDict();
            var nextShares = (poolLatestBlock as IPool).Shares.ToRitoDecimalDict();

            var usersShare = curShares[targetAccountId];
            var amountsToSend = new Dictionary<string, decimal>();
            amountsToSend.Add(poolGenesisBlock.Token0, curBalance[poolGenesisBlock.Token0] * usersShare);
            amountsToSend.Add(poolGenesisBlock.Token1, curBalance[poolGenesisBlock.Token1] * usersShare);

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

            await QueueBlockForPool(withdrawBlock, recvBlock.Hash);
        }

        private async Task CreateLiquidatePoolAsync(ReceiveTransferBlock recvBlock, string token0, string token1)
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
            var randAccount = Signatures.GenerateWallet(Encoding.ASCII.GetBytes(keyStr).Take(32).ToArray());

            var poolBlock = new PoolGenesisBlock
            {
                Height = 1,
                AccountType = AccountTypes.Standard,
                AccountID = randAccount.AccountId,        // in fact we not use this account.
                Balances = new Dictionary<string, long>(),
                PreviousHash = sb.Hash,
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeType = AuthorizationFeeTypes.NoFee,

                // pool specified config
                Token0 = arrStr[0],
                Token1 = arrStr[1]
            };

            poolBlock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored
            

            // pool blocks are service block so all service block signed by leader node
            poolBlock.InitializeBlock(null, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            await QueueBlockForPool(poolBlock, recvBlock.Hash);
        }
    }
}
