using LyraLexWeb.Services;
using LyraLexWeb.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lyra.Exchange;
using Newtonsoft.Json;
using Microsoft.AspNetCore.SignalR;

namespace LyraLexWeb.Common
{
    public class LyraDealEngine : BackgroundService
    {
        private readonly ILogger<LyraDealEngine> _logger;
        private MongodbContext _dbCtx;
        private LyraRpcContext _rpc;
        private ExchangeHub _hub;

        public LyraDealEngine(
            ILogger<LyraDealEngine> logger,
            MongodbContext ctx,
            LyraRpcContext lyraRpc,
            ExchangeHub hub
            )
        {
            _logger = logger;
            _dbCtx = ctx;
            _rpc = lyraRpc;
            _hub = hub;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // check new orders


                //_logger.LogCritical("Lyra Deal Engine: Trade, deal, make, take");
                await Task.Delay(10000);
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
            var excOrders = (await _dbCtx.GetActiveOrders(tokenName)).OrderByDescending(a => a.Order.Price);
            var sellOrders = excOrders.Where(a => a.Order.BuySellType == OrderType.Sell)
                .GroupBy(a => a.Order.Price)
                .Select(a => new KeyValuePair<Decimal, Decimal>(a.Key, a.Sum(x => x.Order.Amount))).ToList();
            await _hub.Clients.All.SendAsync("SellOrders", JsonConvert.SerializeObject(sellOrders));

            var buyOrders = excOrders.Where(a => a.Order.BuySellType == OrderType.Buy)
                .GroupBy(a => a.Order.Price)
                .Select(a => new KeyValuePair<Decimal, Decimal>(a.Key, a.Sum(x => x.Order.Amount))).ToList();
            await _hub.Clients.All.SendAsync("BuyOrders", JsonConvert.SerializeObject(buyOrders));

        }
    }
}
