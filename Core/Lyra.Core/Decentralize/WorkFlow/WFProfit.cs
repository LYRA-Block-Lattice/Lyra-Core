using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.API;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using Lyra.Data.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize.WorkFlow
{
    [LyraWorkFlow]
    public class WFProfitCreate : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_PFT_CRPFT,
                RecvVia = BrokerRecvType.PFRecv,
                Blocks = new[] {
                    new BlockDesc
                    {
                        BlockType = BlockTypes.ProfitingGenesis,
                        TheBlock = typeof(ProfitingGenesis),
                        AuthorizerName = "ProfitingGenesisAuthorizer",
                    }
                }
            };
        }

        #region BRK_PFT_CRPFT
        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock block, TransactionBlock lastBlock)
        {
            var chgs = block.GetBalanceChanges(lastBlock);
            if (!chgs.Changes.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
                return APIResultCodes.InvalidFeeAmount;

            switch (block.Tags[Block.REQSERVICETAG])
            {
                case BrokerActions.BRK_STK_ADDSTK:
                    if (block.Tags.Count == 1)
                    {
                        // verify sender is the owner of stkingblock
                        var stks = await sys.Storage.FindAllStakingAccountForOwnerAsync(block.AccountID);
                        if (!stks.Any(a => a.AccountID == block.DestinationAccountId))
                            return APIResultCodes.InvalidStakingAccount;
                    }
                    else
                        return APIResultCodes.InvalidBlockTags;
                    break;
                case BrokerActions.BRK_STK_UNSTK:
                    if (
                        block.Tags.ContainsKey("stkid") && !string.IsNullOrWhiteSpace(block.Tags["stkid"])
                        && block.Tags.Count == 2
                        )
                    {
                        if (block.DestinationAccountId != PoolFactoryBlock.FactoryAccount)
                            return APIResultCodes.InvalidServiceRequest;

                        // verify sender is the owner of stkingblock
                        var stks = await sys.Storage.FindAllStakingAccountForOwnerAsync(block.AccountID);
                        if (!stks.Any(a => a.AccountID == block.Tags["stkid"]))
                            return APIResultCodes.InvalidStakingAccount;

                        var lastStk = await sys.Storage.FindLatestBlockAsync(block.Tags["stkid"]) as TransactionBlock;
                        if (lastStk == null)
                            return APIResultCodes.InvalidUnstaking;

                        if (!lastStk.Balances.ContainsKey(LyraGlobal.OFFICIALTICKERCODE) || lastStk.Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal() == 0)
                            return APIResultCodes.InvalidUnstaking;
                    }
                    else
                        return APIResultCodes.InvalidBlockTags;
                    break;
                case BrokerActions.BRK_STK_CRSTK:   // create staking
                    if (chgs.Changes[LyraGlobal.OFFICIALTICKERCODE] != PoolFactoryBlock.StakingAccountCreateFee)
                        return APIResultCodes.InvalidFeeAmount;

                    string votefor;
                    int days;
                    if (
                        block.Tags.ContainsKey("name") && !string.IsNullOrWhiteSpace(block.Tags["name"]) &&
                        block.Tags.ContainsKey("days") && int.TryParse(block.Tags["days"], out days) && days >= 3 &&
                        block.Tags.ContainsKey("voting") && !string.IsNullOrEmpty(block.Tags["voting"]) &&
                        block.Tags.ContainsKey("compound") && !string.IsNullOrEmpty(block.Tags["compound"]) &&
                        block.Tags.Count == 5
                        )
                    {
                        var stks = await sys.Storage.FindAllStakingAccountForOwnerAsync(block.AccountID);
                        if (stks.Any(a => a.Name == block.Tags["name"]))
                            return APIResultCodes.DuplicateName;

                        votefor = block.Tags["voting"];
                        if (!Signatures.ValidateAccountId(votefor))
                        {
                            return APIResultCodes.InvalidProfitingAccount;
                        }
                        var pftgen = await sys.Storage.FindFirstBlockAsync(votefor) as ProfitingGenesis;
                        if (pftgen == null || pftgen.AccountType != AccountTypes.Profiting)
                        {
                            return APIResultCodes.InvalidProfitingAccount;
                        }
                        if (pftgen.Seats == 0 || pftgen.ShareRito == 0)
                        {
                            return APIResultCodes.ProfitUnavaliable;
                        }
                        if (days <= 1)
                        {
                            return APIResultCodes.VotingDaysTooSmall;
                        }
                        if (block.Tags["compound"] != "True" && block.Tags["compound"] != "False")
                            return APIResultCodes.InvalidBlockTags;
                    }
                    else
                        return APIResultCodes.InvalidBlockTags;
                    break;
                case BrokerActions.BRK_PFT_CRPFT:   // create profiting
                    if (chgs.Changes[LyraGlobal.OFFICIALTICKERCODE] != PoolFactoryBlock.ProfitingAccountCreateFee)
                        return APIResultCodes.InvalidFeeAmount;

                    ProfitingType ptype;
                    decimal shareRito;
                    int seats;
                    if (
                        block.Tags.ContainsKey("name") && !string.IsNullOrWhiteSpace(block.Tags["name"]) &&
                        block.Tags.ContainsKey("ptype") && Enum.TryParse(block.Tags["ptype"], false, out ptype)
                        && block.Tags.ContainsKey("share") && decimal.TryParse(block.Tags["share"], out shareRito)
                        && block.Tags.ContainsKey("seats") && int.TryParse(block.Tags["seats"], out seats)
                        && block.Tags.Count == 5
                        )
                    {
                        if (shareRito >= 0m && shareRito <= 1m && seats >= 0 && seats <= 100)
                        {
                            // name dup check
                            var pfts = await sys.Storage.FindAllProfitingAccountForOwnerAsync(block.AccountID);
                            if (pfts.Any(a => a.Name == block.Tags["name"]))
                                return APIResultCodes.DuplicateName;

                            // one type per account. just keep it simple.
                            if (pfts.Any(a => a.PType == ptype))
                                return APIResultCodes.DuplicateAccountType;

                            if (shareRito == 0 && seats != 0)
                                return APIResultCodes.InvalidAuthorizerCount;

                            if (shareRito > 0 && seats == 0)
                                return APIResultCodes.InvalidAuthorizerCount;

                            var dupname = sys.Storage.FindProfitingAccountsByName(block.Tags["name"]);
                            if (dupname != null)
                                return APIResultCodes.DuplicateName;
                        }
                        else
                        {
                            return APIResultCodes.InvalidShareOfProfit;
                        }
                    }
                    else
                        return APIResultCodes.InvalidBlockTags;
                    break;
                //case BrokerActions.BRK_PFT_FEEPFT:  //TODO: add support
                //    var nodeid = block.Tags.ContainsKey("nodeid") ? block.Tags["nodeid"] : null;
                //    if (nodeid == null)
                //        return APIResultCodes.InvalidAccountId;

                //    var pfts2 = await sys.Storage.FindAllProfitingAccountForOwnerAsync(nodeid);
                //    if(pfts2.Count > 0)
                //    {
                //        var pftid2 = pfts2.First().AccountID;

                //        var pft2 = await sys.Storage.FindFirstBlockAsync(pftid2) as ProfitingGenesis;
                //        if (pft2 == null)
                //            return APIResultCodes.InvalidAccountId;

                //        var stkrs2 = sys.Storage.FindAllStakings(pftid2, DateTime.UtcNow);
                //        if (!stkrs2.Any(a => a.user == block.AccountID) && pft2.OwnerAccountId != block.AccountID)
                //            return APIResultCodes.RequestNotPermited;

                //    }

                //    return APIResultCodes.Success;
                case BrokerActions.BRK_PFT_GETPFT:
                    var pftid = block.Tags.ContainsKey("pftid") ? block.Tags["pftid"] : null;
                    if (pftid == null)
                        return APIResultCodes.InvalidAccountId;

                    var pft = await sys.Storage.FindFirstBlockAsync(pftid) as ProfitingGenesis;
                    if (pft == null)
                        return APIResultCodes.InvalidAccountId;

                    // check busy
                    //var brkacct = BrokerFactory.GetBrokerAccountID(block);
                    //if (brkacct != null)
                    //{
                    if (BrokerFactory.GetAllBlueprints().Any(a => a.brokerAccount == pftid))
                        return APIResultCodes.SystemBusy;
                    //}

                    var stkrs = sys.Storage.FindAllStakings(pftid, DateTime.UtcNow);
                    if (!stkrs.Any(a => a.OwnerAccount == block.AccountID) && pft.OwnerAccountId != block.AccountID)
                        return APIResultCodes.RequestNotPermited;
                    break;

                default:
                    return APIResultCodes.InvalidServiceRequest;
            }
            return APIResultCodes.Success;
        }
        public override async Task<TransactionBlock> BrokerOpsAsync(DagSystem sys, SendTransferBlock send)
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

        #endregion
    }

    [LyraWorkFlow]
    public class WFProfitGet : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_PFT_GETPFT,
                RecvVia = BrokerRecvType.PFRecv,
                Blocks = new[] {
                    new BlockDesc
                    {
                        BlockType = BlockTypes.Profiting,
                        TheBlock = typeof(ProfitingBlock),
                        AuthorizerName = "ProfitingAuthorizer",
                    },
                    new BlockDesc
                    {
                        BlockType = BlockTypes.Benefiting,
                        TheBlock = typeof(BenefitingBlock),
                        AuthorizerName = "BenefitingAuthorizer",
                    },
                    new BlockDesc
                    {
                        BlockType = BlockTypes.ReceiveNodeProfit,
                        TheBlock = typeof(ReceiveNodeProfitBlock),
                        AuthorizerName = "ReceiveNodeProfitAuthorizer",
                    },
                }
            };
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock block, TransactionBlock lastBlock)
        {
            var chgs = block.GetBalanceChanges(lastBlock);
            if (!chgs.Changes.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
                return APIResultCodes.InvalidFeeAmount;

            switch (block.Tags[Block.REQSERVICETAG])
            {
                case BrokerActions.BRK_STK_ADDSTK:
                    if (block.Tags.Count == 1)
                    {
                        // verify sender is the owner of stkingblock
                        var stks = await sys.Storage.FindAllStakingAccountForOwnerAsync(block.AccountID);
                        if (!stks.Any(a => a.AccountID == block.DestinationAccountId))
                            return APIResultCodes.InvalidStakingAccount;
                    }
                    else
                        return APIResultCodes.InvalidBlockTags;
                    break;
                case BrokerActions.BRK_STK_UNSTK:
                    if (
                        block.Tags.ContainsKey("stkid") && !string.IsNullOrWhiteSpace(block.Tags["stkid"])
                        && block.Tags.Count == 2
                        )
                    {
                        if (block.DestinationAccountId != PoolFactoryBlock.FactoryAccount)
                            return APIResultCodes.InvalidServiceRequest;

                        // verify sender is the owner of stkingblock
                        var stks = await sys.Storage.FindAllStakingAccountForOwnerAsync(block.AccountID);
                        if (!stks.Any(a => a.AccountID == block.Tags["stkid"]))
                            return APIResultCodes.InvalidStakingAccount;

                        var lastStk = await sys.Storage.FindLatestBlockAsync(block.Tags["stkid"]) as TransactionBlock;
                        if (lastStk == null)
                            return APIResultCodes.InvalidUnstaking;

                        if (!lastStk.Balances.ContainsKey(LyraGlobal.OFFICIALTICKERCODE) || lastStk.Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal() == 0)
                            return APIResultCodes.InvalidUnstaking;
                    }
                    else
                        return APIResultCodes.InvalidBlockTags;
                    break;
                case BrokerActions.BRK_STK_CRSTK:   // create staking
                    if (chgs.Changes[LyraGlobal.OFFICIALTICKERCODE] != PoolFactoryBlock.StakingAccountCreateFee)
                        return APIResultCodes.InvalidFeeAmount;

                    string votefor;
                    int days;
                    if (
                        block.Tags.ContainsKey("name") && !string.IsNullOrWhiteSpace(block.Tags["name"]) &&
                        block.Tags.ContainsKey("days") && int.TryParse(block.Tags["days"], out days) && days >= 3 &&
                        block.Tags.ContainsKey("voting") && !string.IsNullOrEmpty(block.Tags["voting"]) &&
                        block.Tags.ContainsKey("compound") && !string.IsNullOrEmpty(block.Tags["compound"]) &&
                        block.Tags.Count == 5
                        )
                    {
                        var stks = await sys.Storage.FindAllStakingAccountForOwnerAsync(block.AccountID);
                        if (stks.Any(a => a.Name == block.Tags["name"]))
                            return APIResultCodes.DuplicateName;

                        votefor = block.Tags["voting"];
                        if (!Signatures.ValidateAccountId(votefor))
                        {
                            return APIResultCodes.InvalidProfitingAccount;
                        }
                        var pftgen = await sys.Storage.FindFirstBlockAsync(votefor) as ProfitingGenesis;
                        if (pftgen == null || pftgen.AccountType != AccountTypes.Profiting)
                        {
                            return APIResultCodes.InvalidProfitingAccount;
                        }
                        if (pftgen.Seats == 0 || pftgen.ShareRito == 0)
                        {
                            return APIResultCodes.ProfitUnavaliable;
                        }
                        if (days <= 1)
                        {
                            return APIResultCodes.VotingDaysTooSmall;
                        }
                        if (block.Tags["compound"] != "True" && block.Tags["compound"] != "False")
                            return APIResultCodes.InvalidBlockTags;
                    }
                    else
                        return APIResultCodes.InvalidBlockTags;
                    break;
                case BrokerActions.BRK_PFT_CRPFT:   // create profiting
                    if (chgs.Changes[LyraGlobal.OFFICIALTICKERCODE] != PoolFactoryBlock.ProfitingAccountCreateFee)
                        return APIResultCodes.InvalidFeeAmount;

                    ProfitingType ptype;
                    decimal shareRito;
                    int seats;
                    if (
                        block.Tags.ContainsKey("name") && !string.IsNullOrWhiteSpace(block.Tags["name"]) &&
                        block.Tags.ContainsKey("ptype") && Enum.TryParse(block.Tags["ptype"], false, out ptype)
                        && block.Tags.ContainsKey("share") && decimal.TryParse(block.Tags["share"], out shareRito)
                        && block.Tags.ContainsKey("seats") && int.TryParse(block.Tags["seats"], out seats)
                        && block.Tags.Count == 5
                        )
                    {
                        if (shareRito >= 0m && shareRito <= 1m && seats >= 0 && seats <= 100)
                        {
                            // name dup check
                            var pfts = await sys.Storage.FindAllProfitingAccountForOwnerAsync(block.AccountID);
                            if (pfts.Any(a => a.Name == block.Tags["name"]))
                                return APIResultCodes.DuplicateName;

                            // one type per account. just keep it simple.
                            if (pfts.Any(a => a.PType == ptype))
                                return APIResultCodes.DuplicateAccountType;

                            if (shareRito == 0 && seats != 0)
                                return APIResultCodes.InvalidAuthorizerCount;

                            if (shareRito > 0 && seats == 0)
                                return APIResultCodes.InvalidAuthorizerCount;

                            var dupname = sys.Storage.FindProfitingAccountsByName(block.Tags["name"]);
                            if (dupname != null)
                                return APIResultCodes.DuplicateName;
                        }
                        else
                        {
                            return APIResultCodes.InvalidShareOfProfit;
                        }
                    }
                    else
                        return APIResultCodes.InvalidBlockTags;
                    break;
                //case BrokerActions.BRK_PFT_FEEPFT:  //TODO: add support
                //    var nodeid = block.Tags.ContainsKey("nodeid") ? block.Tags["nodeid"] : null;
                //    if (nodeid == null)
                //        return APIResultCodes.InvalidAccountId;

                //    var pfts2 = await sys.Storage.FindAllProfitingAccountForOwnerAsync(nodeid);
                //    if(pfts2.Count > 0)
                //    {
                //        var pftid2 = pfts2.First().AccountID;

                //        var pft2 = await sys.Storage.FindFirstBlockAsync(pftid2) as ProfitingGenesis;
                //        if (pft2 == null)
                //            return APIResultCodes.InvalidAccountId;

                //        var stkrs2 = sys.Storage.FindAllStakings(pftid2, DateTime.UtcNow);
                //        if (!stkrs2.Any(a => a.user == block.AccountID) && pft2.OwnerAccountId != block.AccountID)
                //            return APIResultCodes.RequestNotPermited;

                //    }

                //    return APIResultCodes.Success;
                case BrokerActions.BRK_PFT_GETPFT:
                    var pftid = block.Tags.ContainsKey("pftid") ? block.Tags["pftid"] : null;
                    if (pftid == null)
                        return APIResultCodes.InvalidAccountId;

                    var pft = await sys.Storage.FindFirstBlockAsync(pftid) as ProfitingGenesis;
                    if (pft == null)
                        return APIResultCodes.InvalidAccountId;

                    // check busy
                    //var brkacct = BrokerFactory.GetBrokerAccountID(block);
                    //if (brkacct != null)
                    //{
                    if (BrokerFactory.GetAllBlueprints().Any(a => a.brokerAccount == pftid))
                        return APIResultCodes.SystemBusy;
                    //}

                    var stkrs = sys.Storage.FindAllStakings(pftid, DateTime.UtcNow);
                    if (!stkrs.Any(a => a.OwnerAccount == block.AccountID) && pft.OwnerAccountId != block.AccountID)
                        return APIResultCodes.RequestNotPermited;
                    break;

                default:
                    return APIResultCodes.InvalidServiceRequest;
            }
            return APIResultCodes.Success;
        }

        #region Get profit
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
        public override async Task<TransactionBlock> BrokerOpsAsync(DagSystem sys, SendTransferBlock reqSend)
        {
            // if is current authorizers, sync fee first
            // add check to save resources
            var pftid = reqSend.Tags["pftid"];
            var pftgen = await sys.Storage.FindFirstBlockAsync(pftid) as ProfitingGenesis;
            if (pftgen.OwnerAccountId != reqSend.AccountID)
                return null;

            if (pftgen.PType == ProfitingType.Node)
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

        public override async Task<TransactionBlock> ExtraOpsAsync(DagSystem sys, string reqHash)
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
            if (relatedTxs.Count == 0)
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
                    if (amount > 0.00000001m)
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
                    if (target.CompoundMode)
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

                        var compstk = await WFStakingAddStaking.CNOAddStakingImplAsync(sys, xsend, reqHash);
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

        #endregion
    }
}
