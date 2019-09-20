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

        const string REWARD_TOKEN = "rewards.rewards";
        const string DISCOUNT_TOKEN = "discounts.discounts";

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

            var result = wallet.LookForNewTrade(DISCOUNT_TOKEN, null).Result;

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

            var result = wallet.LookForNewTrade(null, REWARD_TOKEN).Result;

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

            var result = wallet.LookForNewTrade(DISCOUNT_TOKEN, REWARD_TOKEN).Result;

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

            var result = wallet.LookForNewTrade(REWARD_TOKEN, null).Result;

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

            var result = wallet.LookForNewTrade(null, DISCOUNT_TOKEN).Result;

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

            var result = wallet.LookForNewTrade(REWARD_TOKEN, DISCOUNT_TOKEN).Result;

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

            var result = wallet.RedeemRewards(REWARD_TOKEN, 2).Result;

            Assert.AreEqual(APIResultCodes.Success, result.ResultCode);

        }

        [TestMethod]
        public void TestMethod_FULL_TRADE_TEST()
        {
            Wallet merchant_wallet = WalletGenericTest.GetWallet(MERCHANT_PRIVATE_KEY);

            Wallet customer_wallet = WalletGenericTest.GetWallet(CUSTOMER_PRIVATE_KEY);

            var result = merchant_wallet.CreateGenesisForCoreToken();
            Assert.AreEqual(APIResultCodes.Success, result);

            result = merchant_wallet.CreateToken("rewards", "rewards", "", 2, 1000000, false, "Slava", "", "", null).Result.ResultCode;
            Assert.AreEqual(APIResultCodes.Success, result);

            result = merchant_wallet.CreateToken("discounts", "discounts", "", 2, 1000000, false, "Slava", "", "", null).Result.ResultCode;
            Assert.AreEqual(APIResultCodes.Success, result);

            result = merchant_wallet.TradeOrder(TradeOrderTypes.Sell, "discounts.discounts", "rewards.rewards", 100.00M, 1.00M, 10, true, false).Result.ResultCode;

            result = customer_wallet.GetActiveTradeOrders(null, null, Core.Blocks.TradeOrderListTypes.All).Result.ResultCode;

            Assert.AreEqual(APIResultCodes.Success, result);

        }

        [TestMethod]
        public void TestMethod_TradeOrder_SEQUENCE_Success()
        {
            Wallet merchant_wallet = WalletGenericTest.GetWallet(MERCHANT_PRIVATE_KEY);
            Wallet customer_wallet = WalletGenericTest.GetWallet(CUSTOMER_PRIVATE_KEY);
                       
            var result = merchant_wallet.TradeOrder(TradeOrderTypes.Sell, DISCOUNT_TOKEN, REWARD_TOKEN, 100.00M, 1.00M, 10, true, false).Result;
            Assert.AreEqual(APIResultCodes.Success, result.ResultCode);

            var auth_result = merchant_wallet.Send(10M, customer_wallet.AccountId, REWARD_TOKEN).Result;
            Assert.AreEqual(APIResultCodes.Success, auth_result.ResultCode);

            var sync_result = customer_wallet.Sync(null).Result;
            Assert.AreEqual(APIResultCodes.Success, sync_result);

            var orders_result = customer_wallet.GetActiveTradeOrders(null, null, Core.Blocks.TradeOrderListTypes.All).Result;
            Assert.AreEqual(APIResultCodes.Success, orders_result.ResultCode);

            if (orders_result.Successful())
            {
                var order_list = orders_result.GetList();
                var order_block = order_list[0];

                result = customer_wallet.TradeOrder(TradeOrderTypes.Buy, REWARD_TOKEN, DISCOUNT_TOKEN, 1.00M, 1.00M, 10, false, true).Result;
                Assert.AreEqual(APIResultCodes.TradeOrderMatchFound, result.ResultCode);

                var trade_block = result.GetBlock();

                auth_result = customer_wallet.Trade(trade_block).Result;
                Assert.AreEqual(APIResultCodes.Success, auth_result.ResultCode);

                auth_result = merchant_wallet.ExecuteSellOrder(trade_block, order_block, null).Result;
                Assert.AreEqual(APIResultCodes.Success, auth_result.ResultCode);

            }


            //result = customer_wallet.Trade(null, null, Core.Blocks.TradeOrderListTypes.All).Result;
            //Assert.AreEqual(APIResultCodes.Success, result.ResultCode);
        }

        [TestMethod]
        public void TestMethod_ExecuteSellOrder_Success()
        {
            var inmemory_storage = new AccountInMemoryStorage();
            Wallet wallet = new Wallet(inmemory_storage, NETWORK_ID);
            wallet.RestoreAccount("", MERCHANT_PRIVATE_KEY);

            var node = new RPCClient("Test");
            var res = wallet.Sync(node).Result;

            var result = wallet.LookForNewTrade(DISCOUNT_TOKEN, REWARD_TOKEN).Result;

            var trade = result.GetBlock();

            var discount_token = new NonFungibleToken();            discount_token.TokenCode = trade.BuyTokenCode;            discount_token.Denomination = trade.BuyAmount;
            discount_token.ExpirationDate = DateTime.Now + TimeSpan.FromDays(365);            discount_token.SerialNumber = discount_token.CalculateHash();            discount_token.RedemptionCode = "TEST";            discount_token.Sign(MERCHANT_PRIVATE_KEY);

            //trade.Fee = trade.Fee * 2;
            //trade.FeeType = AuthorizationFeeTypes.BothParties;

            var execute = wallet.ExecuteSellOrder(trade, null, discount_token).Result;

            Assert.AreEqual(APIResultCodes.Success, execute.ResultCode);

        }

        private void CreateREWARDToken()
        {

        }
    }
}
