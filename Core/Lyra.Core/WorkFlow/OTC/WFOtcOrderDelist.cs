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

namespace Lyra.Core.WorkFlow.OTC
{
    /// <summary>
    /// when a order is partial traded, it can be delisted.
    /// crypto in selling will be send back to owner, but the collateral will remain untouched.
    /// </summary>
    [LyraWorkFlow]//v
    public class WFOtcOrderDelist : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_OTC_ORDDELST,
                RecvVia = BrokerRecvType.DaoRecv,
                Steps = new [] { DelistOrderAsync }
            };
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock send, TransactionBlock last)
        {
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
            if (daoblk == null || orderblk == null || 
                (orderblk as IOtcOrder).Order.daoId != (daoblk as TransactionBlock).AccountID)
                return APIResultCodes.InvalidTrade;

            if ((orderblk as IBrokerAccount).OwnerAccountId != send.AccountID)
                return APIResultCodes.NotSellerOfTrade;

            if ((orderblk as IOtcOrder).OOStatus != OTCOrderStatus.Partial)
                return APIResultCodes.InvalidOrderStatus;

            return APIResultCodes.Success;
        }

        async Task<TransactionBlock> DelistOrderAsync(DagSystem sys, SendTransferBlock send)
        {
            var daoid = send.Tags["daoid"];
            var orderid = send.Tags["orderid"];

            var lastblock = await sys.Storage.FindLatestBlockAsync(orderid) as TransactionBlock;
            var order = (lastblock as IOtcOrder).Order;

            return await TransactionOperateAsync(sys, send.Hash, lastblock,
                () => lastblock.GenInc<OtcOrderSendBlock>(),
                (b) =>
                {
                        // send
                    (b as SendTransferBlock).DestinationAccountId = (lastblock as IBrokerAccount).OwnerAccountId;

                    var dict = lastblock.Balances.ToDecimalDict();
                    if (order.dir == TradeDirection.Sell)
                    {
                        // send the amount of crypto to order owner
                        dict[order.crypto] = 0;
                    }
                    else
                    {
                        dict[LyraGlobal.OFFICIALTICKERCODE] -= 1;   // must send something
                    }

                    b.Balances = dict.ToLongDict();

                    (b as IOtcOrder).OOStatus = OTCOrderStatus.Delist;
                });
        }
    }
}
