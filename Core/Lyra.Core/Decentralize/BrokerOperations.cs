using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize
{
    public class BrokerOperations
    {
        public static async Task<ReceiveTransferBlock> ReceivePoolFactoryFeeAsync(DagSystem sys, SendTransferBlock sendBlock)
        {
            var lsb = await sys.Storage.GetLastServiceBlockAsync();
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
            //receiveBlock.AddTag("type", actionType);       // pool factory receive

            TransactionBlock prevSend = await sys.Storage.FindBlockByHashAsync(sendBlock.PreviousHash) as TransactionBlock;
            var txInfo = sendBlock.GetBalanceChanges(prevSend);

            TransactionBlock latestPoolBlock = await sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;

            // ignore any token but LYR. keep the block clean.
            //if (!txInfo.Changes.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
            //    return;

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

            receiveBlock.InitializeBlock(latestPoolBlock, (hash) => Signatures.GetSignature(sys.PosWallet.PrivateKey, hash, sys.PosWallet.AccountId));

            return receiveBlock;
            //var tx = new ServiceWithActionTx(sendBlock.Hash)
            //{
            //    PoolId = latestPoolBlock.AccountID
            //};
            //await QueueBlockForPoolAsync(receiveBlock, tx);  // create pool / withdraw
        }

        public static async Task<TransactionBlock> CNOCreateLiquidatePoolAsync(DagSystem sys, SendTransferBlock send/*, ReceiveTransferBlock recvBlock, string token0, string token1*/)
        {
            var sb = await sys.Storage.GetLastServiceBlockAsync();

            // get token gensis to make the token name proper
            var token0Gen = await sys.Storage.FindTokenGenesisBlockAsync(null, send.Tags["token0"]);
            var token1Gen = await sys.Storage.FindTokenGenesisBlockAsync(null, send.Tags["token1"]);

            //if (token0Gen == null || token1Gen == null)
            //{
            //    return;
            //}

            var arrStr = new[] { token0Gen.Ticker, token1Gen.Ticker };
            Array.Sort(arrStr);

            // create a semi random account for pool.
            // it can be verified by other nodes.
            var keyStr = $"{send.Hash.Substring(0, 16)},{arrStr[0]},{arrStr[1]},{send.AccountID}";
            var (_, AccountId) = Signatures.GenerateWallet(Encoding.ASCII.GetBytes(keyStr).Take(32).ToArray());

            var recvBlock = await sys.Storage.FindBlockBySourceHashAsync(send.Hash);
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

            poolGenesis.AddTag(Block.MANAGEDTAG, "");   // value is always ignored
            poolGenesis.AddTag("relhash", send.Hash);  // pool withdraw action
            poolGenesis.AddTag("type", "pfcreat");       // pool remove liquidate

            // pool blocks are service block so all service block signed by leader node
            poolGenesis.InitializeBlock(null, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);

            return poolGenesis;
            //await QueueTxActionBlockAsync(poolGenesis);
        }

        public static async Task<TransactionBlock> AddPoolLiquidateAsync(DagSystem sys, SendTransferBlock sendBlock)
        {
            // assume all send variables are legal
            // token0/1, amount, etc.

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
                FeeType = AuthorizationFeeTypes.NoFee
            };

            depositBlock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored
            depositBlock.AddTag("relhash", sendBlock.Hash);  // pool deposit
            depositBlock.AddTag("type", "pladdin");       // pool add liquidate

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

            depositBlock.InitializeBlock(latestPoolBlock, (hash) => Signatures.GetSignature(sys.PosWallet.PrivateKey, hash, sys.PosWallet.AccountId));
            return depositBlock;
            //var tx = new ServiceTx(sendBlock.Hash)
            //{
            //    PoolId = latestPoolBlock.AccountID
            //};
            //await QueueBlockForPoolAsync(depositBlock, tx);  // pool deposition
        }

        public static async Task<TransactionBlock> SendWithdrawFundsAsync(DagSystem sys, SendTransferBlock send/*, string poolId, string targetAccountId*/)
        {
            var lsb = await sys.Storage.GetLastServiceBlockAsync();
            var recvBlock = await sys.Storage.FindBlockBySourceHashAsync(send.Hash);
            var poolId = send.Tags["poolid"];
            PoolWithdrawBlock withdrawBlock = new PoolWithdrawBlock()
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

            withdrawBlock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored            

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

            withdrawBlock.InitializeBlock(poolLatestBlock, (hash) => Signatures.GetSignature(sys.PosWallet.PrivateKey, hash, sys.PosWallet.AccountId));
            return withdrawBlock;
            //await QueueTxActionBlockAsync(withdrawBlock);
        }

        public static async Task<TransactionBlock> ReceivePoolSwapInAsync(DagSystem sys, SendTransferBlock sendBlock)
        {
            // assume all send variables are legal
            // token0/1, amount, etc.

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
                FeeType = AuthorizationFeeTypes.NoFee
            };

            swapInBlock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored
            swapInBlock.AddTag("relhash", sendBlock.Hash);  // pool swap in
            swapInBlock.AddTag("type", "plswapin");       // pool swap in

            TransactionBlock prevSend = await sys.Storage.FindBlockByHashAsync(sendBlock.PreviousHash) as TransactionBlock;
            var txInfo = sendBlock.GetBalanceChanges(prevSend);

            TransactionBlock latestPoolBlock = await sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;
            PoolGenesisBlock poolGenesis = await sys.Storage.FindFirstBlockAsync(latestPoolBlock.AccountID) as PoolGenesisBlock;

            var depositBalance = new Dictionary<string, decimal>();
            if (latestPoolBlock.Balances.Any())
            {
                var lastBalance = latestPoolBlock.Balances.ToDecimalDict();

                // the rito must be preserved for every deposition
                var poolRito = lastBalance[poolGenesis.Token0] / lastBalance[poolGenesis.Token1];
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
            swapInBlock.InitializeBlock(latestPoolBlock, (hash) => Signatures.GetSignature(sys.PosWallet.PrivateKey, hash, sys.PosWallet.AccountId));
            return swapInBlock;

            //var tx = new ServiceWithActionTx(sendBlock.Hash)
            //{
            //    PoolId = latestPoolBlock.AccountID
            //};
            //await QueueBlockForPoolAsync(swapInBlock, tx);   // pool swap in
        }

        public static async Task<List<TransactionBlock>> SendPoolSwapOutTokenAsync(DagSystem sys, ReceiveTransferBlock recv/*, string targetAccountId, SwapCalculator cfg*/)
        {
            var swapInBlock = recv as PoolSwapInBlock;
            var recvBlockPrev = await sys.Storage.FindBlockByHashAsync(recv.PreviousHash) as TransactionBlock;
            var recvChgs = swapInBlock.GetBalanceChanges(recvBlockPrev);
            var kvp = recvChgs.Changes.First();
            var poolGenesis = await sys.Storage.FindFirstBlockAsync(swapInBlock.AccountID) as PoolGenesisBlock;
            var cfg = new SwapCalculator(poolGenesis.Token0, poolGenesis.Token1, recvBlockPrev,
                    kvp.Key, kvp.Value, 0);

            var lsb = await sys.Storage.GetLastServiceBlockAsync();
            var send = await sys.Storage.FindBlockByHashAsync(recv.SourceHash) as SendTransferBlock;
            var swapOutBlock = new PoolSwapOutBlock()
            {
                AccountID = send.DestinationAccountId,
                ServiceHash = lsb.Hash,
                DestinationAccountId = send.AccountID,
                Balances = new Dictionary<string, long>(),
                Tags = null,
                Fee = cfg.PayToAuthorizer,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.Regular,
                RelatedTx = send.Hash
            };

            swapOutBlock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored
            swapOutBlock.AddTag("relhash", send.Hash);  // pool swap out action
            swapOutBlock.AddTag("type", "plswapout");       // pool swap in

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
                // tokenOut == LYR
                nextBalance[tokenIn] = curBalance[tokenIn];  // pool fee leave in the pool
                nextBalance[tokenOut] = curBalance[tokenOut] - cfg.SwapOutAmount - cfg.PayToAuthorizer;
            }

            swapOutBlock.Balances = nextBalance.ToLongDict();
            swapOutBlock.Shares = (poolLatestBlock as IPool).Shares;
            swapOutBlock.InitializeBlock(poolLatestBlock, (hash) => Signatures.GetSignature(sys.PosWallet.PrivateKey, hash, sys.PosWallet.AccountId));

            // verify 
            var chgs = swapOutBlock.GetBalanceChanges(poolLatestBlock);
            //if (chgs.Changes[cfg.SwapOutToken] != cfg.SwapOutAmount)
            //    _log.LogError($"In swap out block gen: Swap out should be {cfg.SwapOutAmount} {cfg.SwapOutToken} but {chgs.Changes[cfg.SwapOutToken]}");
            //if (chgs.FeeAmount != cfg.PayToAuthorizer)
            //    _log.LogError($"In swap out block gen: Fee should be {cfg.PayToAuthorizer} but {chgs.FeeAmount} ");

            return new List<TransactionBlock> { swapOutBlock };
            //await QueueTxActionBlockAsync(swapOutBlock);
        }

        public static async Task<TransactionBlock> CNOCreateProfitingAccountAsync(DagSystem sys, SendTransferBlock send)
        {
            var sb = await sys.Storage.GetLastServiceBlockAsync();

            // create a semi random account for pool.
            // it can be verified by other nodes.
            var keyStr = $"{send.Hash.Substring(0, 16)},{send.Tags["ptype"]},{send.Tags["share"]},{send.Tags["seats"]},{send.AccountID}";
            var (_, AccountId) = Signatures.GenerateWallet(Encoding.ASCII.GetBytes(keyStr).Take(32).ToArray());

            ProfitingType ptype;
            Enum.TryParse(send.Tags["ptype"], out ptype);
            var pftGenesis = new ProfitingGenesis
            {
                Height = 1,
                Name = send.Tags["name"],
                OwnerAccountId = send.AccountID,
                AccountType = AccountTypes.Profiting,
                AccountID = AccountId,        // in fact we not use this account.
                Balances = new Dictionary<string, long>(),
                PreviousHash = sb.Hash,
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,

                // pool specified config
                PType = ptype,
                ShareRito = decimal.Parse(send.Tags["share"]),
                Seats = int.Parse(send.Tags["seats"]),
                RelatedTx = send.Hash
            };

            pftGenesis.AddTag(Block.MANAGEDTAG, "");   // value is always ignored
            pftGenesis.AddTag("relhash", send.Hash);  // pool withdraw action
            pftGenesis.AddTag("type", "pfcrpft");       // pool remove liquidate

            // pool blocks are service block so all service block signed by leader node
            pftGenesis.InitializeBlock(null, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);

            return pftGenesis;
            //await QueueTxActionBlockAsync(poolGenesis);
        }

        public static async Task<TransactionBlock> CNOReceiveProfitAsync(DagSystem sys, SendTransferBlock send)
        {
            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var sendPrev = await sys.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock;
            var lastPft = await sys.Storage.FindLatestBlockAsync(send.DestinationAccountId) as TransactionBlock;

            var pftNext = new ProfitingBlock
            {
                Height = lastPft.Height + 1,
                Name = ((IBrokerAccount)lastPft).Name,
                OwnerAccountId = ((IBrokerAccount)lastPft).OwnerAccountId,
                AccountID = lastPft.AccountID,
                Balances = new Dictionary<string, long>(),
                PreviousHash = lastPft.Hash,
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,
                SourceHash = send.Hash,

                // profit specified config
                PType = ((IProfiting)lastPft).PType,
                ShareRito = ((IProfiting)lastPft).ShareRito,
                Seats = ((IProfiting)lastPft).Seats,
                RelatedTx = send.Hash
            };

            //TODO: think about multiple token
            var chgs = send.GetBalanceChanges(sendPrev);
            var oldBalance = lastPft.Balances.ContainsKey("LYR") ? lastPft.Balances[LyraGlobal.OFFICIALTICKERCODE] : 0;
            pftNext.Balances.Add(LyraGlobal.OFFICIALTICKERCODE, oldBalance + chgs.Changes[LyraGlobal.OFFICIALTICKERCODE].ToBalanceLong());

            pftNext.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            // pool blocks are service block so all service block signed by leader node
            pftNext.InitializeBlock(lastPft, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            return pftNext;
        }

        public static async Task<List<TransactionBlock>> CNORedistributeProfitAsync(DagSystem sys, ReceiveTransferBlock recv)
        {
            // create [multiple] send based on the staking
            var prevBlock = await sys.Storage.FindBlockByHashAsync(recv.PreviousHash) as TransactionBlock;
            var chgs = recv.GetBalanceChanges(prevBlock);

            // get stakings
            var stakers = await sys.Storage.FindAllStakersForProfitingAccountAsync(recv.AccountID);
            var targets = stakers.Take(((IProfiting)recv).Seats);
            // so 
            var amount = chgs.Changes[LyraGlobal.OFFICIALTICKERCODE] / targets.Count();
            TransactionBlock lastPft = recv;
            var lastBalance = recv.Balances[LyraGlobal.OFFICIALTICKERCODE];
            var sb = await sys.Storage.GetLastServiceBlockAsync();

            var allSends = new List<TransactionBlock>();
            foreach (var target in targets)
            {
                
                var pftSend = new BenefitingBlock
                {
                    Height = lastPft.Height + 1,
                    Name = ((IBrokerAccount)lastPft).Name,
                    OwnerAccountId = ((IBrokerAccount)lastPft).OwnerAccountId,
                    AccountID = lastPft.AccountID,
                    Balances = new Dictionary<string, long>(),
                    PreviousHash = lastPft.Hash,
                    ServiceHash = sb.Hash,
                    Fee = 0,
                    FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                    FeeType = AuthorizationFeeTypes.NoFee,
                    DestinationAccountId = target,

                    // profit specified config
                    PType = ((IProfiting)lastPft).PType,
                    ShareRito = ((IProfiting)lastPft).ShareRito,
                    Seats = ((IProfiting)lastPft).Seats,
                    RelatedTx = recv.SourceHash
                };

                //TODO: think about multiple token
                lastBalance -= amount.ToBalanceLong();
                pftSend.Balances.Add(LyraGlobal.OFFICIALTICKERCODE, lastBalance);

                pftSend.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

                // pool blocks are service block so all service block signed by leader node
                pftSend.InitializeBlock(lastPft, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
                allSends.Add(pftSend);

                lastPft = pftSend;
            }
            return allSends;
        }

        public static async Task<TransactionBlock> CNOCreateStakingAccountAsync(DagSystem sys, SendTransferBlock send)
        {
            var sb = await sys.Storage.GetLastServiceBlockAsync();

            // create a semi random account for pool.
            // it can be verified by other nodes.
            var keyStr = $"{send.Hash.Substring(0, 16)},{send.Tags["voting"]},{send.AccountID}";
            var (_, AccountId) = Signatures.GenerateWallet(Encoding.ASCII.GetBytes(keyStr).Take(32).ToArray());

            var stkGenesis = new StakingGenesis
            {
                Height = 1,
                Name = send.Tags["name"],
                OwnerAccountId = send.AccountID,
                AccountType = AccountTypes.Staking,
                AccountID = AccountId,        // in fact we not use this account.
                Balances = new Dictionary<string, long>(),
                PreviousHash = sb.Hash,
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,

                // pool specified config
                Voting = send.Tags["voting"],
                RelatedTx = send.Hash,
                Days = int.Parse(send.Tags["days"])
            };

            stkGenesis.Balances.Add(LyraGlobal.OFFICIALTICKERCODE, 0);

            stkGenesis.AddTag(Block.MANAGEDTAG, "");   // value is always ignored
            stkGenesis.AddTag("relhash", send.Hash);  // pool withdraw action
            stkGenesis.AddTag("type", "pfcrstk");       // pool remove liquidate

            // pool blocks are service block so all service block signed by leader node
            stkGenesis.InitializeBlock(null, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            return stkGenesis;
            //await QueueTxActionBlockAsync(stkGenesis);
        }

        public static async Task<TransactionBlock> CNOAddStakingAsync(DagSystem sys, SendTransferBlock send)
        {
            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var sendPrev = await sys.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock;
            var lastStk = await sys.Storage.FindLatestBlockAsync(send.DestinationAccountId) as TransactionBlock;

            var stkNext = new StakingBlock
            {
                Height = lastStk.Height + 1,
                Name = ((IBrokerAccount)lastStk).Name,
                OwnerAccountId = ((IBrokerAccount)lastStk).OwnerAccountId,
                //AccountType = ((IOpeningBlock)lastStk).AccountType,
                AccountID = lastStk.AccountID,
                Balances = new Dictionary<string, long>(),
                PreviousHash = lastStk.Hash,
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,
                SourceHash = send.Hash,

                // pool specified config
                Voting = ((IStaking)lastStk).Voting,
                RelatedTx = send.Hash
            };

            var chgs = send.GetBalanceChanges(sendPrev);
            stkNext.Balances.Add(LyraGlobal.OFFICIALTICKERCODE, lastStk.Balances[LyraGlobal.OFFICIALTICKERCODE] + chgs.Changes[LyraGlobal.OFFICIALTICKERCODE].ToBalanceLong());

            stkNext.AddTag(Block.MANAGEDTAG, "");   // value is always ignored
            stkNext.AddTag("relhash", send.Hash);  // pool withdraw action
            stkNext.AddTag("type", "pfaddstk");       // pool remove liquidate

            // pool blocks are service block so all service block signed by leader node
            stkNext.InitializeBlock(lastStk, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            return stkNext;
        }

        public static async Task<TransactionBlock> CNOUnStakeAsync(DagSystem sys, SendTransferBlock send)
        {
            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var sendPrev = await sys.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock;
            var stkId = send.Tags["stkid"];
            var lastStk = await sys.Storage.FindLatestBlockAsync(stkId) as StakingBlock;

            var stkNext = new UnStakingBlock
            {
                Height = lastStk.Height + 1,
                Name = lastStk.Name,
                OwnerAccountId = lastStk.OwnerAccountId,
                AccountID = lastStk.AccountID,
                Balances = new Dictionary<string, long>(),
                PreviousHash = lastStk.Hash,
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,
                DestinationAccountId = send.AccountID,

                // pool specified config
                Voting = lastStk.Voting,
                RelatedTx = send.Hash
            };

            stkNext.Balances.Add(LyraGlobal.OFFICIALTICKERCODE, 0);

            stkNext.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            // pool blocks are service block so all service block signed by leader node
            stkNext.InitializeBlock(lastStk, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            return stkNext;
        }

        // merchants
        public static async Task<TransactionBlock> CNOMCTCreateAsync(DagSystem sys, SendTransferBlock send)
        {
            throw new NotImplementedException();
        }
        public static async Task<TransactionBlock> CNOMCTPayAsync(DagSystem sys, SendTransferBlock send)
        {
            throw new NotImplementedException();
        }
        public static async Task<TransactionBlock> CNOMCTUnPayAsync(DagSystem sys, SendTransferBlock send)
        {
            throw new NotImplementedException();
        }
        public static async Task<TransactionBlock> CNOMCTConfirmPayAsync(DagSystem sys, SendTransferBlock send)
        {
            throw new NotImplementedException();
        }
        public static async Task<TransactionBlock> CNOMCTGetPayAsync(DagSystem sys, SendTransferBlock send)
        {
            throw new NotImplementedException();
        }
    }
}
