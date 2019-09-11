using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
//
using Lyra.Client.Lib;
using Lyra.Client.InMemory;
using Lyra.Core.API;
using Lyra.Client.RPC;
using Lyra.Core.Blocks;


namespace Lyra.WalletTest
{
    [TestClass]
    public class TradingTest
    {
        const string MERCHANT_PRIVATE_KEY = "25kksnE589CTHcDeMNbatGBGoCjiMNFzcDCuGULj1vgCMAfxNV";
        const string CUSTOMER_PRIVATE_KEY = "2QvkckNTBttTt9EwsvWhDCwibcvzSkksx5iBuikh1AzgdYsNov";

        const string NETWORK_ID = "unittest";

        [TestMethod]
        public void TestMethod_Merchant_Restore_Success()
        {
            var inmemory_storage = new AccountInMemoryStorage();
            Wallet wallet = new Wallet(inmemory_storage, NETWORK_ID);
            var result = wallet.RestoreAccount("", MERCHANT_PRIVATE_KEY);
            // Assert.IsFalse(result.Successful());
            Assert.AreEqual(APIResultCodes.Success, result.ResultCode);
        }

        [TestMethod]
        public void TestMethod_Customer_Restore_Success()
        {
            var inmemory_storage = new AccountInMemoryStorage();
            Wallet wallet = new Wallet(inmemory_storage, NETWORK_ID);
            var result = wallet.RestoreAccount("", CUSTOMER_PRIVATE_KEY);
            // Assert.IsFalse(result.Successful());
            Assert.AreEqual(APIResultCodes.Success, result.ResultCode);
        }

        [TestMethod]
        public void TestMethod_Merchant_Sync_Success()
        {
            var inmemory_storage = new AccountInMemoryStorage();
            Wallet wallet = new Wallet(inmemory_storage, NETWORK_ID);
            wallet.RestoreAccount("", MERCHANT_PRIVATE_KEY);

            var node = new RPCClient("Test");
            var result = wallet.Sync(node).Result;
            Assert.AreEqual(APIResultCodes.Success, result);

        }

        [TestMethod]
        public void TestMethod_Customer_Sync_Success()
        {
            var inmemory_storage = new AccountInMemoryStorage();
            Wallet wallet = new Wallet(inmemory_storage, NETWORK_ID);
            wallet.RestoreAccount("", CUSTOMER_PRIVATE_KEY);

            var node = new RPCClient("Test");
            var result = wallet.Sync(node).Result;
            Assert.AreEqual(APIResultCodes.Success, result);

        }

        [TestMethod]
        public void TestMethod_GetActiveTradeOrders_Success()
        {
            var inmemory_storage = new AccountInMemoryStorage();
            Wallet wallet = new Wallet(inmemory_storage, NETWORK_ID);
            wallet.RestoreAccount("", MERCHANT_PRIVATE_KEY);

            var node = new RPCClient("Test");
            var res = wallet.Sync(node).Result;

            var result = wallet.GetActiveTradeOrders(null, null, Core.Blocks.TradeOrderListTypes.All).Result;

            Assert.AreEqual(APIResultCodes.Success, result.ResultCode);

        }

        [TestMethod]
        public void TestMethod_LookForNewTrade_NoTokensSpecified()
        {
            var inmemory_storage = new AccountInMemoryStorage();
            Wallet wallet = new Wallet(inmemory_storage, NETWORK_ID);
            wallet.RestoreAccount("", MERCHANT_PRIVATE_KEY);

            var node = new RPCClient("Test");
            var res = wallet.Sync(node).Result;

            var result = wallet.LookForNewTrade(null, null).Result;

            Assert.AreEqual(APIResultCodes.Success, result.ResultCode);

        }

        [TestMethod]
        public void TestMethod_LookForNewTrade_BuyToken()
        {
            var inmemory_storage = new AccountInMemoryStorage();
            Wallet wallet = new Wallet(inmemory_storage, NETWORK_ID);
            wallet.RestoreAccount("", MERCHANT_PRIVATE_KEY);

            var node = new RPCClient("Test");
            var res = wallet.Sync(node).Result;

            var result = wallet.LookForNewTrade("DIS", null).Result;

            Assert.AreEqual(APIResultCodes.Success, result.ResultCode);

        }

