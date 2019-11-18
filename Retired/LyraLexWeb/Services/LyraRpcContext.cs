using Grpc.Net.Client;
using Lyra.Client.Lib;
using Lyra.Core.API;
using Lyra.Core.Protos;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LyraLexWeb.Services
{
    public class LyraRpcContext
    {
        LyraRpcClient _rpc;
        public LyraRpcContext(IOptions<MongodbConfig> configs)
        {
            // the rpcclient only connect to localhost now.
            try
            {
                var channel = GrpcChannel.ForAddress("https://localhost:5001");
                _rpc = new LyraRpcClient(channel);
            }
            catch(Exception ex)
            {

            }
        }

        public async Task<int> GetHeight()
        {
            var ret = await _rpc.GetSyncHeightAsync(new Lyra.Core.Protos.SyncHeightRequest());
            if (ret.ResultCode == APIResultCodes.Success)
                return ret.Height;
            else
                throw new Exception(ret.ResultCode.ToString());
        }
    }
}
