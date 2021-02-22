using AustinHarris.JsonRpc;
using System;
using System.Collections.Generic;
using System.Text;

namespace Client.CLI
{
    /*
     * 
    [create/restore/open] wallet, send token, receive, get balance
    get transaction history
    liquidate pool add/remove, token swap
     * 
     * */
    public class WalletServer : JsonRpcService
    {
        [JsonRpcMethod]
        private int add(int i)
        {
            return i + 1;
        }
    }
}
