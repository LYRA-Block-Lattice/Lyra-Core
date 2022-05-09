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

            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var sendToTradeBlock = new OtcOrderSendBlock
            {
                // block
                ServiceHash = sb.Hash,

                // trans
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,
                AccountID = lastblock.AccountID,
                Balances = lastblock.Balances.ToDecimalDict().ToLongDict(),

                // send
                DestinationAccountId = (lastblock as IBrokerAccount).OwnerAccountId,

                // broker
                Name = ((IBrokerAccount)lastblock).Name,
                OwnerAccountId = ((IBrokerAccount)lastblock).OwnerAccountId,
                RelatedTx = send.Hash,

                // otc
                Order = new OTCOrder
                {
                    daoId = ((IOtcOrder)lastblock).Order.daoId,
                    dir = ((IOtcOrder)lastblock).Order.dir,
                    crypto = ((IOtcOrder)lastblock).Order.crypto,
                    fiat = ((IOtcOrder)lastblock).Order.fiat,
                    fiatPrice = ((IOtcOrder)lastblock).Order.fiatPrice,
                    priceType = ((IOtcOrder)lastblock).Order.priceType,
                    price = ((IOtcOrder)lastblock).Order.price,
                    limitMax = ((IOtcOrder)lastblock).Order.limitMax,
                    limitMin = ((IOtcOrder)lastblock).Order.limitMin,
                    payBy = ((IOtcOrder)lastblock).Order.payBy,
                    amount = 0,
                    collateral = ((IOtcOrder)lastblock).Order.collateral,   // this is the difference with close.
                    collateralPrice = ((IOtcOrder)lastblock).Order.collateralPrice
                },
                OOStatus = OTCOrderStatus.Delist,
            };

            sendToTradeBlock.Balances[order.crypto] = 0;

            sendToTradeBlock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            sendToTradeBlock.InitializeBlock(lastblock, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            return sendToTradeBlock;
        }
    }
}
