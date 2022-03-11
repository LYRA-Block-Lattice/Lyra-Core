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
    [LyraWorkFlow]
    public class WFChangeDAO : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_DAO_CHANGE,
                RecvVia = BrokerRecvType.None,
                Steps = new[] { MainAsync }
            };
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock send, TransactionBlock last)
        {
            if (send.Tags.Count != 2 ||
                !send.Tags.ContainsKey("data") ||
                string.IsNullOrWhiteSpace(send.Tags["data"])
                )
                return APIResultCodes.InvalidBlockTags;

            // dao must exists
            var dao = await sys.Storage.FindLatestBlockAsync(send.DestinationAccountId)
                as IDao;
            if (dao == null)
                return APIResultCodes.InvalidDAO;

            // verify it
            var change = JsonConvert.DeserializeObject<DAOChange>(send.Tags["data"]);
            if (change.creator != dao.OwnerAccountId)
                return APIResultCodes.Unauthorized;

            if(string.IsNullOrWhiteSpace(change.voteid))
            {
                // only valid if no stake holders
                if (dao.Treasure.Count > 0)
                    return APIResultCodes.Unauthorized;
            }
            else
            {
                // TODO: verify against the vote
                // vote must contain the same changes
                return APIResultCodes.Unauthorized;
            }

            if (change.settings == null || change.settings.Count == 0)
                return APIResultCodes.InvalidArgument;

            return APIResultCodes.Success;
        }

        async Task<TransactionBlock> MainAsync(DagSystem sys, SendTransferBlock send)
        {
            // check exists
            var recv = await sys.Storage.FindBlockBySourceHashAsync(send.Hash);
            if (recv != null)
                return null;

            var daoid = send.DestinationAccountId;
            var change = JsonConvert.DeserializeObject<DAOChange>(send.Tags["data"]);

            var prevBlock = await sys.Storage.FindLatestBlockAsync(daoid) as TransactionBlock;
            var txInfo = send.GetBalanceChanges(await sys.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock);
            var lsb = await sys.Storage.GetLastServiceBlockAsync();

            return await TransactionOperateAsync(sys, send.Hash, prevBlock, 
                () => prevBlock.GenInc<DaoRecvBlock>(),
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
                    }
                });
        }

    }
}
