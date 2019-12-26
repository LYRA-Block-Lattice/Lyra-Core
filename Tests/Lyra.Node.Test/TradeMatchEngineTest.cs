using Microsoft.VisualStudio.TestTools.UnitTesting;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;

using Lyra.Core.API;
using System.Collections.Generic;
using Lyra.Node.Authorizers;

namespace Lyra.Node.Test
{
    public partial class Authorizer_Tests
    {
        // No trades yet
        [TestMethod]
        public void GetActiveTradeOrderList_1()
        {
            CreateFirstGenesisBlock();
            CreateUSDToken(false);
            //SendTransfer();
            ProcessSendGRFT();
            ProcessReceiveGRFT();
            
            var result = tradeMatchEngine.GetActiveTradeOrders(null, null, TradeOrderListTypes.All);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);

        }

        // one sell order
        [TestMethod]
        public void GetActiveTradeOrderList_2()
        {
            CreateFirstGenesisBlock();
            CreateUSDToken(false);
            //SendTransfer();
            ProcessSendGRFT();
            ProcessReceiveGRFT();
            SellOrderUSDAcc1();
            BuyOrderUSDAcc2();

            var result = tradeMatchEngine.GetActiveTradeOrders(null, null, TradeOrderListTypes.All);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);

        }

        // Still one sell order
        [TestMethod]
        public void GetActiveTradeOrderList_3()
        {
            CreateFirstGenesisBlock();
            CreateUSDToken(false);
            //SendTransfer();
            ProcessSendGRFT();
            ProcessReceiveGRFT();
            SellOrderUSDAcc1();
            BuyOrderUSDAcc2();

            var result = tradeMatchEngine.GetActiveTradeOrders(null, null, TradeOrderListTypes.All);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);

        }

        // No orders again
        [TestMethod]
        public void GetActiveTradeOrderList_4()
        {
            CreateFirstGenesisBlock();
            CreateUSDToken(false);
            //SendTransfer();
            ProcessSendGRFT();
            ProcessReceiveGRFT();
            SellOrderUSDAcc1();
            BuyOrderUSDAcc2();
            ProcessTrade();
            ProcessExecuteTrade();

            var result = tradeMatchEngine.GetActiveTradeOrders(null, null, TradeOrderListTypes.All);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);

        }

        // One buy order
        [TestMethod]
        public void GetActiveTradeOrderList_5()
        {
            CreateFirstGenesisBlock();
            CreateUSDToken(false);
            ProcessSendGRFT();
            ProcessReceiveGRFT();
            BuyOrderUSDAcc2();

            var result = tradeMatchEngine.GetActiveTradeOrders(null, null, TradeOrderListTypes.All);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);

        }

        // One buy order - find only buy orders
        [TestMethod]
        public void GetActiveTradeOrderList_6()
        {
            CreateFirstGenesisBlock();
            CreateUSDToken(false);
            ProcessSendGRFT();
            ProcessReceiveGRFT();
            BuyOrderUSDAcc2();

            var result = tradeMatchEngine.GetActiveTradeOrders(null, null, TradeOrderListTypes.BuyOnly);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);

        }

        // One buy order - find only buy orders
        [TestMethod]
        public void GetActiveTradeOrderList_7()
        {
            CreateFirstGenesisBlock();
            CreateUSDToken(false);
            ProcessSendGRFT();
            ProcessReceiveGRFT();
            BuyOrderUSDAcc2();

            var result = tradeMatchEngine.GetActiveTradeOrders(null, null, TradeOrderListTypes.SellOnly);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);

        }

        // one sell order = find sell orders only
        [TestMethod]
        public void GetActiveTradeOrderList_8()
        {
            CreateFirstGenesisBlock();
            CreateUSDToken(false);
            ProcessSendGRFT();
            ProcessReceiveGRFT();
            SellOrderUSDAcc1();

            var result = tradeMatchEngine.GetActiveTradeOrders(null, null, TradeOrderListTypes.SellOnly);

            Assert.AreEqual(1, result.Count);

        }

        // one sell order = find sell orders only
        [TestMethod]
        public void GetActiveTradeOrderList_9()
        {
            CreateFirstGenesisBlock();
            CreateUSDToken(false);
            ProcessSendGRFT();
            ProcessReceiveGRFT();
            SellOrderUSDAcc1();

            var result = tradeMatchEngine.GetActiveTradeOrders(null, null, TradeOrderListTypes.BuyOnly);

            Assert.AreEqual(0, result.Count);

        }

        // one sell order = find specific sell orders for sell token
        [TestMethod]
        public void GetActiveTradeOrderList_10()
        {
            CreateFirstGenesisBlock();
            CreateUSDToken(false);
            ProcessSendGRFT();
            ProcessReceiveGRFT();
            SellOrderUSDAcc1();

            var result = tradeMatchEngine.GetActiveTradeOrders("Custom.USD", null, TradeOrderListTypes.All);

            Assert.AreEqual(1, result.Count);

        }

        // one sell order = find specific sell orders for sell token
        [TestMethod]
        public void GetActiveTradeOrderList_11()
        {
            CreateFirstGenesisBlock();
            CreateUSDToken(false);
            ProcessSendGRFT();
            ProcessReceiveGRFT();
            SellOrderUSDAcc1();

            var result = tradeMatchEngine.GetActiveTradeOrders(TokenGenesisBlock.LYRA_TICKER_CODE, null, TradeOrderListTypes.All);

            Assert.AreEqual(0, result.Count);

        }

        // one sell order = find specific sell orders for sell and buy tokens
        [TestMethod]
        public void GetActiveTradeOrderList_12()
        {
            CreateFirstGenesisBlock();
            CreateUSDToken(false);
            ProcessSendGRFT();
            ProcessReceiveGRFT();
            SellOrderUSDAcc1();

            var result = tradeMatchEngine.GetActiveTradeOrders("Custom.USD", TokenGenesisBlock.LYRA_TICKER_CODE, TradeOrderListTypes.All);

            Assert.AreEqual(1, result.Count);

        }

        // one sell order = find specific sell orders for sell and buy tokens
        [TestMethod]
        public void GetActiveTradeOrderList_13()
        {
            CreateFirstGenesisBlock();
            CreateUSDToken(false);
            ProcessSendGRFT();
            ProcessReceiveGRFT();
            SellOrderUSDAcc1();

            var result = tradeMatchEngine.GetActiveTradeOrders(TokenGenesisBlock.LYRA_TICKER_CODE, "USD", TradeOrderListTypes.All);

            Assert.AreEqual(0, result.Count);

        }

        // one sell order = cancel order
        [TestMethod]
        public void CancelOrder_1()
        {
            CreateFirstGenesisBlock();
            CreateUSDToken(false);
            ProcessSendGRFT();
            ProcessReceiveGRFT();

            var result = ProcessCancelSellOrder(_SendTransferBlock, CreateSellOrderBlock(_SendTransferBlock), _SendTransferBlock);

            Assert.AreEqual(APIResultCodes.NoTradesFound, result);

        }

        // one sell order = cancel order
        [TestMethod]
        public void CancelOrder_2()
        {
            CreateFirstGenesisBlock();
            CreateUSDToken(false);
            ProcessSendGRFT();
            ProcessReceiveGRFT();
            SellOrderUSDAcc1();

            var list = tradeMatchEngine.GetActiveTradeOrders(null, null, TradeOrderListTypes.All);
            Assert.AreEqual(1, list.Count);

            var result = ProcessCancelSellOrder(_SellOrderBlock, _SellOrderBlock, _SendTransferBlock);
            Assert.AreEqual(APIResultCodes.Success, result);

            list = tradeMatchEngine.GetActiveTradeOrders(null, null, TradeOrderListTypes.All);
            Assert.AreEqual(0, list.Count);

        }

        // one buy order = cancel order
        [TestMethod]
        public void CancelOrder_3()
        {
            CreateFirstGenesisBlock();
            CreateUSDToken(false);
            ProcessSendGRFT();
            ProcessReceiveGRFT();
            BuyOrderUSDAcc2();

            var list = tradeMatchEngine.GetActiveTradeOrders(null, null, TradeOrderListTypes.All);
            Assert.AreEqual(1, list.Count);

            var result = ProcessCancelSellOrder(_BuyOrderBlock, _BuyOrderBlock, _OpenAccount2Block);
            Assert.AreEqual(APIResultCodes.Success, result);

            list = tradeMatchEngine.GetActiveTradeOrders(null, null, TradeOrderListTypes.All);
            Assert.AreEqual(0, list.Count);

        }

        APIResultCodes ProcessCancelSellOrder(TransactionBlock lastBlock, TradeOrderBlock order, TransactionBlock previous_to_order_block)
        {
            var authorizer = new CancelTradeOrderAuthorizer(serviceAccount, accountCollection, tradeMatchEngine);
            var cancel_block = CreateCancelOrderBlock(lastBlock, order, previous_to_order_block);
            return authorizer.Authorize<CancelTradeOrderBlock>(ref cancel_block);
        }

        // Let's buy 100 USD for 5 LGT per USD
        CancelTradeOrderBlock CreateCancelOrderBlock(TransactionBlock previousBlock, TradeOrderBlock order, TransactionBlock previous_to_order_block)
        {
            var cancelBlock = new CancelTradeOrderBlock
            {
                AccountID = order.AccountID,
                ServiceHash = string.Empty,
                Balances = new Dictionary<string, decimal>(),
                TradeOrderId = order.Hash
            };


            var order_transaction = order.GetTransaction(previous_to_order_block);

            cancelBlock.Balances.Add(order.SellTokenCode, previousBlock.Balances[order.SellTokenCode] + order_transaction.TotalBalanceChange);

            // transfer unchanged token balances from the previous block
            foreach (var balance in previousBlock.Balances)
                if (!(cancelBlock.Balances.ContainsKey(balance.Key)))
                    cancelBlock.Balances.Add(balance.Key, balance.Value);

            if (order.AccountID == AccountId1)
                cancelBlock.InitializeBlock(previousBlock, PrivateKey1, NETWORK_ID);
            else
                cancelBlock.InitializeBlock(previousBlock, PrivateKey2, NETWORK_ID);

            return cancelBlock;
        }



    }
}
