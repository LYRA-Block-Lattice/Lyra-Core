using Lyra.Core.Blocks;
using Lyra.Data.API;
using Lyra.Data.Crypto;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.API
{
    public class LyraJsonRPCClient : JsonRpcClientBase
    {
        Func<string, Task<string>> _signr;

        public LyraJsonRPCClient(string networkId, Func<string, Task<string>> signr)
        {
            NetworkId = networkId;
            _signr = signr;
        }

        protected override async Task<string> SignMessageAsync(string message)
        {
            return await _signr(message);
        }
    }
}
