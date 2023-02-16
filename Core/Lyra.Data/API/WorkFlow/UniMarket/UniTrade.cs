using Lyra.Core.API;
using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API.WorkFlow.UniMarket
{
    /// <summary>
    /// 
    /// </summary>
    public class UniTrade
    {
        // data
        public string daoId { get; set; } = null!;   // DAO account ID
        public string dealerId { get; set; } = null!;
        public string orderId { get; set; } = null!;   // Order account ID
        public string orderOwnerId { get; set; } = null!;// order's owner account ID

        public HoldTypes offby { get; set; }
        /// <summary>
        /// ticker to give
        /// </summary>
        public string offering { get; set; } = null!;

        public HoldTypes bidby { get; set; }
        /// <summary>
        /// ticker to get
        /// </summary>        
        public string biding { get; set; } = null!;

        /// <summary>
        /// price in specified money type, fiat or token
        /// </summary>
        public decimal price { get; set; }

        /// <summary>
        /// the equivalent price of offering properties count in LYR.
        /// fees are calculated by this value.
        /// </summary>
        public decimal eqprice { get; set; }

        /// <summary>
        /// always crypto
        /// </summary>
        public decimal amount { get; set; }
        public decimal cltamt { get; set; }

        /// <summary>
        /// always fiat
        /// </summary>
        public decimal pay { get; set; }
        public string payVia { get; set; } = null!;

        public override bool Equals(object? obOther)
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
                offby == ob.offby &&
                offering == ob.offering &&
                bidby == ob.bidby &&
                biding == ob.biding &&
                price == ob.price &&
                eqprice == ob.eqprice &&
                amount == ob.amount &&
                cltamt== ob.cltamt &&
                pay == ob.pay &&
                payVia == ob.payVia;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(HashCode.Combine(daoId, dealerId, orderId, orderOwnerId, offby, offering),
                HashCode.Combine(bidby, biding, price, amount, cltamt, payVia, dealerId),
                HashCode.Combine(eqprice)
                );
        }

        public string GetExtraData(Block block)
        {
            string extraData = "";
            extraData += daoId + "|";
            extraData += $"{dealerId}|";
            extraData += $"{orderId}|";
            extraData += $"{orderOwnerId}|";
            extraData += $"{offby}|";
            extraData += $"{offering}|";
            extraData += $"{bidby}|";
            extraData += $"{biding}|";
            extraData += $"{price.ToBalanceLong()}|";
            extraData += $"{amount.ToBalanceLong()}|";
            extraData += $"{cltamt.ToBalanceLong()}|";
            extraData += $"{pay.ToBalanceLong()}|";
            extraData += $"{payVia}|";

            if (block.Version >= 10)
                extraData += $"{eqprice.ToBalanceLong()}|";

            return extraData;
        }

        public override string ToString()
        {
            string result = "";// base.ToString();
            result += $"DAO ID: {daoId}\n";
            result += $"Dealer ID: {dealerId}\n";
            result += $"Order ID: {orderId}\n";
            result += $"Order Owner ID: {orderOwnerId}\n";
            result += $"Property Type: {offby}\n";
            result += $"Property Ticker: {offering}\n";
            result += $"Money Type: {bidby}\n";
            result += $"Money Ticker: {biding}\n";
            result += $"Price: {price}\n";
            result += $"Equivlent price in LYR: {eqprice}\n";
            result += $"Amount: {amount}\n";
            result += $"Buyer Collateral: {cltamt} {LyraGlobal.OFFICIALTICKERCODE}\n";
            result += $"Pay: {pay} {bidby}";
            result += $"Pay Via: {payVia}\n";
            return result;
        }
    }
}
