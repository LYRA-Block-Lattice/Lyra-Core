using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
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

namespace Lyra.Core.WorkFlow.Uni
{
    [LyraWorkFlow]//v
    public class WFUniOrderClose : UniSharedWFCode
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_UNI_ORDCLOSE,
                RecvVia = BrokerRecvType.DaoRecv,
                Steps = new [] { SealOrderAsync, SendCollateralToSellerAsync}
            };
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            if (send.Tags.Count != 3 ||
                !send.Tags.ContainsKey("daoid") ||                                            
                !send.Tags.ContainsKey("orderid") ||
                string.IsNullOrWhiteSpace(send.Tags["orderid"])
                )
                return APIResultCodes.InvalidBlockTags;

            var daoid = send.Tags["daoid"];
            var orderid = send.Tags["orderid"];
            var daoblk = await sys.Storage.FindLatestBlockAsync(daoid);
            var orderblk = await sys.Storage.FindLatestBlockAsync(orderid);

            var ordertx = orderblk as TransactionBlock;

            // need some balance to close. old bug
            if (!ordertx.Balances.Any(a => a.Value > 0))
                return APIResultCodes.InsufficientFunds;

            if (daoblk == null || orderblk == null || 
                (orderblk as IUniOrder).Order.daoId != (daoblk as TransactionBlock).AccountID)
                return APIResultCodes.InvalidTrade;

            if ((orderblk as IBrokerAccount).OwnerAccountId != send.AccountID)
                return APIResultCodes.NotSellerOfTrade;

            // the order may has already closed because no tradable assert avaliable.
            //if ((orderblk as IUniOrder).UOStatus != UniOrderStatus.Open &&
            //    (orderblk as IUniOrder).UOStatus != UniOrderStatus.Partial &&
            //    (orderblk as IUniOrder).UOStatus != UniOrderStatus.Delist)
            //    return APIResultCodes.InvalidOrderStatus;

            var trades = await sys.Storage.FindUniTradeForOrderAsync(orderid);
            if(trades.Any())
            {
                var opened = trades.Cast<IUniTrade>()
                    .Where(a => a.UTStatus != UniTradeStatus.Canceled
                        && a.UTStatus != UniTradeStatus.Closed
                        && a.UTStatus != UniTradeStatus.DisputeClosed
                        && a.UTStatus != UniTradeStatus.OfferReceived
                    );
                if(opened.Any())
                {
                    return APIResultCodes.TradesPending;
                }
            }
            return APIResultCodes.Success;
        }

        protected async Task<TransactionBlock> SealOrderAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            return await SealUniOrderAsync(sys, context, send.Hash, send.Tags["orderid"]);
        }

        protected async Task<TransactionBlock> SendCollateralToSellerAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            var daoid = send.Tags["daoid"];
            var orderid = send.Tags["orderid"];
            return await SendCollateralToSellerAsync(sys, context, send.Hash, orderid);
        }
    }
}
