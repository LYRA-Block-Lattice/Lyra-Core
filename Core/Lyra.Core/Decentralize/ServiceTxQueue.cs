using Lyra.Core.Blocks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lyra.Core.Decentralize
{
    public delegate void ServiceTxFinished(ServiceTx tx);
    public class ServiceTxQueue
    {
        public event ServiceTxFinished OnTxFinished;
        // pool ID, tx mem pool (queue)
        private Dictionary<string, List<ServiceTx>> _poolFifoQueue = new Dictionary<string, List<ServiceTx>>();

        public bool CanAdd(string poolId)
        {
            return !(_poolFifoQueue.ContainsKey(poolId) && _poolFifoQueue[poolId].Any(x => x.IsExclusive));
        }

        public void Add(string poolId, string sendHash)
        {
            if (!_poolFifoQueue.ContainsKey(poolId))
                _poolFifoQueue.Add(poolId, new List<ServiceTx>());

            _poolFifoQueue[poolId].Add(new ServiceTx(sendHash));
        }

        public void Add(string poolId, ServiceTx tx)
        {
            if (!_poolFifoQueue.ContainsKey(poolId))
                _poolFifoQueue.Add(poolId, new List<ServiceTx>());

            _poolFifoQueue[poolId].Add(tx);
        }

        public void Finish(string poolId, string relHash, string recvHash, string actionHash)
        {
            if (!_poolFifoQueue.ContainsKey(poolId))
                return;// throw new ArgumentException("Pool not found!", poolId);

            var poolTx = _poolFifoQueue[poolId];
            var tx = poolTx.FirstOrDefault(x => x.ReqSendHash == relHash);
            if (tx == null)
                return;

            if (!string.IsNullOrEmpty(recvHash))
                tx.FinishRecv(recvHash);

            if (!string.IsNullOrEmpty(actionHash))
                tx.FinishAction(actionHash);

            if(tx.IsTxCompleted)
            {
                poolTx.Remove(tx);
                if(poolTx.Count == 0)
                    _poolFifoQueue.Remove(poolId);
                OnTxFinished?.Invoke(tx);
            }
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

        public void FinishRecv(string recvHash)
        {
            ReqRecvHash = recvHash;
        }

        public virtual void FinishAction (string actionHash)
        {
            throw new Exception("Must override FinishAction");
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

        public override void FinishAction(string actionHash)
        {
            ReplyActionHash = actionHash;
        }
    }
}
