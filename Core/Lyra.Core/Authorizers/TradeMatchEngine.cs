﻿using System.Collections.Generic;
using System.Threading.Tasks;

using Lyra.Core.Blocks;
using Lyra.Core.Accounts;
using System;
using Lyra.Core.API;

namespace Lyra.Core.Authorizers
{
    /*
    public class TradeMatchEngine
    {
        protected IAccountCollectionAsync _AccountCollection { get; set; }
        //protected ServiceAccount _ServiceAccount { get; set; }
        //protected Dictionary<string, TradeOrderBlock> ActiveSellOrders { get; set; }
        //protected Dictionary<string, TradeOrderBlock> ActiveBuyOrders { get; set; }

        public TradeMatchEngine(IAccountCollectionAsync accountCollection)
        {
            _AccountCollection = accountCollection;
            //_ServiceAccount = serviceAccount;
            //ActiveSellOrders = new Dictionary<string, TradeOrderBlock>();
            //ActiveBuyOrders = new Dictionary<string, TradeOrderBlock>();
            //LoadOrders();
        }

        //public void AddOrder(TradeOrderBlock order)
        //{
        //    if (order.OrderType == TradeOrderTypes.Sell)
        //        ActiveSellOrders.Add(order.Hash, order);
        //    else
        //        ActiveBuyOrders.Add(order.Hash, order);
        //}

        //public void RemoveOrder(TradeOrderBlock order)
        //{
        //    if (order.OrderType == TradeOrderTypes.Sell)
        //        ActiveSellOrders.Remove(order.Hash);
        //    else
        //        ActiveBuyOrders.Remove(order.Hash);
        //}

        public async Task<TradeBlock> MatchAsync(TradeOrderBlock order)
        {
            // Currently we only support nonfungible trades, with one trade per order (MaxQuantity = 1).
            // An order becomes inactive after first trade/execution.
            if (order.MaxQuantity != 1)
                return null;

            // not implemented yet for matching sell orders
            if (order.OrderType == TradeOrderTypes.Sell)
                return null;

            var active_sellorders = await getActiveSellOrdersAsync(order.BuyTokenCode, order.SellTokenCode);

            if (active_sellorders == null)
                return null;

            foreach (TradeOrderBlock sellorder in active_sellorders)
            {
                if (order.BuyTokenCode == sellorder.SellTokenCode &&
                    order.SellTokenCode == sellorder.BuyTokenCode &&
                    order.TradeAmount <= sellorder.TradeAmount &&
                    order.TradeAmount >= sellorder.MinTradeAmount &&
                    order.Price >= sellorder.Price)
                {
                    //int sell_token_precision = FindTokenPrecision(order.SellTokenCode);
                    //if (sell_token_precision < 0)
                    //    continue;

                    //int buy_token_precision = FindTokenPrecision(order.BuyTokenCode);
                    //if (buy_token_precision < 0)
                    //    continue;

                    var trade = new TradeBlock
                    {
                        AccountID = order.AccountID,
                        SellTokenCode = order.SellTokenCode,
                        BuyTokenCode = order.BuyTokenCode,
                        BuyAmount = order.TradeAmount,
                        FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                    };

                    if (sellorder.CoverAnotherTradersFee)
                    {
                        trade.Fee = 0;
                        trade.FeeType = AuthorizationFeeTypes.NoFee;
                    }
                    else
                    if (sellorder.AnotherTraderWillCoverFee)
                    {
                        trade.Fee = (await _AccountCollection.GetLastServiceBlockAsync()).TradeFee * 2;
                        trade.FeeType = AuthorizationFeeTypes.BothParties;
                    }
                    else
                    {
                        trade.Fee = (await _AccountCollection.GetLastServiceBlockAsync()).TradeFee;
                        trade.FeeType = AuthorizationFeeTypes.Regular;
                    }

                    // We take the seller's price since it can be lower that the one offered by the buyer.
                    // If it is really lower, the Trade block should take into account the fact that the buyers pays less than expected.
                    // It is achieved by getting a "change" to the balance of SellToken.
                    // The Authorizer should also take this difference into account as the TransactionInfo and SellAmount can be different.
                    // It can be validated by formula: Original Buy Order's MaxAmount * Price = Trade.SellAmount + "Change"
                    //decimal real_price = sellorder.Price / (decimal)Math.Pow(10, sell_token_precision);
                    //decimal real_buy_amount = trade.BuyAmount / (decimal)Math.Pow(10, buy_token_precision);
                    //trade.SellAmount = (long)(real_price * real_buy_amount * (decimal)Math.Pow(10, sell_token_precision));
                    trade.SellAmount = order.TradeAmount * sellorder.Price;
                    trade.Balances = new Dictionary<string, long>();

                    trade.DestinationAccountId = sellorder.AccountID;
                    trade.TradeOrderId = sellorder.Hash;

                    return trade;
                }

            }

            return null;
        }

        /// <summary>
        /// <param name="SellTokenCode">
        /// If SellToken is not specified (null or empty or *), orders for all sell tokens will be included.
        /// </param>
        /// <param name="BuyTokenCode">
        /// If BuyToken is not specified (null or empty or *), orders for all buy tokens will be included.
        /// </param>
        /// <param name="OrderType">
        /// All, Sell Only, or Buy Only
        /// </param>
        /// <returns>
        // Returns the current list of active trade orders (not cancelled or not executed).
        // If neither SellToken nor BuyToken is specified, and OrderType is "All", it will return the  list of all active orders in the network.
        /// </returns>
        /// </summary>
        public async Task<List<TradeOrderBlock>> GetActiveTradeOrdersAsync(string SellTokenCode, string BuyTokenCode, TradeOrderListTypes OrderType)
        {
            var result_list = new List<TradeOrderBlock>();

            if (BuyTokenCode == "*")
                //BuyTokenCode = null;
                throw new Exception("Not supported");

            if (OrderType == TradeOrderListTypes.All || OrderType == TradeOrderListTypes.BuyOnly)
                throw new Exception("Not supported");

            
            return await getActiveSellOrdersAsync(SellTokenCode, BuyTokenCode);

            //if (OrderType == TradeOrderListTypes.All || OrderType == TradeOrderListTypes.SellOnly)
            //{
            //    foreach (var order in ActiveSellOrders.Values)
            //        if ((string.IsNullOrEmpty(SellTokenCode) || order.SellTokenCode == SellTokenCode) && (string.IsNullOrEmpty(BuyTokenCode) || order.BuyTokenCode == BuyTokenCode))
            //            result_list.Add(order);
            //}

            //if (OrderType == TradeOrderListTypes.All || OrderType == TradeOrderListTypes.BuyOnly)
            //{
            //    foreach (var order in ActiveBuyOrders.Values)
            //        if ((string.IsNullOrEmpty(SellTokenCode) || order.SellTokenCode == SellTokenCode) && (string.IsNullOrEmpty(BuyTokenCode) || order.BuyTokenCode == BuyTokenCode))
            //            result_list.Add(order);
            //}
            //return result_list;
        }

        private async Task<List<TradeOrderBlock>> getActiveSellOrdersAsync(string SellTokenCode, string BuyTokenCode)
        {
            var active_sellorders = new List<TradeOrderBlock>();
            List<TradeOrderBlock> sellorders;
            if (SellTokenCode == "*")
                sellorders = await _AccountCollection.GetSellTradeOrdersForTokenAsync(BuyTokenCode);
            else
                sellorders = await _AccountCollection.GetSellTradeOrdersAsync(SellTokenCode, BuyTokenCode);

            // now let's filter out all inactive orders
            foreach (var order in sellorders)
            {
                var cancel_block = await _AccountCollection.GetCancelTradeOrderBlockAsync(order.Hash);
                if (cancel_block != null)
                    continue;

                var execute_block = await _AccountCollection.GetExecuteTradeOrderBlockAsync(order.Hash);
                if (execute_block != null)
                    continue;
               
                active_sellorders.Add(order);
            }
            
            return active_sellorders;
        }

        //private int FindTokenPrecision(string token)
        //{
        //    int precision = -1;

        //    // see if we have this already in local storage
        //    var genesisBlock = _AccountCollection.FindTokenGenesisBlock(null, token);

        //    if (genesisBlock != null)
        //        precision = (int)genesisBlock.Precision;

        //    return precision;
        //}

        //private void LoadOrders()
        //{
        //    var orders = _AccountCollection.GetTradeOrderBlocks();
        //    var cancellations = _AccountCollection.GetTradeOrderCancellations();
        //    var executions = _AccountCollection.GetExecutedTradeOrderBlocks();

        //    // now let's filter out all inactive orders
        //    foreach (var order in orders)
        //        if (!cancellations.Contains(order.Hash) && !executions.Contains(order.Hash))
        //        {
        //            if (order.OrderType == TradeOrderTypes.Sell)
        //                ActiveSellOrders.Add(order.Hash, order);
        //            else
        //                ActiveBuyOrders.Add(order.Hash, order);
        //        }

        //}
    }*/
}