using Lyra.Exchange;
using LyraLexWeb.Common;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LyraLexWeb.Services
{
    public class ExchangeHub : Hub
    {
        public async Task SendOrder(string orderJson)
        {
            //Console.WriteLine(orderJson);
            var order = JsonConvert.DeserializeObject<TokenTradeOrder>(orderJson);

            CancelKey key;
            if (!order.VerifySignature(order.AccountID))
            {
                key = new CancelKey() { Key = string.Empty, State = OrderState.BadOrder };
            }
            else
            {
                var dbCtx = new MongoUtils();
                
                key = await dbCtx.AddOrder(Context.GetHttpContext().Request, order);

                var excOrders = (await dbCtx.GetActiveOrders()).OrderByDescending(a => a.Order.Price);
                var sellOrders = excOrders.Where(a => a.Order.BuySellType == OrderType.Sell).Select(a => new KeyValuePair<Decimal, Decimal>(a.Order.Price, a.Order.Amount)).ToList();
                await Clients.All.SendAsync("SellOrders", JsonConvert.SerializeObject(sellOrders));

                var buyOrders = excOrders.Where(a => a.Order.BuySellType == OrderType.Buy).Select(a => new KeyValuePair<Decimal, Decimal>(a.Order.Price, a.Order.Amount)).ToList();
                await Clients.All.SendAsync("BuyOrders", JsonConvert.SerializeObject(buyOrders));
            }
        }
    }
}
