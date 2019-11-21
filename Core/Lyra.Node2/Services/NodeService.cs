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
        private static IMongoCollection<ExchangeOrder> _finished;
        static AutoResetEvent _waitOrder;

        public NodeService(Microsoft.Extensions.Options.IOptions<LyraConfig> config)
        {
            _config = config.Value;
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
                Key = item.Id.ToString(),
                Order = order
            };
            return key;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _waitOrder = new AutoResetEvent(false);
            try
            {
                if (_db == null)
                {
                    client = new MongoClient(_config.DBConnect);
                    _db = client.GetDatabase("Dex");

                    _queue = _db.GetCollection<ExchangeOrder>("queuedDexOrders");
                    _finished = _db.GetCollection<ExchangeOrder>("finishedDexOrders");
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Can find or create mongo database.", ex);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                // do work
                if (_waitOrder.WaitOne(1000))
                {
                    _waitOrder.Reset();
                    // has new order. do trade
                    var changedTokens = new List<string>();

                    var placed = await GetNewlyPlacedOrdersAsync();
                    for (int i = 0; i < placed.Length; i++)
                    {
                        var curOrder = placed[i];

                        if (!changedTokens.Contains(curOrder.Order.TokenName))
                            changedTokens.Add(curOrder.Order.TokenName);

                        var matchedOrders = await LookforExecution(curOrder);
                        if (matchedOrders.Count() > 0)
                        {
                            foreach (var matchedOrder in matchedOrders)
                            {
                                // three conditions
                                if (matchedOrder.Order.Amount < curOrder.Order.Amount)
                                {
                                    //matched -> archive, cur -> partial
                                    matchedOrder.State = DealState.Executed;
                                    await _queue.DeleteOneAsync(Builders<ExchangeOrder>.Filter.Eq(o => o.Id, matchedOrder.Id));
                                    await _finished.InsertOneAsync(matchedOrder);

                                    curOrder.State = DealState.PartialExecuted;
                                    curOrder.Order.Amount -= matchedOrder.Order.Amount;

                                    continue;
                                }
                                else if (matchedOrder.Order.Amount == curOrder.Order.Amount)
                                {
                                    // matched -> archive, cur -> archive
                                    matchedOrder.State = DealState.Executed;
                                    await _queue.DeleteOneAsync(Builders<ExchangeOrder>.Filter.Eq(o => o.Id, matchedOrder.Id));
                                    await _finished.InsertOneAsync(matchedOrder);

                                    curOrder.State = DealState.Executed;
                                    await _queue.DeleteOneAsync(Builders<ExchangeOrder>.Filter.Eq(o => o.Id, curOrder.Id));
                                    await _finished.InsertOneAsync(curOrder);

                                    break;
                                }
                                else // matchedOrder.Order.Amount > curOrder.Order.Amount
                                {
                                    // matched -> partial, cur -> archive
                                    matchedOrder.State = DealState.PartialExecuted;
                                    matchedOrder.Order.Amount -= curOrder.Order.Amount;
                                    await _queue.ReplaceOneAsync(Builders<ExchangeOrder>.Filter.Eq(o => o.Id, matchedOrder.Id), matchedOrder);

                                    curOrder.State = DealState.Executed;
                                    await _queue.DeleteOneAsync(Builders<ExchangeOrder>.Filter.Eq(o => o.Id, curOrder.Id));
                                    await _finished.InsertOneAsync(curOrder);

                                    break;
                                }
                            }
                        }
                        else
                        {
                            // change placed to queued
                            var update = Builders<ExchangeOrder>.Update.Set(s => s.State, DealState.Queued);
                            await _queue.UpdateOneAsync(Builders<ExchangeOrder>.Filter.Eq(o => o.Id, curOrder.Id), update);
                        }
                    }
                    foreach (var tokenName in changedTokens)
                    {
                        // the update the client
                        await SendMarket(tokenName);
                    }
                }
                else
                {
                    // no new order. do house keeping.

                }
            }
        }

        public async Task<ExchangeOrder[]> GetNewlyPlacedOrdersAsync()
        {
            var finds = await _queue.FindAsync(a => a.State == DealState.Placed);
            var fl = await finds.ToListAsync();
            return fl.OrderBy(a => a.Order.CreatedTime).ToArray();
        }

        public async Task<ExchangeOrder[]> GetQueuedOrdersAsync()
        {
            var finds = await _queue.FindAsync(a => a.CanDeal);
            var fl = await finds.ToListAsync();
            return fl.OrderBy(a => a.Order.CreatedTime).ToArray();
        }

        public async Task<IOrderedEnumerable<ExchangeOrder>> LookforExecution(ExchangeOrder order)
        {
            var builder = Builders<ExchangeOrder>.Filter;
            var filter = builder.Eq("Order.TokenName", order.Order.TokenName)
                & builder.Ne("State", DealState.Placed)
                & builder.Eq("Order.BuySellType", order.Order.InversedOrderType);

            if (order.Order.BuySellType == OrderType.Buy)
                filter &= builder.Lte("Order.Price", order.Order.Price);
            else
                filter &= builder.Gte("Order.Price", order.Order.Price);

            var matches0 = await _queue.Find<ExchangeOrder>(filter).ToListAsync();

            if (order.Order.BuySellType == OrderType.Buy)
            {
                var matches = matches0.OrderBy(a => a.Order.Price);
                return matches;
            }
            else
            {
                var matches = matches0.OrderByDescending(a => a.Order.Price);
                return matches;
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

        public static async Task<List<ExchangeOrder>> GetActiveOrders(string tokenName)
        {
            return await _queue.Find(a => a.CanDeal && a.Order.TokenName == tokenName).ToListAsync();
        }

        public static async Task SendMarket(string tokenName)
        {
            var excOrders = (await GetActiveOrders(tokenName)).OrderByDescending(a => a.Order.Price);
            var sellOrders = excOrders.Where(a => a.Order.BuySellType == OrderType.Sell)
                .GroupBy(a => a.Order.Price)
                .Select(a => new KeyValuePair<Decimal, Decimal>(a.Key, a.Sum(x => x.Order.Amount))).ToList();

            var buyOrders = excOrders.Where(a => a.Order.BuySellType == OrderType.Buy)
                .GroupBy(a => a.Order.Price)
                .Select(a => new KeyValuePair<Decimal, Decimal>(a.Key, a.Sum(x => x.Order.Amount))).ToList();

            var orders = new Dictionary<string, List<KeyValuePair<Decimal, Decimal>>>();
            orders.Add("SellOrders", sellOrders);
            orders.Add("BuyOrders", buyOrders);

            NotifyService.Notify("", Core.API.NotifySource.Dex, "Orders", tokenName, JsonConvert.SerializeObject(orders));
        }
    }
}
