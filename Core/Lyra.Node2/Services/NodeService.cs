using Lyra.Exchange;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Lyra.Node2.Services
{
    public class NodeService : BackgroundService
    {
        private LyraConfig _config;

        public static MongoClient client;
        private static IMongoDatabase _db;
        private static IMongoCollection<ExchangeOrder> _queue;
        static AutoResetEvent _waitOrder;

        public NodeService(Microsoft.Extensions.Options.IOptions<LyraConfig> config)
        {
            _config = config.Value;
            _waitOrder = new AutoResetEvent(false);
            try
            {
                if (_db == null)
                {
                    client = new MongoClient(_config.DBConnect);
                    _db = client.GetDatabase("Lyra");
                }

                _queue = _db.GetCollection<ExchangeOrder>("queuedDexOrders");
            }
            catch (Exception ex)
            {
                throw new Exception("Can find or create mongo database.", ex);
            }
        }

        public static async Task<CancelKey> AddOrderAsync(TokenTradeOrder order)
        {
            order.CreatedTime = DateTime.Now;
            var item = new ExchangeOrder()
            {
                Order = order,
                CanDeal = true,
                State = DealState.Placed,
                ClientIP = null
            };
            await _queue.InsertOneAsync(item);
            _waitOrder.Set();

            var key = new CancelKey()
            {
                State = OrderState.Placed,
                Key = item.Id.ToString()
            };
            return key;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {     
            while (!stoppingToken.IsCancellationRequested)
            {
                // do work
                if(_waitOrder.WaitOne(1000))
                {
                    // has new order. do trade
                }
                else
                {
                    // no new order. do house keeping.

                }
                //if(_orders.Count > 0)
                //{
                //    TokenTradeOrder order;
                //    var ret = _orders.TryDequeue(out order);
                //    if(ret)
                //    {
                //        // trade? 
                        
                //    }
                //    continue;
                //}

                //_logger.LogCritical("Lyra Deal Engine: Trade, deal, make, take");
                await Task.Delay(1000);
            }
        }

        public async Task<List<ExchangeOrder>> GetActiveOrders(string tokenName)
        {
            return await _queue.Find(a => a.CanDeal && a.Order.TokenName == tokenName).ToListAsync();
        }

        public async Task<ExchangeOrder[]> GetQueuedOrdersAsync()
        {
            var finds = await _queue.FindAsync(a => a.CanDeal);
            var fl = await finds.ToListAsync();
            return fl.OrderBy(a => a.Order.CreatedTime).ToArray();
        }

        public async Task<bool> LookforExecution(ExchangeOrder order)
        {
            var builder = Builders<ExchangeOrder>.Filter;
            var filter = builder.Eq("ToToken", order.Order.TokenName)
                & builder.Gte("Price", (Decimal)(1 / order.Order.Price));
            var matches = await _queue.Find<ExchangeOrder>(filter).ToListAsync();
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

        private async Task DealAsync()
        {
            //// for each order
            //// start mongodb session/transaction
            //// verify balance
            //// lookfor dealer
            //// make changes
            //// commit transaction
            //var orders = await _dbCtx.GetQueuedOrdersAsync();
            //if(orders.Length > 1)    // must have at least two orders
            //{
            //    for(int i = 1; i < orders.Length; i++)
            //    {
            //        for(int j = 0; j < i; j++)
            //        {
            //            if(orders[j].Order.BuySellType != )
            //        }
            //    }
            //}
        }

        private async Task send(string tokenName)
        {
            var excOrders = (await GetActiveOrders(tokenName)).OrderByDescending(a => a.Order.Price);
            var sellOrders = excOrders.Where(a => a.Order.BuySellType == OrderType.Sell)
                .GroupBy(a => a.Order.Price)
                .Select(a => new KeyValuePair<Decimal, Decimal>(a.Key, a.Sum(x => x.Order.Amount))).ToList();
            NotifyService.Notify("", Core.API.NotifySource.Dex, "SellOrders", JsonConvert.SerializeObject(sellOrders));

            var buyOrders = excOrders.Where(a => a.Order.BuySellType == OrderType.Buy)
                .GroupBy(a => a.Order.Price)
                .Select(a => new KeyValuePair<Decimal, Decimal>(a.Key, a.Sum(x => x.Order.Amount))).ToList();
            NotifyService.Notify("", Core.API.NotifySource.Dex, "BuyOrders", JsonConvert.SerializeObject(buyOrders));

        }
    }
}
