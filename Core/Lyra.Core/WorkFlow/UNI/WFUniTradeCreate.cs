using Converto;
using Loyc.Collections;
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

        public override Task<Func<DagSystem, LyraContext, Task<TransactionBlock?>>[]> GetProceduresAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            if (send.Tags == null)
                throw new ArgumentNullException();

            var trade = JsonConvert.DeserializeObject<UniTrade>(send.Tags["data"]);
            if(trade == null)
                throw new ArgumentNullException();

            // the matrix
            // seller fiat, buyer fiat: seller manual send, buyer manual confirm, manual send, seller manual confirm
            // holding type: NFT, Token: auto transfer. others: manual deliver and need peer to confirm.

            bool IsBidToken = !LyraGlobal.GetOTCRequirementFromTicker(trade.biding);
            bool IsOfferToken = !LyraGlobal.GetOTCRequirementFromTicker(trade.offering);

            // if token then auto process
            if (IsBidToken && IsOfferToken)
            {
                //Console.WriteLine("Auto trade for both token is enabled.");
                // when both binding and offering are token/nft, the trade will be finish automatically.
                return Task.FromResult(new[] {
                    SendTokenFromOrderToTradeAsync,
                    TradeGenesisReceiveAsync,

                    // send buyer's collateral and toke from dao to the trade
                    // so the trade has 4 properties: offering, biding, collateral of seller, collateral of buyer.
                    SendTokenFromDaoToTradeAsync,
                    TradeReceiveTokenFromDaoAsync,
                                        
                    // bellow auto trade
                    // for buyer
                    SendPropsFromTradeToBuyerAsync,
                    SendPropsFromTradeToSellerAsync,
                    SendFeesToDaoAsync
                });
            }
            //else if(IsBidToken && !IsOfferToken)
            //{
            //    return Task.FromResult(new[] {
            //        SendTokenFromOrderToTradeAsync,
            //        TradeGenesisReceiveAsync,
                                        
            //        // bellow auto trade
            //        // for buyer
            //        SendCryptoProductFromTradeToBuyerAsync,
            //        SendCollateralFromDAOToBuyerAsync,
            //    });
            //}
            else
            {
                // all need OTC
                return Task.FromResult(new[] {
                    SendTokenFromOrderToTradeAsync,
                    TradeGenesisReceiveAsync,

                    SendTokenFromDaoToTradeAsync,
                    TradeReceiveTokenFromDaoAsync,
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

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
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

            // break code bellow into steps
            if (order.daoId != trade.daoId ||
                order.dealerId != trade.dealerId ||
                order.offerby != trade.offby ||
                order.offering != trade.offering ||
                order.bidby != trade.bidby ||
                order.biding != trade.biding
                )
                return APIResultCodes.InvalidTrade;

            // break code bellow into steps
            if (
                order.price != trade.price ||
                order.eqprice != trade.eqprice ||
                order.amount < trade.amount ||
                orderblk.OwnerAccountId != trade.orderOwnerId
                //|| !order.payBy.Contains(trade.payVia)
                )
                return APIResultCodes.InvalidTrade;

            // break code bellow into steps
            if (
                trade.amount > order.limitMax ||
                trade.amount < order.limitMin ||
                trade.pay != Math.Round(trade.price * trade.amount, 8)
                //|| !order.payBy.Contains(trade.payVia)
                )
                return APIResultCodes.InvalidTrade;

            // pay
            if (trade.pay != Math.Round(trade.pay, 8))
                return APIResultCodes.InvalidTradeAmount;

            var got = Math.Round(trade.pay / order.price, 8);
            if(got != trade.amount)
                return APIResultCodes.InvalidTradeAmount;

            // verify collateral
            var totalTradeValue = trade.eqprice * trade.amount;
            (var tradeFee, var networkFee) = UniTradeFees.CalculateSellerFees(trade.eqprice, trade.amount, (dao as IDao).BuyerFeeRatio);
            var totalFee = tradeFee + networkFee;
            var totalCollateral = totalTradeValue * ((dao as IDao).BuyerPar / 100m) + totalFee;
            if (trade.cltamt != totalCollateral)
                return APIResultCodes.InvalidCollateral;

            TransactionBlock last = await DagSystem.Singleton.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock;
            var chgs = send.GetBalanceChanges(last);

            if(trade.biding == LyraGlobal.OFFICIALTICKERCODE)
            {
                if (!chgs.Changes.ContainsKey(LyraGlobal.OFFICIALTICKERCODE) ||
                    chgs.Changes[LyraGlobal.OFFICIALTICKERCODE] != trade.amount + trade.cltamt + send.Fee)
                    return APIResultCodes.InvalidCollateral;
            }
            else
            {
                if (!chgs.Changes.ContainsKey(LyraGlobal.OFFICIALTICKERCODE) ||
                    chgs.Changes[LyraGlobal.OFFICIALTICKERCODE] != trade.cltamt)
                    return APIResultCodes.InvalidCollateral;
            }

            var bidg = await sys.Storage.FindTokenGenesisBlockAsync("", trade.biding);

            if(!LyraGlobal.GetOTCRequirementFromTicker(bidg.Ticker))
            {
                if(bidg.Ticker == "LYR")
                {
                    if (!chgs.Changes.ContainsKey(bidg.Ticker) ||
                        chgs.Changes[bidg.Ticker] != trade.pay + trade.cltamt ||
                            chgs.Changes.Count != 1)
                    {
                        return APIResultCodes.InvalidAmountToSend;
                    }
                }
                else
                {
                    if (!chgs.Changes.ContainsKey(bidg.Ticker) ||
                        chgs.Changes[bidg.Ticker] != trade.pay ||
                        chgs.Changes["LYR"] != trade.cltamt ||
                            chgs.Changes.Count != 2)
                    {
                        return APIResultCodes.InvalidAmountToSend;
                    }
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

        async Task<TransactionBlock?> SendTokenFromOrderToTradeAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            var trade = JsonConvert.DeserializeObject<UniTrade>(send.Tags["data"]);

            // send token from order to trade
            var lastblock = await sys.Storage.FindLatestBlockAsync(trade.orderId) as TransactionBlock;

            var keyStr = $"{send.Hash.Substring(0, 16)},{trade.offering},{send.AccountID}";
            var AccountId = Base58Encoding.EncodeAccountId(Encoding.ASCII.GetBytes(keyStr).Take(64).ToArray());
            context.Env.Add("tradeid", AccountId);

            return await TransactionOperateAsync(sys, send.Hash, lastblock,
                () => lastblock.GenInc<UniOrderSendBlock>(),
                () => context.State,
                (b) =>
                {
                    // send
                    b.DestinationAccountId = AccountId;

                    // IUniTrade
                    var nextOdr = b as IUniOrder;
                    // send collateral with the rito of token
                    // basically we devide token with collateral by the same rito.
                    var rito = trade.amount / ((IUniOrder)lastblock).Order.amount;

                    nextOdr.Order = nextOdr.Order.With(new
                    {
                        amount = ((IUniOrder)lastblock).Order.amount - trade.amount,
                        cltamt = Math.Round(((IUniOrder)lastblock).Order.cltamt * (1 - rito), 8)
                    });

                    if (((IUniOrder)lastblock).UOStatus == UniOrderStatus.Open)
                        nextOdr.UOStatus = UniOrderStatus.Partial;

                    // don't set to close. it will be set to close when the trade is closed.
                    //nextOdr.UOStatus = ((IUniOrder)lastblock).Order.amount - trade.amount == 0 ?
                    //    UniOrderStatus.Closed : UniOrderStatus.Partial;

                    // calculate balance
                    var dict = lastblock.Balances.ToDecimalDict();
                    dict[trade.offering] -= trade.amount;
                    dict[LyraGlobal.OFFICIALTICKERCODE] -= Math.Round(((IUniOrder)lastblock).Order.cltamt * rito, 8);
                    b.Balances = dict.ToLongDict();
                });
        }

        /*        async Task<TransactionBlock> SendTokenFromDaoToOrderAsync(DagSystem sys, LyraContext context)
                {
                    var trade = JsonConvert.DeserializeObject<UniTrade>(send.Tags["data"]);

                    var lastblock = await sys.Storage.FindLatestBlockAsync(trade.daoId) as TransactionBlock;

                    return await TransactionOperateAsync(sys, send.Hash, lastblock,
                        () => lastblock.GenInc<DaoSendBlock>(),
                        () => context.State,
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

                async Task<TransactionBlock> OrderReceiveCryptoAsync(DagSystem sys, LyraContext context)
                {
                    var trade = JsonConvert.DeserializeObject<UniTrade>(send.Tags["data"]);
                    var lastblock = await sys.Storage.FindLatestBlockAsync(trade.orderId) as TransactionBlock;
                    var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);

                    return await TransactionOperateAsync(sys, send.Hash, lastblock,
                        () => lastblock.GenInc<UniOrderRecvBlock>(),
                        () => context.State,
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

        async Task<TransactionBlock?> TradeGenesisReceiveAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);
            var odrsnd = blocks.Last() as UniOrderSendBlock;

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
                SourceHash = odrsnd.Hash,

                // broker
                Name = "no name",
                OwnerAccountId = send.AccountID,
                RelatedTx = send.Hash,

                // Uni
                Trade = trade,
                Delivery = new DeliveryStatus(),
                UTStatus = UniTradeStatus.Open,
            };

            //string wfstr;
            //if (LyraGlobal.GetHoldTypeFromTicker(trade.biding) == HoldTypes.Token
            //    || LyraGlobal.GetHoldTypeFromTicker(trade.biding) == HoldTypes.NFT)

            //{
            //    // non OTC, set status directly to biding sent.
            //    Uniblock.UTStatus = UniTradeStatus.BidReceived;
            //    wfstr = WFState.Running.ToString();
            //}
            //else
            //{
            //    Uniblock.UTStatus = UniTradeStatus.Open;
            //    wfstr = WFState.Finished.ToString();
            //}
            // TODO: change workflow in other way          
            var odrSendPrev = await sys.Storage.FindBlockByHashAsync(odrsnd.PreviousHash) as TransactionBlock;
            var chgs = odrsnd.GetBalanceChanges(odrSendPrev);

            foreach(var chg in chgs.Changes)
            {
                Uniblock.Balances.Add(chg.Key, chg.Value.ToBalanceLong());
            }

            // set order collateral amount
            var rito = trade.amount / ((IUniOrder)odrSendPrev).Order.amount;
            (Uniblock as UniTradeGenesisBlock).OdrCltMmt = Math.Round(((IUniOrder)odrSendPrev).Order.cltamt * rito, 8).ToBalanceLong();

            Uniblock.AddTag(Block.MANAGEDTAG, context.State.ToString());

            // pool blocks are service block so all service block signed by leader node
            Uniblock.InitializeBlock(null, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            //Console.WriteLine($"trade genesis hash input: {Uniblock.GetHashInput()}");
            return Uniblock;
        }

        async Task<TransactionBlock?> SendTokenFromDaoToTradeAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            var trade = JsonConvert.DeserializeObject<UniTrade>(send.Tags["data"]);

            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);
            var tradgen = blocks.Last() as UniTradeGenesisBlock;

            var amounts = new Dictionary<string, decimal> { { trade.biding, trade.pay } };
            if(trade.biding == "LYR")
            {
                amounts["LYR"] += trade.cltamt;
            }
            else
            {
                amounts.Add("LYR", trade.cltamt);
            }

            return await TransSendAsync<DaoSendBlock>(sys, context.Send.Hash, trade.daoId, tradgen.AccountID,
                amounts, context.State);
        }

        async Task<TransactionBlock?> TradeReceiveTokenFromDaoAsync(DagSystem sys, LyraContext context)
        {
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(context.Send.Hash);
            var daosnd = blocks.Last() as DaoSendBlock;

            return await TransReceiveAsync<UniTradeRecvBlock>(sys, context.Send.Hash, daosnd, daosnd.DestinationAccountId, context.State, null);
        }

        protected async Task<TransactionBlock> SendCryptoProductFromTradeToBuyerAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);
            var lastblock = blocks.LastOrDefault() as TransactionBlock;


            //var trade = (lastblock as IUniTrade).Trade;
            //bool IsBidToken = !LyraGlobal.GetOTCRequirementFromTicker(trade.biding);
            //bool IsOfferToken = !LyraGlobal.GetOTCRequirementFromTicker(trade.offering);

            Console.WriteLine("Call SendCryptoProductFromTradeToBuyerAsync");
            return await TransactionOperateAsync(sys, send.Hash, lastblock,
                () => lastblock.GenInc<UniTradeSendBlock>(),
                () => context.State,
                (b) =>
                {
                    var trade = b as IUniTrade;

                    trade.UTStatus = UniTradeStatus.Closed;

                    b.DestinationAccountId = trade.OwnerAccountId;

                    b.Balances[trade.Trade.offering] -= trade.Trade.amount.ToBalanceLong();
                });
        }

        protected async Task<TransactionBlock> SendCollateralFromDAOToBuyerAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
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
                () => context.State,
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

        public static async Task<(decimal sellerFee, decimal buyerFee, decimal sellerNetworkFee, decimal buyerNetworkFee)> CalculateFeesAsync(DagSystem sys, LyraContext context)
        {
            //var tradelatest = await sys.Storage.FindLatestBlockAsync(send.DestinationAccountId) as TransactionBlock;
            //var trade = (tradelatest as IUniTrade).Trade;
            var trade = JsonConvert.DeserializeObject<UniTrade>(context.Send.Tags["data"]);

            // find dao by the time when order created.
            // first find order genesis block
            var odrgen = await sys.Storage.FindFirstBlockAsync(trade.orderId) as UniOrderGenesisBlock;
            var sndHash = odrgen.RelatedTx;
            var odrCreateSend = await sys.Storage.FindBlockByHashAsync(sndHash) as SendTransferBlock;

            var daoforodr = await sys.Storage.FindLatestBlockByTimeAsync(trade.daoId, odrCreateSend.TimeStamp) as IDao;

            return UniTradeFees.CalculateFees(daoforodr, trade);
        }

        //protected async Task<TransactionBlock> SealOrderAsync(DagSystem sys, LyraContext context)
        //{
        //    var send = context.Send;
        //    var unitrade = JsonConvert.DeserializeObject<UniTrade>(send.Tags["data"]);
        //    return await SealUniOrderAsync(sys, context, send.Hash, unitrade.orderId);
        //}

        //protected async Task<TransactionBlock> SendCollateralToSellerAsync(DagSystem sys, LyraContext context)
        //{
        //    var send = context.Send;
        //    var unitrade = JsonConvert.DeserializeObject<UniTrade>(send.Tags["data"]);
        //    return await SendCollateralToSellerAsync(sys, context, send.Hash, unitrade.orderId);
        //}

        // copied from '... GotPay'
        protected async Task<TransactionBlock?> SendPropsFromTradeToBuyerAsync(DagSystem sys, LyraContext context)
        {
            var sendBlock = context.Send;
            var lastblock = await sys.Storage.FindLatestBlockAsync(context.Env["tradeid"]) as TransactionBlock;

            // calculate fees
            var (sf, bf, snf, bnf) = await CalculateFeesAsync(sys, context);

            return await TransactionOperateAsync(sys, sendBlock.Hash, lastblock,
                () => lastblock.GenInc<UniTradeSendBlock>(),
                () => context.State,
                (b) =>
                {
                    var trade = b as IUniTrade;

                    (b as SendTransferBlock).DestinationAccountId = trade.OwnerAccountId;

                    b.Balances[trade.Trade.offering] -= trade.Trade.amount.ToBalanceLong();
                    b.Balances["LYR"] -= (trade.Trade.cltamt - bf - bnf).ToBalanceLong();
                });
        }

        protected async Task<TransactionBlock?> SendPropsFromTradeToSellerAsync(DagSystem sys, LyraContext context)
        {
            var sendBlock = context.Send;
            var lastblock = await sys.Storage.FindLatestBlockAsync(context.Env["tradeid"]) as TransactionBlock;

            // calculate fees
            var (sf, bf, snf, bnf) = await CalculateFeesAsync(sys, context);

            return await TransactionOperateAsync(sys, sendBlock.Hash, lastblock,
                () => lastblock.GenInc<UniTradeSendBlock>(),
                () => context.State,
                (b) =>
                {
                    var trade = b as IUniTrade;

                    (b as SendTransferBlock).DestinationAccountId = trade.Trade.orderOwnerId;

                    b.Balances[trade.Trade.biding] -= trade.Trade.pay.ToBalanceLong();
                    b.Balances["LYR"] -= (trade.OdrCltMmt.ToBalanceDecimal() - sf - snf).ToBalanceLong();
                });
        }

        protected async Task<TransactionBlock?> SendFeesToDaoAsync(DagSystem sys, LyraContext context)
        {
            var sendBlock = context.Send;
            var lastblock = await sys.Storage.FindLatestBlockAsync(context.Env["tradeid"]) as TransactionBlock;

            // calculate fees
            var (sf, bf, snf, bnf) = await CalculateFeesAsync(sys, context);

            return await TransactionOperateAsync(sys, sendBlock.Hash, lastblock,
                () => lastblock.GenInc<UniTradeSendBlock>(),
                () => context.State,
                (b) =>
                {
                    // pay network fees
                    b.FeeType = AuthorizationFeeTypes.Dynamic;
                    b.Fee = snf + bnf;

                    var trade = b as IUniTrade;

                    trade.UTStatus = UniTradeStatus.Closed;
                    (b as SendTransferBlock).DestinationAccountId = trade.Trade.daoId;

                    b.Balances["LYR"] = 0;
                });
        }
    }
}
