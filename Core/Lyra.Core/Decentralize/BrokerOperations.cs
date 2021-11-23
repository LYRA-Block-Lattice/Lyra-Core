using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.API;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using Lyra.Data.Utils;
using Lyra.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize
{
    public class BrokerOperations
    {
        // every method must check if the operation has been done.
        // if has been done, return null.
        public static async Task<ReceiveTransferBlock> ReceivePoolFactoryFeeAsync(DagSystem sys, SendTransferBlock sendBlock)
        {
            // check exists
            var recv = await sys.Storage.FindBlockBySourceHashAsync(sendBlock.Hash);
            if (recv != null)
                return null;

            TransactionBlock prevSend = await sys.Storage.FindBlockByHashAsync(sendBlock.PreviousHash) as TransactionBlock;
            var txInfo = sendBlock.GetBalanceChanges(prevSend);

            var lsb = await sys.Storage.GetLastServiceBlockAsync();
            var receiveBlock = new ReceiveAsFeeBlock
            {
                AccountID = sendBlock.DestinationAccountId,
                VoteFor = null,
                ServiceHash = lsb.Hash,
                SourceHash = sendBlock.Hash,
                Balances = new Dictionary<string, long>(),
                Fee = Math.Round(txInfo.Changes[LyraGlobal.OFFICIALTICKERCODE], 8, MidpointRounding.ToZero),
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.FullFee,
            };

            receiveBlock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            TransactionBlock latestPoolBlock = await sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;

            // ignore any token but LYR. keep the block clean.
            //if (!txInfo.Changes.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
            //    return;

            var latestBalances = latestPoolBlock.Balances.ToDecimalDict();
            var recvBalances = latestPoolBlock.Balances.ToDecimalDict();
            //foreach (var chg in txInfo.Changes)
            //{
            //    if (chg.Key != LyraGlobal.OFFICIALTICKERCODE)
            //        continue;

            //    if (recvBalances.ContainsKey(chg.Key))
            //        recvBalances[chg.Key] += chg.Value;
            //    else
            //        recvBalances.Add(chg.Key, chg.Value);
            //}

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

            poolGenesis.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            // pool blocks are service block so all service block signed by leader node
            poolGenesis.InitializeBlock(null, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            return poolGenesis;
            //await QueueTxActionBlockAsync(poolGenesis);
        }

        public static async Task<TransactionBlock> AddPoolLiquidateAsync(DagSystem sys, SendTransferBlock sendBlock)
        {
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

            depositBlock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

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
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);
            if (blocks.Any(a => a is PoolWithdrawBlock))
                return null;

            var lsb = await sys.Storage.GetLastServiceBlockAsync();
            var recvBlock = await sys.Storage.FindBlockBySourceHashAsync(send.Hash);

            var poolGenesis = sys.Storage.GetPoolByID(send.Tags["poolid"]);
            var poolId = poolGenesis.AccountID;

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

            swapInBlock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

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
        }

        public static async Task<TransactionBlock> SendPoolSwapOutTokenAsync(DagSystem sys, string reqHash)
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
            var swapOutBlock = new PoolSwapOutBlock()
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

            swapOutBlock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

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

            return swapOutBlock;
        }

        public static async Task<TransactionBlock> CNOCreateProfitingAccountAsync(DagSystem sys, SendTransferBlock send)
        {
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);
            var pgen = blocks.FirstOrDefault(a => a is ProfitingGenesis);
            if (pgen != null)
                return null;

            var sb = await sys.Storage.GetLastServiceBlockAsync();

            // create a semi random account for pool.
            // it can be verified by other nodes.
            decimal shareRito = decimal.Parse(send.Tags["share"]);
            var keyStr = $"{send.Hash.Substring(0, 16)},{send.Tags["ptype"]},{shareRito.ToBalanceLong()},{send.Tags["seats"]},{send.AccountID}";
            var AccountId = Base58Encoding.EncodeAccountId(Encoding.ASCII.GetBytes(keyStr).Take(64).ToArray());

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

            // pool blocks are service block so all service block signed by leader node
            pftGenesis.InitializeBlock(null, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);

            return pftGenesis;
        }

        public static async Task<TransactionBlock> SyncNodeFeesAsync(DagSystem sys, SendTransferBlock send)
        {
            var nodeid = send.AccountID;

            // must be first profiting account of nodes'
            var pfts = await sys.Storage.FindAllProfitingAccountForOwnerAsync(nodeid);
            var pft = pfts.First();

            var usf = await sys.Storage.FindUnsettledFeesAsync(nodeid, pft.AccountID);
            if (usf == null)
                return null;

            var feesEndSb = await sys.Storage.FindServiceBlockByIndexAsync(usf.ServiceBlockEndHeight);

            TransactionBlock latestBlock = await sys.Storage.FindLatestBlockAsync(pft.AccountID) as TransactionBlock;
            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var receiveBlock = new ReceiveNodeProfitBlock
            {
                AccountID = pft.AccountID,
                ServiceHash = sb.Hash,
                //SourceHash = feesEndSb.Hash,      // no source like all genesis. set source to svc block vaoliate the rule.
                ServiceBlockStartHeight = usf.ServiceBlockStartHeight,
                ServiceBlockEndHeight = usf.ServiceBlockEndHeight,
                Balances = latestBlock.Balances.ToDictionary(entry => entry.Key,
                                           entry => entry.Value),
                Fee = 0,
                FeeType = AuthorizationFeeTypes.NoFee,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                NonFungibleToken = null,

                // profit specified config
                Name = ((IBrokerAccount)latestBlock).Name,
                OwnerAccountId = ((IBrokerAccount)latestBlock).OwnerAccountId,
                PType = ((IProfiting)latestBlock).PType,
                ShareRito = ((IProfiting)latestBlock).ShareRito,
                Seats = ((IProfiting)latestBlock).Seats,
                RelatedTx = send.Hash
            };

            receiveBlock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            if (latestBlock.Balances.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
            {
                receiveBlock.Balances[LyraGlobal.OFFICIALTICKERCODE] += usf.TotalFees.ToBalanceLong();
            }
            else
            {
                receiveBlock.Balances.Add(LyraGlobal.OFFICIALTICKERCODE, usf.TotalFees.ToBalanceLong());
            }

            receiveBlock.InitializeBlock(latestBlock, sys.PosWallet.PrivateKey, AccountId: sys.PosWallet.AccountId);

            return receiveBlock;
        }

        // like wallet.receive
        public static async Task<TransactionBlock> CNOReceiveAllProfitAsync(DagSystem sys, SendTransferBlock reqSend)
        {
            // if is current authorizers, sync fee first
            // add check to save resources
            var pftid = reqSend.Tags["pftid"];
            var pftgen = await sys.Storage.FindFirstBlockAsync(pftid) as ProfitingGenesis;
            if (pftgen.OwnerAccountId != reqSend.AccountID)
                return null;

            if(pftgen.PType == ProfitingType.Node)
            {
                var feeBlk = await SyncNodeFeesAsync(sys, reqSend);
                if (feeBlk != null)
                    return feeBlk;
            }

            var transfer_info = await GetSendToPftAsync(sys, pftid);

            if (transfer_info.Successful())
            {
                var receiveBlock = await CNOReceiveProfitAsync(sys, reqSend.Hash, pftid, transfer_info);

                return receiveBlock;        // because we do it one block a time
            }
            else
                return null;        // the check
        }

        private static async Task<NewTransferAPIResult2> GetSendToPftAsync(DagSystem sys, string pftid)
        {
            NewTransferAPIResult2 transfer_info = new NewTransferAPIResult2();
            SendTransferBlock sendBlock = await sys.Storage.FindUnsettledSendBlockAsync(pftid);

            if (sendBlock != null)
            {
                TransactionBlock previousBlock = await sys.Storage.FindBlockByHashAsync(sendBlock.PreviousHash) as TransactionBlock;
                if (previousBlock == null)
                    transfer_info.ResultCode = APIResultCodes.CouldNotTraceSendBlockChain;
                else
                {
                    transfer_info.Transfer = sendBlock.GetBalanceChanges(previousBlock); //CalculateTransaction(sendBlock, previousSendBlock);
                    transfer_info.SourceHash = sendBlock.Hash;
                    transfer_info.NonFungibleToken = sendBlock.NonFungibleToken;
                    transfer_info.ResultCode = APIResultCodes.Success;
                }
            }
            else
                transfer_info.ResultCode = APIResultCodes.NoNewTransferFound;
            return transfer_info;
        }

        private static async Task<TransactionBlock> CNOReceiveProfitAsync(DagSystem sys, string relatedTx, string pftid, NewTransferAPIResult2 transInfo)
        {
            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var lastPft = await sys.Storage.FindLatestBlockAsync(pftid) as TransactionBlock;

            var pftNext = new ProfitingBlock
            {
                AccountID = lastPft.AccountID,
                Balances = new Dictionary<string, long>(),
                PreviousHash = lastPft.Hash,
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,
                SourceHash = transInfo.SourceHash,

                // profit specified config
                Name = ((IBrokerAccount)lastPft).Name,
                OwnerAccountId = ((IBrokerAccount)lastPft).OwnerAccountId,
                PType = ((IProfiting)lastPft).PType,
                ShareRito = ((IProfiting)lastPft).ShareRito,
                Seats = ((IProfiting)lastPft).Seats,
                RelatedTx = relatedTx
            };

            var recvBalances = lastPft.Balances.ToDecimalDict();
            foreach (var chg in transInfo.Transfer.Changes)
            {
                if (recvBalances.ContainsKey(chg.Key))
                    recvBalances[chg.Key] += chg.Value;
                else
                    recvBalances.Add(chg.Key, chg.Value);
            }

            pftNext.Balances = recvBalances.ToLongDict();

            pftNext.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            // pool blocks are service block so all service block signed by leader node
            pftNext.InitializeBlock(lastPft, sys.PosWallet.PrivateKey, AccountId: sys.PosWallet.AccountId);

            return pftNext;
        }

        public static async Task<TransactionBlock> CNORedistributeProfitAsync(DagSystem sys, string reqHash)
        {
            var reqBlock = await sys.Storage.FindBlockByHashAsync(reqHash);
            var pftid = reqBlock.Tags["pftid"];
            // create [multiple] send based on the staking
            // TODO: get staking by time receiving.
            // TODO: support bulk receive and single send
            // get stakings
            var lastBlock = await sys.Storage.FindLatestBlockAsync(pftid) as IProfiting;
            var stakers = sys.Storage.FindAllStakings(pftid, reqBlock.TimeStamp);
            var targets = stakers.Take(lastBlock.Seats);
            var relatedTxs = (await sys.Storage.FindBlocksByRelatedTxAsync(reqHash))
                .Cast<TransactionBlock>()
                .OrderBy(a => a.TimeStamp).ToList();
            if(relatedTxs.Count == 0)
            {
                // no balance
                return null;
            }
            // be carefull a profiting account may have no stakers.
            var totalStakingAmount = stakers.Sum(a => a.Amount);

            var allSends = new List<TransactionBlock>();
            var sentBlocks = relatedTxs.Where(a => a is BenefitingBlock)
                .Cast<BenefitingBlock>()
                .OrderBy(a => a.Height)
                .ToList();

            var lastProfitingBlock = relatedTxs.Where(a => a is ProfitingBlock)
                .OrderBy(a => a.TimeStamp)
                .Last() as TransactionBlock;
            var profitToDistribute = lastProfitingBlock.Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal() * lastBlock.ShareRito;

            // don't distribute < 1LYR
            if (profitToDistribute >= 1 && totalStakingAmount > 0)
            {
                // create a dictionary to hold amounts to send
                // staking account -> amount
                var sendAmounts = new Dictionary<string, decimal>();
                foreach (var target in targets)
                {
                    var amount = Math.Round(profitToDistribute * (target.Amount / totalStakingAmount), 8, MidpointRounding.ToZero);
                    if(amount > 0.00000001m)
                        sendAmounts.Add(target.StkAccount, amount);
                }

                foreach (var target in targets)
                {
                    var stkSend = sentBlocks.FirstOrDefault(a => a.StakingAccountId == target.StkAccount);
                    if (stkSend != null)
                        continue;

                    if (!sendAmounts.ContainsKey(target.StkAccount))
                        continue;

                    var amount = sendAmounts[target.StkAccount];
                    var sb = await sys.Storage.GetLastServiceBlockAsync();
                    var lastblkx = await sys.Storage.FindLatestBlockAsync(pftid);
                    var pftSend = CreateBenefiting(lastblkx as TransactionBlock, sb,
                        target, reqHash,
                        amount);

                    return pftSend;
                }

                // then create compound staking
                foreach (var target in targets)
                {
                    if(target.CompoundMode)
                    {
                        //var stkSend = sentBlocks.FirstOrDefault(a => a.StakingAccountId == target.StkAccount);
                        //if (stkSend == null)
                        //    continue;

                        //if (relatedTxs.Any(a => a is StakingBlock stk && stk.SourceHash == stkSend.Hash))
                        //    continue;

                        // look for any unsettled receive
                        SendTransferBlock xsend = await NodeService.Dag.Storage.FindUnsettledSendBlockAsync(target.StkAccount);
                        if (xsend == null)
                            continue;

                        var compstk = await CNOAddStakingAsync(sys, xsend);
                        if (compstk != null)
                            return compstk;
                    }
                }
            }
                        
            // if share 100%, no need to send
            var pftgen = await sys.Storage.FindFirstBlockAsync(pftid) as ProfitingGenesis;
            if (pftgen.ShareRito == 1m)
                return null;

            // all remaining send to the owner
            if (sentBlocks.Any(a => a.DestinationAccountId == lastBlock.OwnerAccountId && a.StakingAccountId == null))
                return null;

            var sb2 = await sys.Storage.GetLastServiceBlockAsync();
            var lastblk = await sys.Storage.FindLatestBlockAsync(pftid) as TransactionBlock;
            var ownrSend = CreateBenefiting(lastblk, sb2, new Staker { OwnerAccount = lastBlock.OwnerAccountId }, reqHash, lastblk.Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal());

            return ownrSend;
        }

        private static BenefitingBlock CreateBenefiting(TransactionBlock lastPft, ServiceBlock sb,
            Staker target, string relatedTx,
            decimal amount
            )
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
                DestinationAccountId = target.CompoundMode ? target.StkAccount : target.OwnerAccount,

                // profit specified config
                PType = ((IProfiting)lastPft).PType,
                ShareRito = ((IProfiting)lastPft).ShareRito,
                Seats = ((IProfiting)lastPft).Seats,
                RelatedTx = relatedTx,
                StakingAccountId = target.StkAccount
            };

            //TODO: think about multiple token

            var lastBalance = lastPft.Balances[LyraGlobal.OFFICIALTICKERCODE];
            lastBalance -= amount.ToBalanceLong();
            pftSend.Balances.Add(LyraGlobal.OFFICIALTICKERCODE, lastBalance);

            pftSend.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            // pool blocks are service block so all service block signed by leader node
            pftSend.InitializeBlock(lastPft, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);

            Console.WriteLine($"Benefiting {pftSend.DestinationAccountId.Shorten()} Index {pftSend.Height} who is staking {target.Amount} {amount} LYR hash: {pftSend.Hash} signed by {NodeService.Dag.PosWallet.AccountId.Shorten()}");

            return pftSend;
        }

        public static async Task<TransactionBlock> CNOCreateStakingAccountAsync(DagSystem sys, SendTransferBlock send)
        {
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);
            var pgen = blocks.FirstOrDefault(a => a is StakingGenesis);
            if (pgen != null)
                return null;

            var sb = await sys.Storage.GetLastServiceBlockAsync();

            // create a semi random account for pool.
            // it can be verified by other nodes.
            var keyStr = $"{send.Hash.Substring(0, 16)},{send.Tags["voting"]},{send.AccountID}";
            var AccountId = Base58Encoding.EncodeAccountId(Encoding.ASCII.GetBytes(keyStr).Take(64).ToArray());

            var start = DateTime.UtcNow.AddDays(1);
            if (LyraNodeConfig.GetNetworkId() == "devnet")
                start = DateTime.UtcNow;        // for debug

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
                Days = int.Parse(send.Tags["days"]),
                Start = start,
                CompoundMode = send.Tags["compound"] == "True"
            };

            stkGenesis.Balances.Add(LyraGlobal.OFFICIALTICKERCODE, 0);

            stkGenesis.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            // pool blocks are service block so all service block signed by leader node
            stkGenesis.InitializeBlock(null, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);

            return stkGenesis;
        }

        public static async Task<TransactionBlock> CNOAddStakingAsync(DagSystem sys, SendTransferBlock send)
        {
            var block = await sys.Storage.FindBlockBySourceHashAsync(send.Hash);
            if (block != null)
                return null;

            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var sendPrev = await sys.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock;
            var lastBlock = await sys.Storage.FindLatestBlockAsync(send.DestinationAccountId);
            var lastStk = lastBlock as TransactionBlock;

            string relatedTx = send.Hash;
            DateTime start;
            if (send is BenefitingBlock bnb)
            {
                start = (lastStk as IStaking).Start;
            }
            else
            {
                start = send.TimeStamp.AddDays(1);         // manual add staking, start after 1 day.

                if (LyraNodeConfig.GetNetworkId() == "devnet")
                    start = send.TimeStamp;        // for debug
            }

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
                Days = (lastBlock as IStaking).Days,
                Voting = ((IStaking)lastStk).Voting,
                RelatedTx = relatedTx,
                Start = start,
                CompoundMode = ((IStaking)lastStk).CompoundMode
            };

            var chgs = send.GetBalanceChanges(sendPrev);
            stkNext.Balances.Add(LyraGlobal.OFFICIALTICKERCODE, lastStk.Balances[LyraGlobal.OFFICIALTICKERCODE] + chgs.Changes[LyraGlobal.OFFICIALTICKERCODE].ToBalanceLong());

            stkNext.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            // pool blocks are service block so all service block signed by leader node
            stkNext.InitializeBlock(lastStk, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);

            return stkNext;
        }

        public static async Task<TransactionBlock> CNOUnStakeAsync(DagSystem sys, SendTransferBlock send)
        {
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);
            if (blocks.Any(a => a is UnStakingBlock))
                return null;

            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var sendPrev = await sys.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock;
            var stkId = send.Tags["stkid"];
            var lastStk = await sys.Storage.FindLatestBlockAsync(stkId) as TransactionBlock;

            var stkNext = new UnStakingBlock
            {
                Height = lastStk.Height + 1,
                Name = (lastStk as IBrokerAccount).Name,
                OwnerAccountId = (lastStk as IBrokerAccount).OwnerAccountId,
                AccountID = lastStk.AccountID,
                Balances = new Dictionary<string, long>(),
                PreviousHash = lastStk.Hash,
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.Dynamic,
                DestinationAccountId = send.AccountID,

                // pool specified config
                Days = (lastStk as IStaking).Days,
                Voting = (lastStk as IStaking).Voting,
                RelatedTx = send.Hash,
                Start = DateTime.MaxValue,
                CompoundMode = ((IStaking)lastStk).CompoundMode
            };

            if(((IStaking)lastStk).Start.AddDays(((IStaking)lastStk).Days) > DateTime.UtcNow)
            {
                stkNext.Fee = Math.Round(0.008m * lastStk.Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal(), 8, MidpointRounding.ToZero);
            }

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
