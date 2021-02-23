using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Noded.Services;
using StreamJsonRpc;
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
    public class JsonRpcServer : IDisposable
    {
        public JsonRpc RPC { get; set; }
        INodeAPI _node;
        INodeTransactionAPI _trans;

        string _monitorAccountId;

        public JsonRpcServer(INodeAPI node, INodeTransactionAPI trans)
        {
            _node = node;
            _trans = trans;

            NodeService.Dag.OnNewBlock += NewBlockMonitor;
        }

        public void NewBlockMonitor(Block block, Block prevBlock)
        {
            if(block is SendTransferBlock send && send.DestinationAccountId == _monitorAccountId)
            {
                var chgs = send.GetBalanceChanges(prevBlock as TransactionBlock);
                var recvInfo = new Receiving
                {
                    sendHash = send.Hash,
                    from = send.AccountID,
                    funds = chgs.Changes
                };
                Notify?.Invoke(this, recvInfo);
            }
        }

        // group hand shake
        public async Task<ApiStatus> Status(string version, string networkid)
        {
            var clientVer = new Version(version);
            if (LyraGlobal.NODE_VERSION > clientVer)
                throw new Exception("Client version too low. Need upgrade.");

            var syncState = await _node.GetSyncState();

            return new ApiStatus
            {
                version = LyraGlobal.NODE_VERSION.ToString(),
                networkid = syncState.NetworkID,
                synced = syncState.Status.state == Data.API.BlockChainState.Almighty
                    || syncState.Status.state == Data.API.BlockChainState.Engaging
            };
        }

        // group wallet
        public async Task<BalanceResult> Balance(string accountId)
        {
            var blockResult = await _node.GetLastBlock(accountId);
            var anySendResult = await _node.LookForNewTransfer2(accountId, null);
            
            if (blockResult.Successful())
            {
                var block = blockResult.GetBlock() as TransactionBlock;                               

                return new BalanceResult
                {
                    balance = block.Balances.ToDecimalDict(),
                    unreceived = anySendResult.Successful()
                };
            }
            else if(blockResult.ResultCode == APIResultCodes.BlockNotFound)
            {
                return new BalanceResult
                {
                    balance = null,
                    unreceived = anySendResult.Successful()
                };
            }

            throw new Exception("Can't get latest block for account.");
        }
        public async Task<BalanceResult> Receive(string accountId)
        {
            var klWallet = new KeylessWallet(accountId, (msg) =>
            {
                var result3 = RPC.InvokeAsync<string>("Sign", new object[] { msg });
                return result3.GetAwaiter().GetResult();
            }, _node, _trans);

            var result = await klWallet.ReceiveAsync();
            if(result == APIResultCodes.Success)
            {
                return await Balance(accountId);
            }
            else
            {
                throw new Exception(result.ToString());
            }
        }
        public async Task<BalanceResult> Send(string accountId, decimal amount, string destAccount, string ticker)
        {
            var klWallet = new KeylessWallet(accountId, (msg) =>
            {
                var result3 = RPC.InvokeAsync<string>("Sign", new object[] { msg });
                return result3.GetAwaiter().GetResult();
            }, _node, _trans);

            var result = await klWallet.SendAsync(amount, destAccount, ticker);
            if (result == APIResultCodes.Success)
            {
                return await Balance(accountId);
            }
            else
            {
                throw new Exception(result.ToString());
            }
        }
        public void Monitor(string accountId)
        {
            _monitorAccountId = accountId;
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

        public void Dispose()
        {
            NodeService.Dag.OnNewBlock -= NewBlockMonitor;
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
        public bool unreceived { get; set; }
    }
    public class ApiStatus
    {
        public string version { get; set; }
        public string networkid { get; set; }
        public bool synced { get; set; }
    }
    public class Receiving
    {
        public string from { get; set; }
        public string sendHash { get; set; }
        public Dictionary<string, decimal> funds { get; set; }
    }
}
