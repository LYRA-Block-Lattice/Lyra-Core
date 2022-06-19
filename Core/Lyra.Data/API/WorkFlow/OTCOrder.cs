using Lyra.Core.API;
using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API.WorkFlow
{
    public class OTCOrder
    {
        // data
        public string daoId { get; set; }   // DAO account ID
        public string dealerId { get; set; }
        public TradeDirection dir { get; set; }
        public string crypto { get; set; }
        public string fiat { get; set; }
        /// <summary>
        /// decide weather follow the market with a fixed commission
        /// </summary>
        public PriceType priceType { get; set; }
        /// <summary>
        /// price in specified fiat
        /// </summary>
        public decimal price { get; set; }
        /// <summary>
        /// 1 fiat in USD
        /// </summary>
        public decimal fiatPrice { get; set; }

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
        public string[] payBy { get; set; }
        /// <summary>
        /// 1 lyr in USD
        /// </summary>
        public decimal collateral { get; set; }

        /// <summary>
        /// the price of LYR in USD on the time order created.
        /// will be used to calcute fee.
        /// </summary>
        public decimal collateralPrice { get; set; }

        public override bool Equals(object obOther)
        {
            if (null == obOther)
                return false;

            if (object.ReferenceEquals(this, obOther))
              return true;

            if (this.GetType() != obOther.GetType())
                return false;

            var ob = obOther as OTCOrder;
            return daoId == ob.daoId &&
                dealerId == ob.dealerId &&
                dir == ob.dir &&
                crypto == ob.crypto &&
                fiat == ob.fiat &&
                priceType == ob.priceType &&
                amount == ob.amount &&
                collateral == ob.collateral &&
                price == ob.price &&
                fiatPrice == ob.fiatPrice &&
                limitMin == ob.limitMin &&
                limitMax == ob.limitMax &&
                collateralPrice == ob.collateralPrice &&
                Enumerable.SequenceEqual(payBy, ob.payBy);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(HashCode.Combine(daoId, dealerId, dir, crypto, fiat, price, fiatPrice, priceType),
                HashCode.Combine(amount, collateral, collateralPrice, limitMin, limitMax, payBy));
        }

        public string GetExtraData(Block parent)
        {
            string extraData = "";
            extraData += daoId + "|";
            if (parent.Version >= 9)
            {
                extraData += $"{dealerId}|";
            }
            extraData += $"{dir}|";
            extraData += $"{crypto}|";
            extraData += $"{fiat}|";
            extraData += $"{priceType}|";
            extraData += $"{price.ToBalanceLong()}|";
            extraData += $"{amount.ToBalanceLong()}|";
            extraData += $"{collateral.ToBalanceLong()}|";

            if(parent.Version >= 8)
            {
                extraData += $"{collateralPrice.ToBalanceLong()}|";
                extraData += $"{fiatPrice.ToBalanceLong()}|";
            }

            extraData += $"{limitMin}|";
            extraData += $"{limitMax}|";
            extraData += $"{string.Join(",", payBy)}|";
            return extraData;
        }

        public override string ToString()
        {
            string result = base.ToString();
            result += $"DAO ID: {daoId}\n";
            result += $"Dealer ID: {dealerId}\n";
            result += $"Direction: {dir}\n";
            result += $"Crypto: {crypto}\n";
            result += $"Fiat: {fiat}\n";
            result += $"Fiat Price (USD): {fiatPrice}\n";
            result += $"Price Type: {priceType}\n";
            result += $"Price: {price}\n";
            result += $"Amount: {amount}\n";
            result += $"Seller Collateral: {collateral}\n";
            result += $"Collateral Price (USD): {collateralPrice}\n";
            result += $"limitMin: {limitMin}\n";
            result += $"limitMax: {limitMax}\n";
            result += $"Pay By: {string.Join(", ", payBy)}\n";
            return result;
        }
    }
}
