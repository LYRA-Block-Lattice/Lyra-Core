using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize
{
    public partial class ConsensusService
    {
        private async Task<(ConsensusResult?, ReceiveTransferBlock)> ReceivePoolFactoryFeeAsync(SendTransferBlock sendBlock)
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

            TransactionBlock prevSend = await _sys.Storage.FindBlockByHashAsync(sendBlock.PreviousHash) as TransactionBlock;
            var txInfo = sendBlock.GetBalanceChanges(prevSend);

            TransactionBlock latestBlock = await _sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;

            // ignore any token but LYR. keep the block clean.
            if (!txInfo.Changes.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
                return (ConsensusResult.Uncertain, null);

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

            var result = await SendBlockToConsensusAndWaitResultAsync(receiveBlock);
            return (result, receiveBlock);
        }

        private async Task<(ConsensusResult?, KeyValuePair<string, decimal>, PoolSwapInBlock)> ReceivePoolSwapInAsync(SendTransferBlock sendBlock)
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

            result = await SendBlockToConsensusAndWaitResultAsync(swapInBlock);

            return (result, txInfo.Changes.First(), swapInBlock);
        }

        private async Task<ConsensusResult?> SendPoolSwapOutToken(PoolSwapInBlock swapInBlock, string poolId, string targetAccountId, string token, decimal amount)
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

            var poolGenesisBlock = await _sys.Storage.FindFirstBlockAsync(poolId) as PoolGenesisBlock;
            var poolLatestBlock = await _sys.Storage.FindLatestBlockAsync(poolId) as TransactionBlock;

            var curBalance = poolLatestBlock.Balances.ToDecimalDict();
            var nextBalance = poolLatestBlock.Balances.ToDecimalDict();

            nextBalance[token] = curBalance[token] - amount;

            swapOutBlock.Balances = nextBalance.ToLongDict();
            swapOutBlock.Shares = (poolLatestBlock as IPool).Shares;
            swapOutBlock.InitializeBlock(poolLatestBlock, (hash) => Signatures.GetSignature(_sys.PosWallet.PrivateKey, hash, _sys.PosWallet.AccountId));

            var result = await SendBlockToConsensusAndWaitResultAsync(swapOutBlock);

            return result;
        }

        private async Task<ConsensusResult?> ReceivePoolDepositionAsync(SendTransferBlock sendBlock)
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

            result = await SendBlockToConsensusAndWaitResultAsync(depositBlock);

            return result;
        }

        private async Task<ConsensusResult?> SendWithdrawFunds(ReceiveTransferBlock recvBlock, string poolId, string targetAccountId)
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

            var result = await SendBlockToConsensusAndWaitResultAsync(withdrawBlock);

            return result;
        }

        private async Task<ConsensusResult?> CreateLiquidatePoolAsync(string token0, string token1)
        {
            var sb = await _sys.Storage.GetLastServiceBlockAsync();
            var pf = await _sys.Storage.GetPoolFactoryAsync();
            var arrStr = new[] { token0, token1 };
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
            return await SendBlockToConsensusAndWaitResultAsync(poolBlock);
        }
    }
}
