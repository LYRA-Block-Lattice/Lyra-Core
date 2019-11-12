using Lyra.Exchange;
using LyraLexWeb.Models;
using Microsoft.AspNetCore.Http;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LyraLexWeb.Common
{
    public class MongoUtils
    {
        private const string connStr = "mongodb://lexweb:j2CsADf4@localhost/lexweb";
        private static IMongoDatabase _db;
        private IMongoCollection<ExchangeOrder> _orders;

        public MongoUtils()
        {
            var client = new MongoClient(connStr);
            _db = client.GetDatabase("LexWeb");
            _orders = _db.GetCollection<ExchangeOrder>("exchangeOrders");
        }

        public async Task<CancelKey> AddOrder(HttpRequest req, TokenTradeOrder reqOrder)
        {
            // check order validation
            if (reqOrder.TokenName == null ||
                reqOrder.Price <= 0 || reqOrder.Amount <= 0)
                return new CancelKey() { State = OrderState.BadOrder };

            var item = new ExchangeOrder()
            {
                Order = reqOrder,
                CanDeal = true,
                State = DealState.Placed,
                ClientIP = req.HttpContext.Connection.RemoteIpAddress
            };
            await _orders.InsertOneAsync(item);

            var result = await LookforExecution(item);
            return new CancelKey()
            {
                State = result ?
                OrderState.Executed : OrderState.Placed,
                Key = item.Id.ToString()
            };
        }

        public async Task<List<ExchangeOrder>> GetActiveOrders()
        {
            return await _orders.Find(a => a.CanDeal).ToListAsync();
        }

        private async Task<bool> LookforExecution(ExchangeOrder order)
        {
            var builder = Builders<ExchangeOrder>.Filter;
            var filter = builder.Eq("ToToken", order.Order.TokenName)
                & builder.Gte("Price", (Decimal)(1 / order.Order.Price));
            var matches = await _orders.Find<ExchangeOrder>(filter).ToListAsync();
            if (matches.Any())
            {
                // we can make a deal
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
