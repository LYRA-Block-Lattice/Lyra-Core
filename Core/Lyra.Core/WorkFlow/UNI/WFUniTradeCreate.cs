using Converto;
using Lyra.Core.API;
using Lyra.Core.Authorizers;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Core.WorkFlow.Uni;
using Lyra.Data.API;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.API.WorkFlow.UniMarket;
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
    public class WFUniTradeCreate : UniSharedWFCode
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_UNI_CRTRD,
                RecvVia = BrokerRecvType.DaoRecv,
            };
        }

        public override Task<Func<DagSystem, SendTransferBlock, Task<TransactionBlock>>[]> GetProceduresAsync(DagSystem sys, SendTransferBlock send)
        {
            if (send.Tags == null)
                throw new ArgumentNullException();

            var trade = JsonConvert.DeserializeObject<UniTrade>(send.Tags["data"]);
            if(trade == null)
                throw new ArgumentNullException();

            // the matrix
            // seller fiat, buyer fiat: seller manual send, buyer manual confirm, manual send, seller manual confirm
            // holding type: NFT, Token: auto transfer. others: manual deliver and need peer to confirm.

            bool IsBidToken = LyraGlobal.GetHoldTypeFromTicker(trade.biding) == HoldTypes.Token
                || LyraGlobal.GetHoldTypeFromTicker(trade.biding) == HoldTypes.NFT;
            bool IsOfferToken = LyraGlobal.GetHoldTypeFromTicker(trade.offering) == HoldTypes.Token
                || LyraGlobal.GetHoldTypeFromTicker(trade.offering) == HoldTypes.NFT;

            if (IsBidToken && IsOfferToken)
            {
                //Console.WriteLine("Auto trade for both token is enabled.");
                // when both binding and offering are token/nft, the trade will be finish automatically.
                return Task.FromResult(new[] {
                    SendTokenFromOrderToTradeAsync,
                    TradeGenesisReceiveAsync,
                                        
                    // bellow auto trade
                    // for buyer
                    SendCryptoProductFromTradeToBuyerAsync,
                    SendCollateralFromDAOToBuyerAsync,

                    // for seller
                    SealOrderAsync, 
                    SendCollateralToSellerAsync
                });
            }
            else
            {
                // all need OTC
                return Task.FromResult(new[] {
                    SendTokenFromOrderToTradeAsync,
                    TradeGenesisReceiveAsync,
                });
            }

            
            // bellow for buying order. temp archive
            //else
            //{
            //    return Task.FromResult(new[] {
            //        SendTokenFromDaoToOrderAsync,
            //        OrderReceiveCryptoAsync,
            //        SendTokenFromOrderToTradeAsync,
            //        TradeGenesisReceiveAsync
            //    });
            //}
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock send, TransactionBlock last)
        {
            if (send.Tags.Count != 2 ||
                !send.Tags.ContainsKey("data") ||
                string.IsNullOrWhiteSpace(send.Tags["data"])
                )
                return APIResultCodes.InvalidBlockTags;

            UniTrade trade;
            try
            {
                trade = JsonConvert.DeserializeObject<UniTrade>(send.Tags["data"]);
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
            var orderblk = await sys.Storage.FindLatestBlockAsync(trade.orderId) as IUniOrder;
            if (orderblk == null)
                return APIResultCodes.InvalidOrder;

            var order = orderblk.Order;
            if (string.IsNullOrWhiteSpace(order.dealerId))
                return APIResultCodes.InvalidOrder;

            if (order.daoId != trade.daoId ||
                order.dealerId != trade.dealerId ||
                order.offerby != trade.offby ||
                order.offering != trade.offering ||
                order.bidby != trade.bidby ||
                order.biding != trade.biding ||
                order.price != trade.price ||
                order.amount < trade.amount ||
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
                chgs.Changes[LyraGlobal.OFFICIALTICKERCODE] < trade.cltamt)
                return APIResultCodes.InvalidCollateral;

            var bidg = await sys.Storage.FindTokenGenesisBlockAsync("", trade.biding);

            if (bidg.DomainName != "fiat")
            {
                if (!chgs.Changes.ContainsKey(bidg.Ticker) ||
                    chgs.Changes[bidg.Ticker] != trade.amount ||
                        chgs.Changes.Count != 2)
                {
                    return APIResultCodes.InvalidAmountToSend;
                }
            }

            // check the price of order and collateral. !not needed.
            //var dlrblk = await sys.Storage.FindLatestBlockAsync(trade.dealerId);
            //var uri = new Uri(new Uri((dlrblk as IDealer).Endpoint), "/api/dealer/");
            //var dealer = new DealerClient(uri);
            //var prices = await dealer.GetPricesAsync();
            //var tokenSymbol = propg.Ticker.Split('/')[1];

            //if(prices.ContainsKey(tokenSymbol))
            //{
            //    // only calculate the worth of collateral when we have a standard price for the property.
            //    if (trade.cltamt * prices["LYR"] < prices[tokenSymbol] * trade.amount * ((dao as IDao).BuyerPar / 100))
            //            return APIResultCodes.CollateralNotEnough;
            //}

            return APIResultCodes.Success;
        }

        async Task<TransactionBlock> SendTokenFromOrderToTradeAsync(DagSystem sys, SendTransferBlock send)
        {
            var trade = JsonConvert.DeserializeObject<UniTrade>(send.Tags["data"]);

            // send token from order to trade
            var lastblock = await sys.Storage.FindLatestBlockAsync(trade.orderId) as TransactionBlock;

            var keyStr = $"{send.Hash.Substring(0, 16)},{trade.offering},{send.AccountID}";
            var AccountId = Base58Encoding.EncodeAccountId(Encoding.ASCII.GetBytes(keyStr).Take(64).ToArray());

            return await TransactionOperateAsync(sys, send.Hash, lastblock,
                () => lastblock.GenInc<UniOrderSendBlock>(),
                () => WFState.Running,
                (b) =>
                {
                    // send
                    (b as SendTransferBlock).DestinationAccountId = AccountId;

                    // IUniTrade
                    var nextOdr = b as IUniOrder;
                    nextOdr.Order = nextOdr.Order.With(new
                    {
                        amount = ((IUniOrder)lastblock).Order.amount - trade.amount,
                    });

                    nextOdr.UOStatus = ((IUniOrder)lastblock).Order.amount - trade.amount == 0 ?
                        UniOrderStatus.Closed : UniOrderStatus.Partial;

                    // calculate balance
                    var dict = lastblock.Balances.ToDecimalDict();
                    dict[trade.offering] -= trade.amount;
                    b.Balances = dict.ToLongDict();
                });

            //var nextblock = lastblock.GenInc<UniOrderSendBlock>();  //gender change
            //var sendtotrade = nextblock
            //    .With(new
            //    {
            //            // generic
            //        ServiceHash = sb.Hash,
            //        BlockType = BlockTypes.UniOrderSend,

            //            // send & recv
            //        DestinationAccountId = AccountId,

            //            // broker
            //        RelatedTx = send.Hash,

            //            // business object
            //        Order = nextblock.Order.With(new
            //        {
            //            amount = ((IUniOrder)lastblock).Order.amount - trade.amount,
            //        }),
            //        OOStatus = ((IUniOrder)lastblock).Order.amount - trade.amount == 0 ?
            //            UniOrderStatus.Closed : UniOrderStatus.Partial,
            //    });

            //// calculate balance
            //var dict = lastblock.Balances.ToDecimalDict();
            //dict[trade.crypto] -= trade.amount;
            //sendtotrade.Balances = dict.ToLongDict();
            //sendtotrade.InitializeBlock(lastblock, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            //return sendtotrade;
        }

/*        async Task<TransactionBlock> SendTokenFromDaoToOrderAsync(DagSystem sys, SendTransferBlock send)
        {
            var trade = JsonConvert.DeserializeObject<UniTrade>(send.Tags["data"]);

            var lastblock = await sys.Storage.FindLatestBlockAsync(trade.daoId) as TransactionBlock;

            return await TransactionOperateAsync(sys, send.Hash, lastblock,
                () => lastblock.GenInc<DaoSendBlock>(),
                () => WFState.Running,
                (b) =>
                {
                    // send
                    (b as SendTransferBlock).DestinationAccountId = trade.orderId;

                    // send the amount of crypto from dao to order
                    var dict = lastblock.Balances.ToDecimalDict();
                    dict[trade.offering] -= trade.amount;
                    b.Balances = dict.ToLongDict();
                });
        }

        async Task<TransactionBlock> OrderReceiveCryptoAsync(DagSystem sys, SendTransferBlock send)
        {
            var trade = JsonConvert.DeserializeObject<UniTrade>(send.Tags["data"]);
            var lastblock = await sys.Storage.FindLatestBlockAsync(trade.orderId) as TransactionBlock;
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);

            return await TransactionOperateAsync(sys, send.Hash, lastblock,
                () => lastblock.GenInc<UniOrderRecvBlock>(),
                () => WFState.Running,
                (b) =>
                {
                    // send
                    (b as ReceiveTransferBlock).SourceHash = blocks.Last().Hash;

                    // send the amount of crypto from dao to order
                    var dict = lastblock.Balances.ToDecimalDict();
                    if (dict.ContainsKey(trade.offering))
                        dict[trade.offering] += trade.amount;
                    else
                        dict.Add(trade.offering, trade.amount);
                    b.Balances = dict.ToLongDict();
                });
        }*/

        async Task<TransactionBlock> TradeGenesisReceiveAsync(DagSystem sys, SendTransferBlock send)
        {
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);

            var trade = JsonConvert.DeserializeObject<UniTrade>(send.Tags["data"]);
            var keyStr = $"{send.Hash.Substring(0, 16)},{trade.offering},{send.AccountID}";
            var AccountId = Base58Encoding.EncodeAccountId(Encoding.ASCII.GetBytes(keyStr).Take(64).ToArray());

            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var Uniblock = new UniTradeGenesisBlock
            {
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,

                // transaction
                AccountType = LyraGlobal.GetAccountTypeFromTicker(trade.offering),
                AccountID = AccountId,
                Balances = new Dictionary<string, long>(),

                // receive
                SourceHash = (blocks.Last() as TransactionBlock).Hash,

                // broker
                Name = "no name",
                OwnerAccountId = send.AccountID,
                RelatedTx = send.Hash,

                // Uni
                Trade = trade,
            };

            string wfstr;
            if (LyraGlobal.GetHoldTypeFromTicker(trade.biding) == HoldTypes.Token
                || LyraGlobal.GetHoldTypeFromTicker(trade.biding) == HoldTypes.NFT)

            {
                // non OTC, set status directly to biding sent.
                Uniblock.UTStatus = UniTradeStatus.BidReceived;
                wfstr = WFState.Running.ToString();
            }
            else
            {
                Uniblock.UTStatus = UniTradeStatus.Open;
                wfstr = WFState.Finished.ToString();
            }

            Uniblock.Balances.Add(trade.offering, trade.amount.ToBalanceLong());
            Uniblock.AddTag(Block.MANAGEDTAG, wfstr);

            // pool blocks are service block so all service block signed by leader node
            Uniblock.InitializeBlock(null, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            return Uniblock;
        }

        protected async Task<TransactionBlock> SendCryptoProductFromTradeToBuyerAsync(DagSystem sys, SendTransferBlock send)
        {
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);
            var lastblock = blocks.LastOrDefault() as TransactionBlock;

            Console.WriteLine("Call SendCryptoProductFromTradeToBuyerAsync");
            return await TransactionOperateAsync(sys, send.Hash, lastblock,
                () => lastblock.GenInc<UniTradeSendBlock>(),
                () => WFState.Running,
                (b) =>
                {
                    var trade = b as IUniTrade;

                    trade.UTStatus = UniTradeStatus.OfferReceived;

                    (b as SendTransferBlock).DestinationAccountId = trade.OwnerAccountId;

                    b.Balances[trade.Trade.offering] = 0;
                });
        }

        protected async Task<TransactionBlock> SendCollateralFromDAOToBuyerAsync(DagSystem sys, SendTransferBlock send)
        {
            Console.WriteLine("Call SendCollateralFromDAOToBuyerAsync");
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);
            var lastblock = blocks.LastOrDefault() as TransactionBlock;
            var tradelatest = lastblock;

            var trade = (tradelatest as IUniTrade).Trade;
            var daolastblock = await sys.Storage.FindLatestBlockAsync(trade.daoId) as TransactionBlock;

            // get dao for order genesis
            var odrgen = await sys.Storage.FindFirstBlockAsync(trade.orderId) as ReceiveTransferBlock;
            var daoforodr = await sys.Storage.FindBlockByHashAsync(odrgen.SourceHash) as IDao;

            // buyer fee calculated as LYR
            var totalAmount = trade.amount;
            decimal totalFee = 0;
            decimal networkFee = 0;
            var order = (odrgen as IUniOrder).Order;
            // transaction fee

            totalFee += Math.Round(trade.cltamt * daoforodr.BuyerFeeRatio, 8);
            networkFee = Math.Round(trade.cltamt * LyraGlobal.BidingNetworkFeeRatio, 8);

            Console.WriteLine($"buyer pay svc fee {totalFee} and net fee {networkFee}");

            var amountToSeller = trade.cltamt - totalFee;
            //Console.WriteLine($"collateral: {trade.collateral} txfee: {totalFee} netfee: {networkFee} remains: {trade.collateral - totalFee - networkFee} cost: {totalFee + networkFee }");

            return await TransactionOperateAsync(sys, send.Hash, daolastblock,
                () => daolastblock.GenInc<DaoSendBlock>(),
                () => WFState.Running,
                (b) =>
                {
                    // block
                    b.Fee = networkFee;
                    b.FeeType = AuthorizationFeeTypes.Dynamic;

                    // recv
                    (b as SendTransferBlock).DestinationAccountId = (tradelatest as IUniTrade).OwnerAccountId;

                    // balance
                    var oldbalance = b.Balances.ToDecimalDict();
                    oldbalance[LyraGlobal.OFFICIALTICKERCODE] -= amountToSeller;
                    b.Balances = oldbalance.ToLongDict();
                });
        }

        protected async Task<TransactionBlock> SealOrderAsync(DagSystem sys, SendTransferBlock send)
        {
            var unitrade = JsonConvert.DeserializeObject<UniTrade>(send.Tags["data"]);
            return await SealUniOrderAsync(sys, send.Hash, unitrade.orderId);
        }

        protected async Task<TransactionBlock> SendCollateralToSellerAsync(DagSystem sys, SendTransferBlock send)
        {
            var unitrade = JsonConvert.DeserializeObject<UniTrade>(send.Tags["data"]);
            return await SendCollateralToSellerAsync(sys, send.Hash, unitrade.orderId);
        }
    }
}
