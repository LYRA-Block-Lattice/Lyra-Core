using Lyra.Core.Decentralize;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;

namespace UnitTests
{
    [TestClass]
    public class UT_ServiceTx
    {
        [TestMethod]
        public void TestServiceTxAdd()
        {
            var queue = new ServiceTxQueue();
            var poolId = "sssssss";
            var tx1 = new ServiceTx("aaa");
            Assert.IsFalse(tx1.IsTxCompleted);
            Assert.IsTrue(queue.CanAdd(poolId));
            queue.Add(poolId, tx1);
            Assert.IsFalse(queue.CanAdd(poolId));

            tx1.ReqRecvHash = "bbbbb";
            Assert.IsTrue(tx1.IsTxCompleted);
        }

        [TestMethod]
        public void TestTxWithAction()
        {
            var queue = new ServiceTxQueue();
            var poolId = "sssssss";
            var tx1 = new ServiceWithActionTx("aaa");
            Assert.IsFalse(tx1.IsTxCompleted);
            Assert.IsTrue(queue.CanAdd(poolId));
            queue.Add(poolId, tx1);
            Assert.IsFalse(queue.CanAdd(poolId));

            tx1.ReqRecvHash = "bbbbb";
            Assert.IsFalse(tx1.IsTxCompleted);

            tx1.ReplyActionHash = "ccc";
            Assert.IsTrue(tx1.IsTxCompleted);
        }
    }
}
