using Converto;
using Lyra.Core.API;
using Lyra.Core.Authorizers;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.WorkFlow
{
    [LyraWorkFlow]//v
    public class WFOtcTradeCreate : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_OTC_CRTRD,
                RecvVia = BrokerRecvType.DaoRecv,
            };
        }

        public override Task<Func<DagSystem, SendTransferBlock, Task<TransactionBlock>>[]> GetProceduresAsync(DagSystem sys, SendTransferBlock send)
        {
            if (send.Tags == null)
                throw new ArgumentNullException();

            var trade = JsonConvert.DeserializeObject<OTCTrade>(send.Tags["data"]);
            if(trade == null)
                throw new ArgumentNullException();

            if (trade.dir == TradeDirection.Buy)
            {
                return Task.FromResult(new[] {
                    SendTokenFromOrderToTradeAsync,
                    TradeGenesisReceiveAsync });
            }
            else
            {
                return Task.FromResult(new[] {
                    SendTokenFromDaoToOrderAsync,
                    OrderReceiveCryptoAsync,
                    SendTokenFromOrderToTradeAsync,
                    TradeGenesisReceiveAsync
                });
            }
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock send, TransactionBlock last)
        {
            if (send.Tags.Count != 2 ||
                !send.Tags.ContainsKey("data") ||
                string.IsNullOrWhiteSpace(send.Tags["data"])
                )
                return APIResultCodes.InvalidBlockTags;

            OTCTrade trade;
            try
            {
                trade = JsonConvert.DeserializeObject<OTCTrade>(send.Tags["data"]);
            }
            catch (Exception ex)
            {
                return APIResultCodes.InvalidBlockTags;
            }

            // daoId
            var dao = await sys.Storage.FindLatestBlockAsync(trade.daoId);
            if (dao == null || (dao as TransactionBlock).AccountID != send.DestinationAccountId)
                return APIResultCodes.InvalidOrgnization;

            // orderId
            var orderblk = await sys.Storage.FindLatestBlockAsync(trade.orderId) as IOtcOrder;
            if (orderblk == null)
                return APIResultCodes.InvalidOrder;

            var order = orderblk.Order;
            if (string.IsNullOrWhiteSpace(order.dealerId))
                return APIResultCodes.InvalidOrder;

            if (order.daoId != trade.daoId ||
                order.dealerId != trade.dealerId ||
                order.crypto != trade.crypto ||
                order.fiat != trade.fiat ||
                order.price != trade.price ||
                order.amount < trade.amount ||
                order.dir == trade.dir ||
                orderblk.OwnerAccountId != trade.orderOwnerId ||
                trade.pay > order.limitMax ||
                trade.pay < order.limitMin ||
                !order.payBy.Contains(trade.payVia)
                )
                return APIResultCodes.InvalidTrade;

            // pay
            if(trade.pay != Math.Round(trade.pay, 2))
                return APIResultCodes.InvalidTradeAmount;

            var got = Math.Round(trade.pay / order.price, 8);
            if(got != trade.amount)
                return APIResultCodes.InvalidTradeAmount;

            // verify collateral
            var chgs = send.GetBalanceChanges(last);
            if (!chgs.Changes.ContainsKey(LyraGlobal.OFFICIALTICKERCODE) ||
                chgs.Changes[LyraGlobal.OFFICIALTICKERCODE] < trade.collateral)
                return APIResultCodes.InvalidCollateral;

            if(trade.dir == TradeDirection.Sell)
            {
                if(!chgs.Changes.ContainsKey(trade.crypto) ||
                    chgs.Changes[trade.crypto] != trade.amount ||
                        chgs.Changes.Count != 2)
                {
                    return APIResultCodes.InvalidAmountToSend;
                }
            }
            else
            {
                if (chgs.Changes.Count != 1)
                    return APIResultCodes.InvalidAmountToSend;
            }

            // check the price of order and collateral.
            var dlrblk = await sys.Storage.FindLatestBlockAsync(trade.dealerId);
            var uri = new Uri(new Uri((dlrblk as IDealer).Endpoint), "/api/dealer/");
            var dealer = new DealerClient(uri);
            var prices = await dealer.GetPricesAsync();
            var tokenSymbol = order.crypto.Split('/')[1];

            if(trade.dir == TradeDirection.Buy)
            {
                if (trade.collateral * prices["LYR"] < prices[tokenSymbol] * trade.amount * ((dao as IDao).BuyerPar / 100))
                    return APIResultCodes.CollateralNotEnough;
            }
            else
            {
                if (trade.collateral * prices["LYR"] < prices[tokenSymbol] * trade.amount  * ((dao as IDao).SellerPar / 100))
                    return APIResultCodes.CollateralNotEnough;
            }

            return APIResultCodes.Success;
        }

        async Task<TransactionBlock> SendTokenFromOrderToTradeAsync(DagSystem sys, SendTransferBlock send)
        {
            var trade = JsonConvert.DeserializeObject<OTCTrade>(send.Tags["data"]);

            // send token from order to trade
            var lastblock = await sys.Storage.FindLatestBlockAsync(trade.orderId) as TransactionBlock;

            var keyStr = $"{send.Hash.Substring(0, 16)},{trade.crypto},{send.AccountID}";
            var AccountId = Base58Encoding.EncodeAccountId(Encoding.ASCII.GetBytes(keyStr).Take(64).ToArray());

            var sb = await sys.Storage.GetLastServiceBlockAsync();

            var nextblock = lastblock.GenInc<OtcOrderSendBlock>();  //gender change
            var sendtotrade = nextblock
                .With(new
                {
                        // generic
                    ServiceHash = sb.Hash,
                    BlockType = BlockTypes.OTCOrderSend,

                        // send & recv
                    DestinationAccountId = AccountId,

                        // broker
                    RelatedTx = send.Hash,

                        // business object
                    Order = nextblock.Order.With(new
                    {
                        amount = ((IOtcOrder)lastblock).Order.amount - trade.amount,
                    }),
                    OOStatus = ((IOtcOrder)lastblock).Order.amount - trade.amount == 0 ?
                        OTCOrderStatus.Closed : OTCOrderStatus.Partial,
                });

            // calculate balance
            var dict = lastblock.Balances.ToDecimalDict();
            dict[trade.crypto] -= trade.amount;
            sendtotrade.Balances = dict.ToLongDict();
            sendtotrade.InitializeBlock(lastblock, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            return sendtotrade;
        }

        async Task<TransactionBlock> SendTokenFromDaoToOrderAsync(DagSystem sys, SendTransferBlock send)
        {
            var trade = JsonConvert.DeserializeObject<OTCTrade>(send.Tags["data"]);

            var lastblock = await sys.Storage.FindLatestBlockAsync(trade.daoId) as TransactionBlock;

            return await TransactionOperateAsync(sys, send.Hash, lastblock,
                () => lastblock.GenInc<DaoSendBlock>(),
                (b) =>
                {
                    // send
                    (b as SendTransferBlock).DestinationAccountId = trade.orderId;

                    // send the amount of crypto from dao to order
                    var dict = lastblock.Balances.ToDecimalDict();
                    dict[trade.crypto] -= trade.amount;
                    b.Balances = dict.ToLongDict();
                });
        }

        async Task<TransactionBlock> OrderReceiveCryptoAsync(DagSystem sys, SendTransferBlock send)
        {
            var trade = JsonConvert.DeserializeObject<OTCTrade>(send.Tags["data"]);

            var lastblock = await sys.Storage.FindLatestBlockAsync(trade.orderId) as TransactionBlock;
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);

            return await TransactionOperateAsync(sys, send.Hash, lastblock,
                () => lastblock.GenInc<OtcOrderRecvBlock>(),
                (b) =>
                {
                    // send
                    (b as ReceiveTransferBlock).SourceHash = blocks.Last().Hash;

                    // send the amount of crypto from dao to order
                    var dict = lastblock.Balances.ToDecimalDict();
                    if (dict.ContainsKey(trade.crypto))
                        dict[trade.crypto] += trade.amount;
                    else
                        dict.Add(trade.crypto, trade.amount);
                    b.Balances = dict.ToLongDict();
                });
        }

        async Task<TransactionBlock> TradeGenesisReceiveAsync(DagSystem sys, SendTransferBlock send)
        {
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);

            var trade = JsonConvert.DeserializeObject<OTCTrade>(send.Tags["data"]);

            var keyStr = $"{send.Hash.Substring(0, 16)},{trade.crypto},{send.AccountID}";
            var AccountId = Base58Encoding.EncodeAccountId(Encoding.ASCII.GetBytes(keyStr).Take(64).ToArray());

            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var otcblock = new OtcTradeGenesisBlock
            {
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,

                // transaction
                AccountType = AccountTypes.OTC,
                AccountID = AccountId,
                Balances = new Dictionary<string, long>(),

                // receive
                SourceHash = (blocks.Last() as TransactionBlock).Hash,

                // broker
                Name = "no name",
                OwnerAccountId = send.AccountID,
                RelatedTx = send.Hash,

                // otc
                Trade = trade,
            };

            otcblock.Balances.Add(trade.crypto, trade.amount.ToBalanceLong());
            otcblock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            // pool blocks are service block so all service block signed by leader node
            otcblock.InitializeBlock(null, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            return otcblock;
        }

        //async Task<TransactionBlock> SendUtilityTokenToUserAsync(DagSystem sys, SendTransferBlock send)
        //{
        //    throw new NotImplementedException();
        //}
    }
}
