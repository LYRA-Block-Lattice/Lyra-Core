using Lyra.Core.API;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Lyra.Node
{
    /*
     * Joseph Fowler, [22.02.21 16:41]
        I have a better idea. provides jsonrpc service not in the wallet cli but in the node. 
    first we get better performance, second, we get realtime notify on receiving transfer (by signalR). 
    third, we not clone the old wallet api on nodes but design a new high efficiency one, 
    perhaps 10x speed up. as for the cli, I'll simply add a api to allow reading 
    private key if have password. all wallet should use the new jsonrpc api provided by node. 
    the Lyra Broker project can be abandoned, for exchange, using the  same api is enough.
     */
    public class JsonRpcServer
    {
        INodeAPI _node;
        public JsonRpcServer(INodeAPI node)
        {
            _node = node;
        }
        // group hand shake
        public async Task<ApiStatus> Status(string version)
        {
            var syncState = await _node.GetSyncState();

            return new ApiStatus
            {
                version = LyraGlobal.NODE_VERSION.ToString(),
                synced = syncState.Status.state == Data.API.BlockChainState.Almighty
                    || syncState.Status.state == Data.API.BlockChainState.Engaging
            };
        }

        // group wallet
        public BalanceResult Balance(string accountId)
        {
            throw new NotImplementedException();
        }
        public void Receive(List<string> unreceiveTx)
        {
            throw new NotImplementedException();
        }
        public void Send()
        {

        }
        public void Monitor(string accountId)
        {

        }
        public void History(string accountId, long startTime, long endTime, int count)
        {

        }
        // group pool
        public void Pool(string token0, string token1)
        {

        }
        public void AddLiquidaty(string poolId)
        {

        }
        public void RemoveLiquidaty(string poolId)
        {

        }
        public void Swap(string poolId, string fromToken, long amountLong)  // need convert to decimal
        {

        }
        // group notification
        // server request to sign hash
        public event EventHandler<string> Sign;
        public event EventHandler<Receiving> Notify;

        // group cli
        // on cli only

        /// <summary>
        /// Occurs every second. Just for the heck of it.
        /// </summary>
        public event EventHandler<int> Tick;

        public int Add(int a, int b) => a + b;

        public async Task SendTicksAsync(CancellationToken cancellationToken)
        {
            int tickNumber = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000, cancellationToken);
                this.Tick?.Invoke(this, ++tickNumber);
            }
        }
    }

    public class TxInfo
    {
        public string from { get; set; }
        public string to { get; set; }
        public Dictionary<string, decimal> changes { get; set; }
    }
    public class BalanceResult
    {
        public Dictionary<string, decimal> balance { get; set; }
        public List<string> unreceivedTx { get; set; }
    }
    public class ApiStatus
    {
        public string version { get; set; }
        public bool synced { get; set; }
    }
    public class Receiving
    {
        public string from { get; set; }
        public string sendHash { get; set; }
    }
}
