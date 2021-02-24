using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
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

        private KeylessWallet CreateWallet(string accountId)
        {
            var klWallet = new KeylessWallet(accountId, (msg) =>
            {
                var result3 = RPC.InvokeAsync<string>("Sign", new object[] { msg });
                try
                {
                    return result3.GetAwaiter().GetResult();
                }
                catch
                {
                    return null;
                }
            }, _node, _trans);
            return klWallet;
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
            var klWallet = CreateWallet(accountId);

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
            var klWallet = CreateWallet(accountId);

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

        public async Task<BalanceResult> Token(string accountId, string name, string domain, decimal supply)
        {
            var klWallet = CreateWallet(accountId);

            var result = await klWallet.CreateTokenAsync(name, domain,
                "", 8, supply, true, "", "", "", ContractTypes.Cryptocurrency, null);
            if (result.ResultCode == APIResultCodes.Success)
            {
                return await Balance(accountId);
            }
            else
            {
                throw new Exception(result.ResultCode.ToString());
            }
        }

        public void Monitor(string accountId)
        {
            _monitorAccountId = accountId;
        }
        public async Task<List<TransactionDescription>> History(string accountId, long startTime, long endTime, int count)
        {
            var hists = await _node.SearchTransactions(accountId, startTime, endTime, count);
            if (hists.Successful())
                return hists.Transactions;
            else
                throw new Exception(hists.ResultCode.ToString());
        }
        // group pool
        public async Task<PoolInfo> Pool(string token0, string token1)
        {
            var poolResult = await _node.GetPool(token0, token1);
            if(poolResult.Successful() && poolResult.PoolAccountId != null)
            {
                var poolLatest = await _node.GetLastBlock(poolResult.PoolAccountId);
                if(poolLatest.Successful())
                {
                    var latestBlock = poolLatest.GetBlock() as TransactionBlock;
                    return new PoolInfo
                    {
                        poolId = poolResult.PoolAccountId,
                        height = latestBlock.Height,
                        token0 = poolResult.Token0,
                        token1 = poolResult.Token1,
                        balance = latestBlock.Balances.ToDecimalDict()
                    };
                }
            }

            throw new Exception("Failed to get pool");
        }
        public async Task<PoolInfo> CreatePool(string accountId, string token0, string token1)
        {
            var klWallet = CreateWallet(accountId);

            var result = await klWallet.CreateLiquidatePoolAsync(token0, token1);
            if (result.ResultCode == APIResultCodes.Success)
            {
                for(int i = 0; i < 30; i++)
                {
                    await Task.Delay(500);     // wait for the pool to be created.
                    try
                    {
                        return await Pool(token0, token1);
                    }
                    catch { }
                }
                throw new Exception("Pool was not created properly.");
            }
            else
            {
                throw new Exception(result.ToString());
            }
        }
        public async Task<SwapCalculator> PoolCalculate(string poolId, string swapFrom, decimal amount, decimal slippage)
        {
            var poolLatest = await NodeService.Dag.Storage.FindLatestBlockAsync(poolId) as TransactionBlock;
            var poolGenesis = await NodeService.Dag.Storage.FindFirstBlockAsync(poolId) as PoolGenesisBlock;
            
            if (poolLatest != null && poolGenesis != null && poolLatest.Hash != poolGenesis.Hash)
            {
                var cal = new SwapCalculator(poolGenesis.Token0, poolGenesis.Token1,
                    poolLatest, swapFrom, amount, slippage);
                return cal;
            }

            throw new Exception("Failed to get pool");
        }
        public async Task<PoolInfo> AddLiquidaty(string accountId, string token0, decimal token0Amount, string token1, decimal token1Amount)
        {
            var klWallet = CreateWallet(accountId);

            var oldPool = await Pool(token0, token1);
            var result = await klWallet.AddLiquidateToPoolAsync(token0, token0Amount, token1, token1Amount);
            if (result.ResultCode == APIResultCodes.Success)
            {
                for (int i = 0; i < 30; i++)
                {
                    await Task.Delay(500);     // wait for the pool to be liquidated
                    try
                    {
                        var latestPool = await Pool(token0, token1);
                        if (oldPool.height == latestPool.height)
                            continue;
                        else
                            return latestPool;
                    }
                    catch { }
                }
                throw new Exception("Add liquidaty failed.");
            }
            else
            {
                throw new Exception(result.ResultCode.ToString());
            }
        }
        public async Task<BalanceResult> RemoveLiquidaty(string accountId, string token0, string token1)
        {
            var klWallet = CreateWallet(accountId);

            var oldPool = await Pool(token0, token1);
            var result = await klWallet.RemoveLiquidateFromPoolAsync(token0, token1);
            if (result.ResultCode == APIResultCodes.Success)
            {
                for (int i = 0; i < 30; i++)
                {
                    await Task.Delay(500);     // wait for the liquidaty to be removed
                    try
                    {
                        var latestPool = await Pool(token0, token1);
                        if (oldPool.height == latestPool.height)
                            continue;
                        else
                        {
                            return await Receive(accountId);
                        }
                    }
                    catch { }
                }
                throw new Exception("Remove liquidaty failed.");
            }
            else
            {
                throw new Exception(result.ToString());
            }
        }
        public async Task<BalanceResult> Swap(string accountId, string token0, string token1, string tokenToSwap, decimal amountToSwap, decimal amountToGet)
        {
            var klWallet = CreateWallet(accountId);

            var oldPool = await Pool(token0, token1);
            var result = await klWallet.SwapToken(token0, token1, tokenToSwap, amountToSwap, amountToGet);
            if (result.ResultCode == APIResultCodes.Success)
            {
                for (int i = 0; i < 30; i++)
                {
                    await Task.Delay(500);     // wait for the swap to be finished
                    try
                    {
                        var latestPool = await Pool(token0, token1);
                        if (oldPool.height == latestPool.height)
                            continue;
                        else
                        {
                            return await Receive(accountId);
                        }                            
                    }
                    catch { }
                }
                throw new Exception("Token swap failed.");                
            }
            else
            {
                throw new Exception(result.ToString());
            }
        }

        // group notification
        public event EventHandler<Receiving> Notify;

        // group cli
        // on cli only

        public void Dispose()
        {
            NodeService.Dag.OnNewBlock -= NewBlockMonitor;
        }
    }
}
