using Lyra.Core.Blocks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using System;

namespace Lyra.Exchange
{
    public enum OrderType { Buy, Sell };
    public class TokenTradeOrder : SignableObject
    {
        public DateTime CreatedTime { get; set; }
        public string AccountID { get; set; }
        public OrderType BuySellType { get; set; }
        public string TokenName { get; set; }
        public string NetworkID { get; set; }
        [BsonRepresentation(BsonType.Decimal128)]
        public decimal Price { get; set; }
        [BsonRepresentation(BsonType.Decimal128)]
        public decimal Amount { get; set; }

        public override string GetHashInput()
        {
            return $"{NetworkID} {AccountID} {BuySellType} {TokenName} {JsonConvert.SerializeObject(Price)} {JsonConvert.SerializeObject(Amount)} {DateTimeToString(CreatedTime)}";
        }

        protected override string GetExtraData()
        {
            return "";
        }
    }
}