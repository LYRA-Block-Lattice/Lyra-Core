using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.API;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using Lyra.Data.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize.WorkFlow
{
    public class WFStakingCreate : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_STK_CRSTK,
                RecvVia = BrokerRecvType.PFRecv,
                Blocks = new[] {
                    new BlockDesc
                    {
                        BlockType = BlockTypes.StakingGenesis,
                        TheBlock = typeof(StakingGenesis),
                        AuthorizerName = "StakingGenesisAuthorizer",
                    }
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
                default:
                    return APIResultCodes.InvalidServiceRequest;
            }
            return APIResultCodes.Success;
        }

        public override async Task<TransactionBlock> BrokerOpsAsync(DagSystem sys, SendTransferBlock send)
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
    }
    public class WFStakingAddStaking : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_STK_ADDSTK,
                RecvVia = BrokerRecvType.None,
                Blocks = new[] {
                    new BlockDesc
                    {
                        BlockType = BlockTypes.Staking,
                        TheBlock = typeof(StakingBlock),
                        AuthorizerName = "StakingAuthorizer",
                    }
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
                default:
                    return APIResultCodes.InvalidServiceRequest;
            }
            return APIResultCodes.Success;
        }

        public override async Task<TransactionBlock> BrokerOpsAsync(DagSystem sys, SendTransferBlock send)
        {
            return await CNOAddStakingImplAsync(sys, send, send.Hash);
        }
        public static async Task<TransactionBlock> CNOAddStakingImplAsync(DagSystem sys, SendTransferBlock send, string relatedTx)
        {
            var block = await sys.Storage.FindBlockBySourceHashAsync(send.Hash);
            if (block != null)
                return null;

            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var sendPrev = await sys.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock;
            var lastBlock = await sys.Storage.FindLatestBlockAsync(send.DestinationAccountId);
            var lastStk = lastBlock as TransactionBlock;

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
    }
    public class WFStakingUnStaking : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_STK_UNSTK,
                RecvVia = BrokerRecvType.PFRecv,
                Blocks = new[] {
                    new BlockDesc
                    {
                        BlockType = BlockTypes.UnStaking,
                        TheBlock = typeof(UnStakingBlock),
                        AuthorizerName = "UnStakingAuthorizer",
                    }
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
                default:
                    return APIResultCodes.InvalidServiceRequest;
            }
            return APIResultCodes.Success;
        }

        public override async Task<TransactionBlock> BrokerOpsAsync(DagSystem sys, SendTransferBlock send)
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

            if (((IStaking)lastStk).Start.AddDays(((IStaking)lastStk).Days) > DateTime.UtcNow)
            {
                stkNext.Fee = Math.Round(0.008m * lastStk.Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal(), 8, MidpointRounding.ToZero);
            }

            stkNext.Balances.Add(LyraGlobal.OFFICIALTICKERCODE, 0);

            stkNext.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            // pool blocks are service block so all service block signed by leader node
            stkNext.InitializeBlock(lastStk, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);

            return stkNext;
        }
    }
}
