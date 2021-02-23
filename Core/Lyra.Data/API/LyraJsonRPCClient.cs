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
        string _networkId;
        SignHandler _signr;

        public LyraJsonRPCClient(string networkId, SignHandler signr)
        {
            _networkId = networkId;
            _signr = signr;
        }

        protected override string SignMessage(string message)
        {
            return _signr(message);
        }
    }
}
