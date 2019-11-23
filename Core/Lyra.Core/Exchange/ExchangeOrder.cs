using Lyra.Core.Blocks;
using Lyra.Exchange;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Lyra.Exchange
{
    public enum DealState { Invalid, Placed, Queued, Executed, PartialExecuted, Canceled };
    public class ExchangeOrder
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string ExchangeAccountId { get; set; }
        public IPAddress ClientIP { get; set; }

        public bool CanDeal { get; set; }

        public DealState State { get; set; }

        public TokenTradeOrder Order { get; set; }
    }

    public class ExchangeAccount
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public string AssociatedToAccountId { get; set; }
        public string AccountId { get; set; }
        public string PrivateKey { get; set; }
        public bool IsBalancePending { get; set; }

        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfArrays)]
        public Dictionary<string, decimal> Balance { get; set; }

        public ExchangeAccount()
        {
            Balance = new Dictionary<string, decimal>();
        }
    }
}
