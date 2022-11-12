using Lyra.Core.API;
using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API.WorkFlow.UniMarket
{
    public enum UniTradeStatus
    {
        Open,
        MoneySent,
        MoneyReceived,
        PropReleased,
        Closed,
        Dispute,
        DisputeClosed,
        Canceled
    };

    /// <summary>
    /// 
    /// </summary>
    public class UniTrade
    {
        // data
        public string daoId { get; set; }   // DAO account ID
        public string dealerId { get; set; }
        public string orderId { get; set; }   // Order account ID
        public string orderOwnerId { get; set; } // order's owner account ID
        public TradeDirection dir { get; set; }

        public HoldTypes propType { get; set; }
        /// <summary>
        /// property token genesis to sell
        /// </summary>
        public string propHash { get; set; }

        public HoldTypes moneyType { get; set; }
        /// <summary>
        /// moeny token genesis to buy
        /// </summary>
        public string moneyHash { get; set; }

        /// <summary>
        /// price in specified money type, fiat or token
        /// </summary>
        public decimal price { get; set; }

        /// <summary>
        /// always crypto
        /// </summary>
        public decimal amount { get; set; }
        public decimal cltamt { get; set; }

        /// <summary>
        /// always fiat
        /// </summary>
        public decimal pay { get; set; }
        public string payVia { get; set; }

        public override bool Equals(object obOther)
        {
            if (null == obOther)
                return false;

            if (ReferenceEquals(this, obOther))
                return true;

            if (GetType() != obOther.GetType())
                return false;

            var ob = obOther as UniTrade;
            return daoId == ob.daoId &&
                dealerId == ob.dealerId &&
                orderId == ob.orderId &&
                orderOwnerId == ob.orderOwnerId &&
                dir == ob.dir &&
                propType == ob.propType &&
                propHash == ob.propHash &&
                moneyType == ob.moneyType &&
                moneyHash == ob.moneyHash &&
                price == ob.price &&
                amount== ob.amount &&
                cltamt== ob.cltamt &&
                pay == ob.pay &&
                payVia == ob.payVia;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(HashCode.Combine(daoId, dealerId, orderId, orderOwnerId, dir, propType, propHash),
                HashCode.Combine(moneyType, moneyHash, price, amount, cltamt, payVia, dealerId));
        }

        public string GetExtraData(Block block)
        {
            string extraData = "";
            extraData += daoId + "|";
            extraData += $"{dealerId}|";
            extraData += $"{orderId}|";
            extraData += $"{orderOwnerId}|";
            extraData += $"{dir}|";
            extraData += $"{propType}|";
            extraData += $"{propHash}|";
            extraData += $"{moneyType}|";
            extraData += $"{moneyHash}|";
            extraData += $"{price.ToBalanceLong()}|";
            extraData += $"{amount.ToBalanceLong()}|";
            extraData += $"{cltamt.ToBalanceLong()}|";
            extraData += $"{pay.ToBalanceLong()}|";
            extraData += $"{payVia}|";
            return extraData;
        }

        public override string ToString()
        {
            string result = base.ToString();
            result += $"DAO ID: {daoId}\n";
            result += $"Dealer ID: {dealerId}\n";
            result += $"Order ID: {orderId}\n";
            result += $"Order Owner ID: {orderOwnerId}\n";
            result += $"Direction: {dir}\n";
            result += $"Property Type: {propType}\n";
            result += $"Property Hash: {propHash}\n";
            result += $"Money Type: {moneyType}\n";
            result += $"Money Hash: {moneyHash}\n";
            result += $"Price: {price}\n";
            result += $"Amount: {amount}\n";
            result += $"Buyer Collateral: {cltamt} {LyraGlobal.OFFICIALTICKERCODE}\n";
            result += $"Pay: {pay} {moneyType}";
            result += $"Pay Via: {payVia}\n";
            return result;
        }
    }
}
