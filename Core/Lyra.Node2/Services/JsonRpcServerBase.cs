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
using Lyra.Shared;

namespace Lyra.Node
{
    public class JsonRpcServerBase : IDisposable
    {
        public JsonRpc RPC { get; set; }
        protected INodeAPI _node;
        protected INodeTransactionAPI _trans;

        static ConcurrentQueue<object> _queue;
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
                _queue = new ConcurrentQueue<object>();
                _haveBlock = new EventWaitHandle(false, EventResetMode.ManualReset);
                _instances = new ConcurrentDictionary<int, JsonRpcServerBase>();

                NodeService.Dag.OnNewBlock += NewBlockMonitor;

                _ = Task.Run(async () => { 
                    while(true)
                    {
                        await _haveBlock.AsTask();
                        _haveBlock.Reset();

                        object info;
                        while(_queue.TryDequeue(out info))
                        {
                            foreach (var inst in _instances.Values)
                            {
                                try
                                {
                                    inst.Notify?.Invoke(this, new News { catalog = info.GetType().Name, content = info });
                                }
                                catch(Exception ex)
                                {
                                    
                                }
                            }
                        }
                    }
                });
            }

            _instances?.TryAdd(this.GetHashCode(), this);
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

        public void Dispose()
        {
            _instances?.TryRemove(this.GetHashCode(), out _);
        }
    }
}
