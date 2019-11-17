using System;
using Lyra.Client.RPC;
using Lyra.Core.API;
using System.Threading.Tasks;

namespace Lyra.Node.API
{
    public static class RPC
    {
        static RPC()
        {
            Console.WriteLine("Starting Lyra Node WEB API RPC...");
        }

        static INodeAPI _RPC = null;

        static string _SyncHash = "";

        public static string SyncHash
        {
            get
            {
                lock (_SyncHash)
                {
                    return _SyncHash;
                }
            }

            set
            {
                lock (_SyncHash)
                {
                    _SyncHash = value;
                }
            }
        }

        public static INodeAPI Client
        {
            get
            {
                if (_RPC == null)
                    _RPC = new RPCClient(Guid.NewGuid().ToString());
                return _RPC;
            }
        }
    }
}
