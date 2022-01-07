using Lyra.Core.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API.WorkFlow
{
    // type
    public enum Direction { Buy, Sell };
    public enum PriceType { Fixed, Float }
    public class OTCTrade
    {
        // data
        public string daoid { get; set; }   // DAO account ID
        public string orderid { get; set; }   // Order account ID
        public Direction dir { get; set; }
        public string crypto { get; set; }
        public string fiat { get; set; }
        public PriceType priceType { get; set; }
        public decimal price { get; set; }
        public decimal amount { get; set; }

        public override bool Equals(object obOther)
        {
            if (null == obOther)
                return false;

            if (object.ReferenceEquals(this, obOther))
              return true;

            if (this.GetType() != obOther.GetType())
                return false;

            var ob = obOther as OTCTrade;
            return daoid == ob.daoid &&
                dir == ob.dir &&
                crypto == ob.crypto &&
                fiat == ob.fiat &&
                priceType == ob.priceType &&
                amount == ob.amount &&
                price == ob.price;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(daoid, dir, crypto, fiat, price, priceType, amount);
        }

        public string GetExtraData()
        {
            string extraData = "";
            extraData += daoid + "|";
            extraData += $"{dir}|";
            extraData += $"{crypto}|";
            extraData += $"{fiat}|";
            extraData += $"{priceType}|";
            extraData += $"{price.ToBalanceLong()}|";
            extraData += $"{amount.ToBalanceLong()}|";
            return extraData;
        }

        public override string ToString()
        {
            string result = base.ToString();
            result += $"DAO ID: {daoid}\n";
            result += $"Direction: {dir}\n";
            result += $"Crypto: {crypto}\n";
            result += $"Fiat: {fiat}\n";
            result += $"Price Type: {priceType}\n";
            result += $"Price: {price}\n";
            result += $"Amount: {amount}\n";
            return result;
        }
    }
}
