using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using Lyra.Data.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.WorkFlow.STK
{
    [LyraWorkFlow]//v
    public class WFStakingCreate : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_STK_CRSTK,
                RecvVia = BrokerRecvType.PFRecv,
            };
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, LyraContext context)
        {
            var block = context.Send;
            TransactionBlock lastBlock = await DagSystem.Singleton.Storage.FindBlockByHashAsync(block.PreviousHash) as TransactionBlock;
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

        public override async Task<TransactionBlock> BrokerOpsAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(context.Send.Hash);
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

            stkGenesis.AddTag(Block.MANAGEDTAG, WFState.Finished.ToString());

            // pool blocks are service block so all service block signed by leader node
            stkGenesis.InitializeBlock(null, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);

            return stkGenesis;
        }
    }
}
