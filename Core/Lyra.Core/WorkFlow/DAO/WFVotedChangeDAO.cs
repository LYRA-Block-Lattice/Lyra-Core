using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.API.ODR;
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
    [LyraWorkFlow]//v
    public class WFVotedChangeDAO : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_DAO_VOTED_CHANGE,
                RecvVia = BrokerRecvType.None,
                Steps = new[] { MainAsync }
            };
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            if (send.Tags.Count != 3 ||
                !send.Tags.ContainsKey("data") ||
                string.IsNullOrWhiteSpace(send.Tags["data"]) ||
                !send.Tags.ContainsKey("voteid")
                )
                return APIResultCodes.InvalidBlockTags;

            // dao must exists
            var dao = await sys.Storage.FindLatestBlockAsync(send.DestinationAccountId)
                as IDao;
            if (dao == null)
                return APIResultCodes.InvalidDAO;

            // verify it
            var change = JsonConvert.DeserializeObject<DAOChange>(send.Tags["data"]);
            if (change == null || change.creator != dao.OwnerAccountId || send.AccountID != dao.OwnerAccountId)
                return APIResultCodes.Unauthorized;

            var voteid = send.Tags["voteid"];
            if (string.IsNullOrWhiteSpace(voteid))
            {
                return APIResultCodes.Unauthorized;
            }
            else
            {
                var vs = await sys.Storage.GetVoteSummaryAsync(voteid);
                if(vs == null || !vs.IsDecided)
                    return APIResultCodes.Unauthorized;

                if (vs.Spec.Proposal.data != send.Tags["data"])
                    return APIResultCodes.ArgumentOutOfRange;

                // should not execute more than once
                var exec = await sys.Storage.FindExecForVoteAsync(voteid);
                if (exec != null)
                    return APIResultCodes.AlreadyExecuted;
            }

            if (change.settings == null || change.settings.Count == 0)
                return APIResultCodes.InvalidArgument;

            return WFChangeDAO.VerifyDaoChanges(change);
        }

        async Task<TransactionBlock> MainAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            // check exists
            var recv = await sys.Storage.FindBlockBySourceHashAsync(send.Hash);
            if (recv != null)
                return null;

            var daoid = send.DestinationAccountId;
            var change = JsonConvert.DeserializeObject<DAOChange>(send.Tags["data"]);

            var prevBlock = await sys.Storage.FindLatestBlockAsync(daoid) as TransactionBlock;
            var txInfo = send.GetBalanceChanges(await sys.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock);

            return await TransactionOperateAsync(sys, send.Hash, prevBlock, 
                () => prevBlock.GenInc<DaoVotedChangeBlock>(),
                () => WFState.Finished,
                (b) =>
                {
                    // recv
                    (b as ReceiveTransferBlock).SourceHash = send.Hash;

                    // broker
                    (b as IBrokerAccount).OwnerAccountId = send.AccountID;
                    (b as IBrokerAccount).RelatedTx = send.Hash;

                    // balance
                    var oldbalance = prevBlock.Balances.ToDecimalDict();
                    if (oldbalance.ContainsKey("LYR"))
                        oldbalance["LYR"] += txInfo.Changes["LYR"];
                    else
                        oldbalance.Add("LYR", txInfo.Changes["LYR"]);
                    b.Balances = oldbalance.ToLongDict();

                    // vote
                    (b as IVoteExec).voteid = send.Tags["voteid"];

                    // change config
                    var dao = b as IDao;
                    foreach(var chg in change.settings)
                    {
                        if(chg.Key == "ShareRito")
                            dao.ShareRito = decimal.Parse(chg.Value);
                        else if (chg.Key == "Seats")
                            dao.Seats = int.Parse(chg.Value);
                        else if (chg.Key == "SellerPar")
                            dao.SellerPar = int.Parse(chg.Value);
                        else if (chg.Key == "BuyerPar")
                            dao.BuyerPar = int.Parse(chg.Value);
                        else if (chg.Key == "Description")
                            dao.Description = chg.Value;
                    }
                });
        }

    }
}
