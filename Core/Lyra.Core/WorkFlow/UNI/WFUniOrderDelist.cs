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
    /// <summary>
    /// when a order is partial traded, it can be delisted.
    /// crypto in selling will be send back to owner, but the collateral will remain untouched.
    /// </summary>
    [LyraWorkFlow]//v
    public class WFUniOrderDelist : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_UNI_ORDDELST,
                RecvVia = BrokerRecvType.DaoRecv,
                Steps = new [] { DelistOrderAsync }
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
            if (daoblk == null || orderblk == null || 
                (orderblk as IUniOrder).Order.daoId != (daoblk as TransactionBlock).AccountID)
                return APIResultCodes.InvalidTrade;

            if ((orderblk as IBrokerAccount).OwnerAccountId != send.AccountID)
                return APIResultCodes.NotSellerOfTrade;

            if ((orderblk as IUniOrder).UOStatus != UniOrderStatus.Partial)
                return APIResultCodes.InvalidOrderStatus;

            return APIResultCodes.Success;
        }

        async Task<TransactionBlock> DelistOrderAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            var daoid = send.Tags["daoid"];
            var orderid = send.Tags["orderid"];

            var lastblock = await sys.Storage.FindLatestBlockAsync(orderid) as TransactionBlock;
            var order = (lastblock as IUniOrder).Order;

            return await TransactionOperateAsync(sys, send.Hash, lastblock,
                () => lastblock.GenInc<UniOrderSendBlock>(),
                () => context.State,
                (b) =>
                {
                        // send
                    (b as SendTransferBlock).DestinationAccountId = (lastblock as IBrokerAccount).OwnerAccountId;

                    var dict = lastblock.Balances.ToDecimalDict();

                    // send the amount of crypto to order owner
                    dict[order.offering] = 0;

                    b.Balances = dict.ToLongDict();

                    (b as IUniOrder).UOStatus = UniOrderStatus.Delist;
                });
        }
    }
}
