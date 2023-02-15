using Lyra.Core.API;
using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API.WorkFlow.UniMarket
{
    // about the fee
    // because anyone can sell anything to anyone to get any anything, we can't calculate fee pe 'market price'.
    // what we need to calculate a ratio from collateral LYR.
    // the collateral is like buying a assurance. user pay for it to guard the trade.
    // buying the confidence.
    public enum HoldTypes
    {
        Token,
        NFT,
        Fiat,        
        TOT,
        SVC,     
    }

    public class UniOrder
    {
        public string daoId { get; set; } = null!;   // DAO account ID
        public string dealerId { get; set; } = null!;

        public HoldTypes offerby { get; set; }
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
        /// price in specified biding type, fiat or token
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
        /// <summary>
        /// always fiat
        /// </summary>
        public decimal limitMin { get; set; }
        /// <summary>
        /// always fiat
        /// </summary>
        public decimal limitMax { get; set; }

        /// <summary>
        /// buyer paying methods, online or offline, token or fiat
        /// </summary>
        public string[] payBy { get; set; } = null!;

        /// <summary>
        /// the amount of collateral, always be LYR token
        /// including dao fees, network fees, and collateral for offering properties.
        /// </summary>
        public decimal cltamt { get; set; }

        public override bool Equals(object? obOther)
        {
            if (null == obOther)
                return false;

            if (ReferenceEquals(this, obOther))
                return true;

            if (GetType() != obOther.GetType())
                return false;

            var ob = obOther as UniOrder;
            if(ob == null)
                return false;

            return daoId == ob.daoId &&
                dealerId == ob.dealerId &&
                offerby == ob.offerby &&
                offering == ob.offering &&
                bidby == ob.bidby &&
                biding == ob.biding &&
                amount == ob.amount &&
                cltamt == ob.cltamt &&
                price == ob.price &&
                eqprice == ob.eqprice &&
                limitMin == ob.limitMin &&
                limitMax == ob.limitMax &&
                payBy.SequenceEqual(ob.payBy);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(HashCode.Combine(daoId, dealerId, offerby, offering, bidby, biding),
                HashCode.Combine(price, amount, cltamt, limitMin, limitMax, payBy, eqprice));
        }

        public string GetExtraData(Block parent)
        {
            string extraData = "";
            extraData += daoId + "|";
            extraData += $"{dealerId}|";
            extraData += $"{offerby}|";
            extraData += $"{offering}|";
            extraData += $"{bidby}|";
            extraData += $"{biding}|";
            extraData += $"{price.ToBalanceLong()}|";
            extraData += $"{amount.ToBalanceLong()}|";
            extraData += $"{cltamt.ToBalanceLong()}|";

            extraData += $"{limitMin}|";
            extraData += $"{limitMax}|";
            extraData += $"{string.Join(",", payBy)}|";

            if(parent.Version >= 10)
                extraData += $"{eqprice.ToBalanceLong()}|";

            return extraData;
        }

        public override string? ToString()
        {
            string? result = base.ToString();
            result += $"DAO ID: {daoId}\n";
            result += $"Dealer ID: {dealerId}\n";
            result += $"Property Type: {offerby}\n";
            result += $"Property Ticker: {offering}\n";
            result += $"Money Type: {bidby}\n";
            result += $"Money Ticker: {biding}\n";
            result += $"Price: {price}\n";
            result += $"Equivlent price in LYR: {eqprice}\n";
            result += $"Amount: {amount}\n";
            result += $"Seller Collateral: {cltamt}\n";
            result += $"limitMin: {limitMin}\n";
            result += $"limitMax: {limitMax}\n";
            result += $"Pay By: {string.Join(", ", payBy)}\n";
            return result;
        }
    }
}
