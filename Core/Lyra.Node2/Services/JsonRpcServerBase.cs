using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using StreamJsonRpc;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Lyra.Data.Shared;

namespace Lyra.Node
{
    public class JsonRpcServerBase : IDisposable
    {
        public JsonRpc RPC { get; set; }
        protected INodeAPI _node;
        protected INodeTransactionAPI _trans;

        static ConcurrentQueue<TxInfoBase> _queue;
        static EventWaitHandle _haveBlock;
        static ConcurrentDictionary<int, JsonRpcServerBase> _instances;

        protected string _monitorAccountId;

        // group notification
        public event EventHandler<News> Notify;

        public JsonRpcServerBase(INodeAPI node, INodeTransactionAPI trans)
        {
            _node = node;
            _trans = trans;

            if(_queue == null && NodeService.Dag != null)
            {
                _queue = new ConcurrentQueue<TxInfoBase>();
                _haveBlock = new EventWaitHandle(false, EventResetMode.ManualReset);
                _instances = new ConcurrentDictionary<int, JsonRpcServerBase>();

                NodeService.Dag.OnNewBlock += NewBlockMonitor;

                _ = Task.Run(async () => { 
                    while(true)
                    {
                        await _haveBlock.AsTaskAsync();
                        _haveBlock.Reset();

                        TxInfoBase info;
                        while(_queue.TryDequeue(out info))
                        {
                            foreach (var inst in _instances.Values)
                            {
                                try
                                {
                                    if(inst.GetIfInterested(info.to))
                                        inst.Notify?.Invoke(this, new News { catalog = info.GetType().Name, content = info });
                                }
                                catch
                                {
                                    
                                }
                            }
                        }
                    }
                });
            }

            _instances?.TryAdd(this.GetHashCode(), this);
        }

        protected virtual bool GetIfInterested(string addr)
        {
            return false;
        }

        private static void NewBlockMonitor(Block block, Block prevBlock)
        {
            if (block is SendTransferBlock send)
            {
                var chgs = send.GetBalanceChanges(prevBlock as TransactionBlock);
                var recvInfo = new Receiving
                {
                    sendHash = send.Hash,
                    from = send.AccountID,
                    to = send.DestinationAccountId,
                    funds = chgs.Changes
                };
                _queue.Enqueue(recvInfo);
                _haveBlock.Set();
            }
            else if(block is ReceiveTransferBlock recv)
            {
                var chgs = recv.GetBalanceChanges(prevBlock as TransactionBlock);
                var setInfo = new Settlement
                {
                    sendHash = recv.SourceHash,
                    recvHash = recv.Hash,
                    //from = send.AccountID,
                    to = recv.AccountID,
                    funds = chgs.Changes
                };
                _queue.Enqueue(setInfo);
                _haveBlock.Set();
            }
        }

        [JsonRpcMethod("Authorize")]
        public async Task<SimpleJsonAPIResult> AuthorizeAsync(string blockType, string jsonBlock)
        {
            BlockTypes types;
            try
            {
                types = (BlockTypes)Enum.Parse(typeof(BlockTypes), blockType);
            }
            catch (Exception)
            {
                return new SimpleJsonAPIResult { ResultCode = APIResultCodes.InvalidBlockType };
            }

            var br = new BlockAPIResult
            {
                BlockData = jsonBlock,
                ResultBlockType = types
            };

            var block = br.GetBlock();
            if (block == null)
            {
                return new SimpleJsonAPIResult { ResultCode = APIResultCodes.InvalidBlockData };
            }

            // block is valid. send it to consensus network
            AuthorizationAPIResult result;
            switch(types)
            {
                case BlockTypes.SendTransfer:
                    result = await _trans.SendTransferAsync(block as SendTransferBlock);
                    break;
                case BlockTypes.ReceiveTransfer:
                    result = await _trans.ReceiveTransferAsync(block as ReceiveTransferBlock);
                    break;
                case BlockTypes.OpenAccountWithReceiveTransfer:
                    result = await _trans.ReceiveTransferAndOpenAccountAsync(block as OpenWithReceiveTransferBlock);
                    break;
                case BlockTypes.TokenGenesis:
                    result = await _trans.CreateTokenAsync(block as TokenGenesisBlock);
                    break;
                default:
                    result = null;
                    break;                
            }
            
            if(result == null)
                return new SimpleJsonAPIResult { ResultCode = APIResultCodes.UnsupportedBlockType };

            return new SimpleJsonAPIResult { ResultCode = result.ResultCode };
        }

        public void Dispose()
        {
            _instances?.TryRemove(this.GetHashCode(), out _);
        }
    }
}