        [TestMethod]
        public void TestMethod_LookForNewTrade_SellToken()
        {
            var inmemory_storage = new AccountInMemoryStorage();
            Wallet wallet = new Wallet(inmemory_storage, NETWORK_ID);
            wallet.RestoreAccount("", MERCHANT_PRIVATE_KEY);

            var node = new RPCClient("Test");
            var res = wallet.Sync(node).Result;

            var result = wallet.LookForNewTrade(null, "REW").Result;

            Assert.AreEqual(APIResultCodes.Success, result.ResultCode);

        }

        [TestMethod]
        public void TestMethod_LookForNewTrade_BothTokens()
        {
            var inmemory_storage = new AccountInMemoryStorage();
            Wallet wallet = new Wallet(inmemory_storage, NETWORK_ID);
            wallet.RestoreAccount("", MERCHANT_PRIVATE_KEY);

            var node = new RPCClient("Test");
            var res = wallet.Sync(node).Result;

            var result = wallet.LookForNewTrade("DIS", "REW").Result;

            Assert.AreEqual(APIResultCodes.Success, result.ResultCode);

        }

        [TestMethod]
        public void TestMethod_LookForNewTrade_WrongBuyToken()
        {
            var inmemory_storage = new AccountInMemoryStorage();
            Wallet wallet = new Wallet(inmemory_storage, NETWORK_ID);
            wallet.RestoreAccount("", MERCHANT_PRIVATE_KEY);

            var node = new RPCClient("Test");
            var res = wallet.Sync(node).Result;

            var result = wallet.LookForNewTrade("REW", null).Result;

            Assert.AreEqual(APIResultCodes.NoTradesFound, result.ResultCode);

        }

        [TestMethod]
        public void TestMethod_LookForNewTrade_WrongSellToken()
        {
            var inmemory_storage = new AccountInMemoryStorage();
            Wallet wallet = new Wallet(inmemory_storage, NETWORK_ID);
            wallet.RestoreAccount("", MERCHANT_PRIVATE_KEY);

            var node = new RPCClient("Test");
            var res = wallet.Sync(node).Result;

            var result = wallet.LookForNewTrade(null, "DIS").Result;

            Assert.AreEqual(APIResultCodes.NoTradesFound, result.ResultCode);

        }

        [TestMethod]
        public void TestMethod_LookForNewTrade_BothTokensWrong()
        {
            var inmemory_storage = new AccountInMemoryStorage();
            Wallet wallet = new Wallet(inmemory_storage, NETWORK_ID);
            wallet.RestoreAccount("", MERCHANT_PRIVATE_KEY);

            var node = new RPCClient("Test");
            var res = wallet.Sync(node).Result;

            var result = wallet.LookForNewTrade("REW", "DIS").Result;

            Assert.AreEqual(APIResultCodes.NoTradesFound, result.ResultCode);

        }

        [TestMethod]
        public void TestMethod_Redeem_Success()
        {
            var inmemory_storage = new AccountInMemoryStorage();
            Wallet wallet = new Wallet(inmemory_storage, NETWORK_ID);
            wallet.RestoreAccount("", CUSTOMER_PRIVATE_KEY);

            var node = new RPCClient("Test");
            var res = wallet.Sync(node).Result;

            var result = wallet.RedeemRewards("REW", 2).Result;

            Assert.AreEqual(APIResultCodes.Success, result.ResultCode);

        }

        [TestMethod]
        public void TestMethod_ExecuteSellOrder_Success()
        {
            var inmemory_storage = new AccountInMemoryStorage();
            Wallet wallet = new Wallet(inmemory_storage, NETWORK_ID);
            wallet.RestoreAccount("", MERCHANT_PRIVATE_KEY);

            var node = new RPCClient("Test");
            var res = wallet.Sync(node).Result;

            var result = wallet.LookForNewTrade("DIS", "REW").Result;

            var trade = result.GetBlock();

            var discount_token = new NonFungibleToken();            discount_token.TokenCode = trade.BuyTokenCode;            discount_token.Denomination = trade.BuyAmount;
            discount_token.ExpirationDate = DateTime.Now + TimeSpan.FromDays(365);            discount_token.SerialNumber = discount_token.CalculateHash();            discount_token.RedemptionCode = "TEST";            discount_token.Sign(MERCHANT_PRIVATE_KEY);

            //trade.Fee = trade.Fee * 2;
            //trade.FeeType = AuthorizationFeeTypes.BothParties;

            var execute = wallet.ExecuteSellOrder(trade, null, discount_token).Result;

            Assert.AreEqual(APIResultCodes.Success, execute.ResultCode);

        }
    }
}
