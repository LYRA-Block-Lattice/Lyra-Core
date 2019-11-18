using Lyra.Exchange;
using LyraLexWeb.Services;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LyraLexWeb.Common
{
    public class ExchangeHub : Hub
    {
        public ExchangeHub()
        {
            
        }
        //public async Task SendOrder(string orderJson)
        //{
        //    //Console.WriteLine(orderJson);
        //    var order = JsonConvert.DeserializeObject<TokenTradeOrder>(orderJson);

        //    CancelKey key;
        //    if (!order.VerifySignature(order.AccountID))
        //    {
        //        key = new CancelKey() { Key = string.Empty, State = OrderState.BadOrder };
        //    }
        //    else
        //    {
        //        key = await dbCtx.AddOrder(Context.GetHttpContext().Request, order);

        //        await FetchOrders(order.TokenName);

        //    }
        //    await Clients.All.SendAsync("UserOrder", JsonConvert.SerializeObject(key));
        //}

        public async Task FetchOrders(string tokenName)
        {
        }
    }
}
