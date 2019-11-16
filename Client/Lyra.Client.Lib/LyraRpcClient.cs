using Grpc.Net.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Client.Lib
{
    public class LyraRpcClient : Lyra.Core.Protos.LyraApi.LyraApiClient
    {
        public LyraRpcClient(GrpcChannel channel) : base(channel)
        {

        }
    }
}
