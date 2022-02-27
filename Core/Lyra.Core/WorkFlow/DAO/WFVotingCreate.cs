using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.WorkFlow.DAO
{
    [LyraWorkFlow]
    public class WFVotingCreate : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_VOT_CREATE,
                RecvVia = BrokerRecvType.DaoRecv,
                Steps = new[] { CreateGenesisAsync }
            };
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock send, TransactionBlock last)
        {
            if (send.Tags.Count != 2 ||
                !send.Tags.ContainsKey("data") ||
                string.IsNullOrWhiteSpace(send.Tags["data"]))
                return APIResultCodes.InvalidBlockTags;

            var subject = JsonConvert.DeserializeObject<VotingSubject>(send.Tags["data"]);
            // verify subject
            if (string.IsNullOrEmpty(subject.DaoId) ||
                string.IsNullOrEmpty(subject.Issuer) ||
                string.IsNullOrEmpty(subject.Title) ||
                string.IsNullOrEmpty(subject.Description) ||
                subject.Options == null ||
                subject.Options.Length < 2 ||
                subject.TimeSpan < 1 ||
                subject.Type == SubjectType.None
                )
                return APIResultCodes.InvalidArgument;

            // issuer should be the owner of DAO
            var dao = await sys.Storage.FindLatestBlockAsync(subject.DaoId) as IDao;
            if (dao == null || dao.OwnerAccountId != subject.Issuer)
                return APIResultCodes.InvalidDAO;

            // title can't repeat
            var votes = await sys.Storage.FindAllVotesByDaoAsync(subject.DaoId, false);
            if (votes.Any(a => a is VotingGenesisBlock vg && vg.Subject.Title == subject.Title))
                return APIResultCodes.InvalidArgument;

            // options can't repeat
            if (subject.Options.Length != subject.Options.Distinct().Count())
                return APIResultCodes.InvalidArgument;

            // must has enough voter
            if(dao.Treasure.Count() < 2)
                return APIResultCodes.InvalidArgument;

            return APIResultCodes.Success;
        }

        async Task<TransactionBlock> CreateGenesisAsync(DagSystem sys, SendTransferBlock send)
        {
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);

            var subject = JsonConvert.DeserializeObject<VotingSubject>(send.Tags["data"]);

            var keyStr = $"{send.Hash.Substring(0, 16)},{subject.Title},{send.AccountID}";
            var AccountId = Base58Encoding.EncodeAccountId(Encoding.ASCII.GetBytes(keyStr).Take(64).ToArray());

            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var gens = new VotingGenesisBlock
            {
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,

                // transaction
                AccountType = AccountTypes.Voting,
                AccountID = AccountId,
                Balances = new Dictionary<string, long>(),

                // recv
                SourceHash = null,

                // broker
                Name = "no name",
                OwnerAccountId = send.AccountID,
                RelatedTx = send.Hash,

                // voting
                VoteState = VoteStatus.InProgress,
                Subject  = subject,
            };

            gens.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            // pool blocks are service block so all service block signed by leader node
            gens.InitializeBlock(null, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            return gens;
        }

    }
}
