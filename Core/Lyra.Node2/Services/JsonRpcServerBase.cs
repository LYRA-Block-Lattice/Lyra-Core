using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Node
{
    public class JsonRpcServerBase : IDisposable
    {
        public JsonRpc RPC { get; set; }
        protected INodeAPI _node;
        protected INodeTransactionAPI _trans;

        protected string _monitorAccountId;

        // group notification
        public event EventHandler<News> Notify;

        public JsonRpcServerBase(INodeAPI node, INodeTransactionAPI trans)
        {
            _node = node;
            _trans = trans;

            if (NodeService.Dag != null)
                NodeService.Dag.OnNewBlock += NewBlockMonitor;
        }

        public void NewBlockMonitor(Block block, Block prevBlock)
        {
            if (block is SendTransferBlock send && (send.DestinationAccountId == _monitorAccountId || _monitorAccountId == "*"))
            {
                var chgs = send.GetBalanceChanges(prevBlock as TransactionBlock);
                var recvInfo = new Receiving
                {
                    sendHash = send.Hash,
                    from = send.AccountID,
                    to = send.DestinationAccountId,
                    funds = chgs.Changes
                };
                Notify?.Invoke(this, new News { catalog = "Receiving", content = recvInfo });
            }
        }

        public void Dispose()
        {
            NodeService.Dag.OnNewBlock -= NewBlockMonitor;
        }
    }
}
