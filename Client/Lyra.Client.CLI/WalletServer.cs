using AustinHarris.JsonRpc;
using System;
using System.Collections.Generic;
using System.Text;

namespace Client.CLI
{
    public class WalletServer : JsonRpcService
    {
        [JsonRpcMethod]
        private int add(int i)
        {
            return i + 1;
        }
    }
}
