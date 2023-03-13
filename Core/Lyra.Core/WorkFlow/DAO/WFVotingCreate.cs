using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.API.Identity;
using Lyra.Data.API.ODR;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.API.WorkFlow.UniMarket;
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
    [LyraWorkFlow]//v
    public class WFVotingCreate : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_VOT_CREATE,
                RecvVia = BrokerRecvType.DaoRecv,
                Steps = new[] { VoteGenesisAsync }
            };
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            if (send.Tags.Count != 4 ||
                !send.Tags.ContainsKey("data") ||
                string.IsNullOrWhiteSpace(send.Tags["data"]) ||
                !send.Tags.ContainsKey("pptype") ||
                string.IsNullOrWhiteSpace(send.Tags["pptype"]) ||
                !send.Tags.ContainsKey("ppdata") ||
                string.IsNullOrWhiteSpace(send.Tags["ppdata"])
                )
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

            if (subject.Title == null || subject.Title.Length > 100 ||
                subject.Description == null || subject.Description.Length > 300)
                return APIResultCodes.ArgumentOutOfRange;

            var pptype = (ProposalType)Enum.Parse(typeof(ProposalType), send.Tags["pptype"]);
            object? proposal = pptype switch
            {
                ProposalType.DisputeResolution => JsonConvert.DeserializeObject<ODRResolution>(send.Tags["ppdata"]),
                ProposalType.DAOSettingChanges => JsonConvert.DeserializeObject<DAOChange>(send.Tags["ppdata"]),
                _ => null
            };

            if (pptype == ProposalType.None || proposal == null)
                return APIResultCodes.InvalidArgument;

            // issuer should be the owner of DAO
            var dao = await sys.Storage.FindLatestBlockAsync(subject.DaoId) as IDao;
            if (dao == null || (dao as IBrokerAccount).OwnerAccountId != subject.Issuer)
                return APIResultCodes.Unauthorized;

            var resolution = proposal as ODRResolution;
            
            if(resolution != null)
            {
                // verify ODR resolution subject
                if (string.IsNullOrEmpty(resolution.TradeId) ||
                    string.IsNullOrEmpty(resolution.Creator) ||
                    resolution.Actions == null ||
                    resolution.Actions.Length < 1
                    )
                    return APIResultCodes.InvalidArgument;

                // trade's dao == subject' dao
                var tradeblk = await sys.Storage.FindLatestBlockAsync(resolution.TradeId) as IUniTrade;
                if(tradeblk == null)
                    return APIResultCodes.InvalidArgument;

                if(tradeblk.Trade.daoId != subject.DaoId)
                    return APIResultCodes.InvalidDAO;

                if (tradeblk.UTStatus != UniTradeStatus.Dispute)
                    return APIResultCodes.InvalidTradeStatus;

                // verify complain is from user. by signature.
                var dlr = sys.Storage.FindFirstBlock(tradeblk.Trade.dealerId) as IDealer;
                if (dlr == null)
                    return APIResultCodes.InvalidDealerServer;

                var uri = new Uri(new Uri(dlr.Endpoint), "/api/dealer/");
                var dealer = new DealerClient(uri);

                var lsb = sys.Storage.GetLastServiceBlock();
                var wallet = sys.PosWallet;
                var sign = Signatures.GetSignature(wallet.PrivateKey, lsb.Hash, wallet.AccountId);

                var ret = await dealer.GetTradeBriefAsync(resolution.TradeId, wallet.AccountId, sign);
                if (!ret.Successful())
                    return APIResultCodes.InvalidOperation;

                var brief = ret.Deserialize<TradeBrief>();
                if (brief == null)
                    return APIResultCodes.InvalidOperation;

                // there may be several cases
                var thecases = brief.GetDisputeHistory();
                foreach(var theCase in thecases)
                {
                    if (!resolution.ComplaintHashes.Contains(theCase.Complaint.Hash))
                        return APIResultCodes.DisputeCaseWasNotIncluded;
                }
                foreach(var hash in resolution.ComplaintHashes)
                {
                    if (!thecases.Any(a => a.Complaint.Hash == hash))
                        return APIResultCodes.DisputeCaseWasNotIncluded;
                }

                // there should be no pending or success resolutions.
                if(brief.Resolutions != null && brief.Resolutions.Any(a => a.Status == ResolutionStatus.Pending || a.Status == ResolutionStatus.Success))
                {
                    return APIResultCodes.ResolutionPending;
                }
                
                //var complaint = thecase.Complaint;
                //if ((complaint.ownerId == tradeblk.Trade.orderOwnerId && complaint.VerifySignature(tradeblk.Trade.orderOwnerId))
                //    || (complaint.ownerId == tradeblk.OwnerAccountId && complaint.VerifySignature(tradeblk.OwnerAccountId)))
                //{
                //    // seems OK
                //}
                //else
                //    return APIResultCodes.Unauthorized;
            }

            var daochg = proposal as DAOChange;
            if(daochg != null)
            {
                if (dao.OwnerAccountId != daochg.creator || daochg.creator != subject.Issuer)
                    return APIResultCodes.Unauthorized;

                var vrfychg = WFChangeDAO.VerifyDaoChanges(daochg);
                if (APIResultCodes.Success != vrfychg)
                    return vrfychg;
            }

            if (proposal != null && resolution == null && daochg == null)
                return APIResultCodes.Unsupported;

            // title can't repeat
            var votes = await sys.Storage.FindAllVotesByDaoAsync(subject.DaoId, false);
            if (votes.Any(a => a is VotingGenesisBlock vg && vg.Subject.Title == subject.Title))
                return APIResultCodes.InvalidArgument;

            // options can't repeat
            if (subject.Options.Length != subject.Options.Distinct().Count())
                return APIResultCodes.InvalidArgument;

            // must has enough voter
            if(dao.Treasure == null || dao.Treasure.Count < 1)
                return APIResultCodes.NotEnoughVoters;

            return APIResultCodes.Success;
        }

        async Task<TransactionBlock> VoteGenesisAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);

            var subject = JsonConvert.DeserializeObject<VotingSubject>(send.Tags["data"]);
            var pptype = (ProposalType)Enum.Parse(typeof(ProposalType), send.Tags["pptype"]);
            object? proposal = pptype switch
            {
                ProposalType.DisputeResolution => JsonConvert.DeserializeObject<ODRResolution>(send.Tags["ppdata"]),
                ProposalType.DAOSettingChanges => JsonConvert.DeserializeObject<DAOChange>(send.Tags["ppdata"]),
                _ => null
            };

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
                Proposal = new VoteProposal
                {
                    pptype = pptype,
                    data = send.Tags["ppdata"]
                },
            };

            gens.AddTag(Block.MANAGEDTAG, context.State.ToString());

            // pool blocks are service block so all service block signed by leader node
            gens.InitializeBlock(null, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            return gens;
        }

    }
}
