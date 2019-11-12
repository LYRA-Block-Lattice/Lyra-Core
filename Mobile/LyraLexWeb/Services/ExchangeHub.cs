using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LyraLexWeb.Services
{
    public class ExchangeHub : Hub
    {
        public async Task SendMessage(Decimal price, Decimal amount, bool IsBuy)
        {
            await Clients.All.SendAsync("OrderCreated", price, amount, IsBuy);
        }
    }
}
