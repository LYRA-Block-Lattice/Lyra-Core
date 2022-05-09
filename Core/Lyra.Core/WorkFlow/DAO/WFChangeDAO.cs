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
            if(string.IsNullOrWhiteSpace(voteid))
            {
                // only valid if no stake holders
                if (dao.Treasure.Count > 0)
                    return APIResultCodes.Unauthorized;
            }
            else
            {
                return APIResultCodes.Unauthorized;
            }

            if (change.settings == null || change.settings.Count == 0)
                return APIResultCodes.InvalidArgument;

            return VerifyDaoChanges(change);
        }

        public static APIResultCodes VerifyDaoChanges(DAOChange change)
        {
            var unk = false;
            foreach (var chg in change.settings)
            {
                if (chg.Key == "ShareRito")
                {
                    var ShareRito = decimal.Parse(chg.Value);
                    if (ShareRito < 0 || ShareRito > 1)
                        return APIResultCodes.InvalidShareRitio;
                }
                else if (chg.Key == "Seats")
                {
                    var Seats = int.Parse(chg.Value);
                    if (Seats < 0 || Seats > 100)
                        return APIResultCodes.InvalidSeatsCount;
                }
                else if (chg.Key == "SellerFeeRatio")
                {
                    var SellerFeeRatio = decimal.Parse(chg.Value);
                    if (SellerFeeRatio < 0 || SellerFeeRatio > 100)
                        return APIResultCodes.ArgumentOutOfRange;
                }
                else if (chg.Key == "BuyerFeeRatio")
                {
                    var BuyerFeeRatio = decimal.Parse(chg.Value);
                    if (BuyerFeeRatio < 0 || BuyerFeeRatio > 100)
                        return APIResultCodes.ArgumentOutOfRange;
                }
                else if (chg.Key == "SellerPar")
                {
                    var SellerPar = int.Parse(chg.Value);
                    if (SellerPar <= 0)
                        return APIResultCodes.ArgumentOutOfRange;
                }
                else if (chg.Key == "BuyerPar")
                {
                    var BuyerPar = int.Parse(chg.Value);
                    if (BuyerPar <= 0)
                        return APIResultCodes.ArgumentOutOfRange;
                }
                else if (chg.Key == "Description")
                {
                    var Description = chg.Value;
                    if (string.IsNullOrEmpty(Description) || Description.Length > 300)
                        return APIResultCodes.ArgumentOutOfRange;
                }
                else
                    unk = true;
            }

            if (unk)
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
                    dao.PType = ProfitingType.Orgnization; // not necessary, but for testnet compatibility
                    foreach(var chg in change.settings)
                    {
                        if(chg.Key == "ShareRito")
                            dao.ShareRito = decimal.Parse(chg.Value);
                        else if (chg.Key == "Seats")
                            dao.Seats = int.Parse(chg.Value);
                        else if (chg.Key == "SellerFeeRatio")
                            dao.SellerFeeRatio = decimal.Parse(chg.Value);
                        else if (chg.Key == "BuyerFeeRatio")
                            dao.BuyerFeeRatio = decimal.Parse(chg.Value);
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
