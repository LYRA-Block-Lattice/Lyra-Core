using Lyra.Core.API;
using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API.WorkFlow
{
    // type
    public enum TradeDirection { Buy, Sell };
    public enum PriceType { Fixed, Float }
    public class OTCTrade
    {
        // data
        public string daoId { get; set; }   // DAO account ID
        public string dealerId { get; set; }
        public string orderId { get; set; }   // Order account ID
        public string orderOwnerId { get; set; } // order's owner account ID
        public TradeDirection dir { get; set; }
        public string crypto { get; set; }
        public string fiat { get; set; }
        public decimal price { get; set; }
        public decimal amount { get; set; }
        public decimal collateral { get; set; }
        public decimal pay { get; set; }
        public string payVia { get; set; }

        public override bool Equals(object obOther)
        {
            if (null == obOther)
                return false;

            if (object.ReferenceEquals(this, obOther))
              return true;

            if (this.GetType() != obOther.GetType())
                return false;

            var ob = obOther as OTCTrade;
            return daoId == ob.daoId &&
                dealerId == ob.dealerId &&
                orderId == ob.orderId &&
                orderOwnerId == ob.orderOwnerId &&
                dir == ob.dir &&
                crypto == ob.crypto &&
                fiat == ob.fiat &&
                price == ob.price &&
                amount == ob.amount &&
                collateral == ob.collateral &&
                pay == ob.pay &&
                payVia == ob.payVia;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(HashCode.Combine(daoId, orderId, orderOwnerId, dir, crypto, fiat, price, amount), 
                HashCode.Combine(collateral, pay, payVia, dealerId));
        }

        public string GetExtraData(Block block)
        {
            string extraData = "";
            extraData += daoId + "|";
            if (block.Version >= 9)
            {
                extraData += $"{dealerId}|";
            }
            extraData += $"{orderId}|";
            extraData += $"{orderOwnerId}|";
            extraData += $"{dir}|";
            extraData += $"{crypto}|";
            extraData += $"{fiat}|";
            extraData += $"{price.ToBalanceLong()}|";
            extraData += $"{amount.ToBalanceLong()}|";
            extraData += $"{collateral.ToBalanceLong()}|";
            if(block.Version >= 6)
            {
                extraData += $"{pay.ToBalanceLong()}|";
            }
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
            result += $"Crypto: {crypto}\n";
            result += $"Fiat: {fiat}\n";
            result += $"Price: {price}\n";
            result += $"Amount: {amount}\n";
            result += $"Buyer Collateral: {collateral} {LyraGlobal.OFFICIALTICKERCODE}\n";
            result += $"Pay: {pay} {fiat}";
            result += $"Pay Via: {payVia}\n";
            return result;
        }
    }
}
