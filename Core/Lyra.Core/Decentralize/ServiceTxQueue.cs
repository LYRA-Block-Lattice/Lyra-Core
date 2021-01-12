using Lyra.Core.Blocks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lyra.Core.Decentralize
{
    public class ServiceTxQueue
    {
        // pool ID, tx mem pool (queue)
        private Dictionary<string, ConcurrentQueue<ServiceTx>> _poolFifoQueue = new Dictionary<string, ConcurrentQueue<ServiceTx>>();

        public bool CanAdd(string poolId)
        {
            return !(_poolFifoQueue.ContainsKey(poolId) && _poolFifoQueue[poolId].ToArray().Any(x => x.IsExclusive));
        }

        public void Add(string poolId, string sendHash)
        {
            if (!_poolFifoQueue.ContainsKey(poolId))
                _poolFifoQueue.Add(poolId, new ConcurrentQueue<ServiceTx>());

            _poolFifoQueue[poolId].Enqueue(new ServiceTx(sendHash));
        }

        public void Add(string poolId, ServiceTx tx)
        {
            if (!_poolFifoQueue.ContainsKey(poolId))
                _poolFifoQueue.Add(poolId, new ConcurrentQueue<ServiceTx>());

            _poolFifoQueue[poolId].Enqueue(tx);
        }
    }

    public class ServiceTx
    {
        public DateTime TimeStamp { get; private set; }
        /// <summary>
        /// wether the tx can existing with others
        /// </summary>
        public bool IsExclusive { get; protected set; }

        /// <summary>
        /// the original send block hash.
        /// </summary>
        public string ReqSendHash { get; set; }

        /// <summary>
        /// the consensus must do receive because of the law of block lattice
        /// </summary>
        public string ReqRecvHash { get; set; }

        public virtual bool IsTxCompleted => !string.IsNullOrEmpty(ReqSendHash) && !string.IsNullOrEmpty(ReqRecvHash);

        public ServiceTx()
        {
            IsExclusive = true;
            TimeStamp = DateTime.UtcNow;    // incase exchange it between nodes
        }

        public ServiceTx(string reqSendHash) : this()
        {
            ReqSendHash = reqSendHash;
        }
    }

    public class ServiceWithActionTx : ServiceTx
    {
        /// <summary>
        /// the action must be performanced after receive block.
        /// create pool, withdraw, swap all need extra action.
        /// deposition not need extra action.
        /// </summary>
        public string ReplyActionHash { get; set; }

        public override bool IsTxCompleted => base.IsTxCompleted && !string.IsNullOrEmpty(ReplyActionHash);

        public ServiceWithActionTx(string reqSendHash) : base(reqSendHash)
        {

        }
    }
}
