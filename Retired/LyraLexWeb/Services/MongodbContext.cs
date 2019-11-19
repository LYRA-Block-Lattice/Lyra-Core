using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Lyra.Exchange;
using Microsoft.AspNetCore.Http;

namespace LyraLexWeb.Services
{
    public class MongodbContext
    {
        public MongoClient client { get; }
        public readonly IMongoDatabase Context;
        private static IMongoDatabase _db;

        private IMongoCollection<ExchangeOrder> _queue;
        private IMongoCollection<ExchangeOrder> _orders;
        
        public MongodbContext(IOptions<MongodbConfig> configs)
        {
            try
            {
                if(_db == null)
                {
                    client = new MongoClient(configs.Value.DatabasePath);
                    _db = client.GetDatabase("LexWeb");
                }
                Context = _db;

                _queue = _db.GetCollection<ExchangeOrder>("queuedOrders");
                _orders = _db.GetCollection<ExchangeOrder>("exchangeOrders");
            }
            catch (Exception ex)
            {
                throw new Exception("Can find or create LiteDb database.", ex);
            }
        }

        public async Task<CancelKey> AddOrder(HttpRequest req, TokenTradeOrder reqOrder)
        {
            // check order validation
            if (reqOrder.TokenName == null ||
                reqOrder.Price <= 0 || reqOrder.Amount <= 0)
                return new CancelKey() { State = OrderState.BadOrder };

            reqOrder.CreatedTime = DateTime.Now;
            var item = new ExchangeOrder()
            {
                Order = reqOrder,
                CanDeal = true,
                State = DealState.Placed,
                ClientIP = req.HttpContext.Connection.RemoteIpAddress
            };
            await _queue.InsertOneAsync(item);

            return new CancelKey()
            {
                State = OrderState.Placed,
                Key = item.Id.ToString()
            };
        }

        public async Task<List<ExchangeOrder>> GetQueuedOrders()
        {
            throw new NotImplementedException();
        }

        public async Task<List<ExchangeOrder>> GetActiveOrders(string tokenName)
        {
            return await _orders.Find(a => a.CanDeal && a.Order.TokenName == tokenName).ToListAsync();
        }

        public async Task<ExchangeOrder[]> GetQueuedOrdersAsync()
        {
            var finds = await _orders.FindAsync(a => a.CanDeal);
            var fl = await finds.ToListAsync();
            return fl.OrderBy(a => a.Order.CreatedTime).ToArray();
        }

        public async Task<bool> LookforExecution(ExchangeOrder order)
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
