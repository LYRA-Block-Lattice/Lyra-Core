using Lyra.Client.RPC;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LyraLexWeb.Services
{
    public class LyraRpcContext
    {
        RPCClient _rpc;
        public LyraRpcContext(IOptions<MongodbConfig> configs)
        {
            // the rpcclient only connect to localhost now.
            try
            {
                _rpc = new RPCClient(Guid.NewGuid().ToString());
            }
            catch(Exception ex)
            {

            }
        }

        public async Task<int> GetHeight()
        {
            var ret = await _rpc.GetSyncHeight();
            if (ret.ResultCode == Lyra.Core.API.APIResultCodes.Success)
                return ret.Height;
            else
                throw new Exception(ret.ResultMessage);
        }
    }
}
