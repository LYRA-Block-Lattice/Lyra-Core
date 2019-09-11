//using Microsoft.VisualStudio.TestTools.UnitTesting;

//using Lyra.Node.Authorizers;
//using Lyra.Core.LiteDB;
//using Lyra.Core.Blocks;
//using Lyra.Core.Blocks.Transactions;

//using Lyra.Core.Cryptography;
//using Lyra.Core.API;
//using Lyra.Core.Accounts;
//using Lyra.Node.LiteDB;
//using Lyra.Node.MongoDB;
//using System;
//using System.Collections.Generic;


//// The test script:
//// Account  Height  Block                   TransAmount                 Fee, LGT       Balance, LGT       Balance, USD    TradeAmount Price
//// 1        0       N/A                     0                           0               0                   0
//// 2        0       N/A                     0                           0               0                   0
//// 1        1       FirstGenesis            +1,800,000,000.00 LGT      0               1,800,000,000.00    0
//// 1        2       TokenGenesis            +100,000.00 USD             100.00          1,799,999,900.00    100,000.00
//// 1        3       SendTransfer            -50,000.00 LGT             1.00            1,799,949,899.00    100,000.00
//// 2        1       OpenWithReceiveTransfer +50,000.00 LGT             0               50,000.00           0
//// 1        4       TradeOrder              -100.00 USD                 0.2             1,799,949,898.80    99,900.00       100 USD     5 LGT          
//// 2        2       TradeOrder              -500.00 LGT                0               49,500.00           0               100 USD     5 LGT
//// 2        2       Trade                   -500.00 LGT                0               49,500.00           0               
//// 1        5       ExecuteTrade            -100.00 USD                  0.2            1,799,949,898.80    99,900.00
//// 1        6       ReceiveTransfer         +500.00 LGT                0               1,799,950,398.80    99,900.00     
//// 2        3       ReceiveTransfer         +100.00 USD                 0               49,500.00           100.00 

//namespace Lyra.Node.Test
//{
//    [TestClass]
//    public partial class Authorizer_Tests
//    {
//        static string PrivateKey1;
//        static string AccountId1;

//        static string PrivateKey2;
//        static string AccountId2;

//        static string NETWORK_ID = "unittest";

//        //int USD_PRECISION = 0;

//        static ServiceAccount serviceAccount;
//        LiteAccountCollection accountCollection;
//        static TradeMatchEngine tradeMatchEngine;
//        static IAccountDatabase serviceDatabase;

//        //Account1:
//        LyraTokenGenesisBlock _FirstGenesisBlock;
//        TokenGenesisBlock _USDTokenBlock;
//        SendTransferBlock _SendTransferBlock;
//        TradeOrderBlock _SellOrderBlock;
//        ExecuteTradeOrderBlock _ExecuteTradeOrderBlock;
//        ReceiveTransferBlock _ReceiveTransferBlockAcc1;

//        // Account2:
//        OpenWithReceiveTransferBlock _OpenAccountBlock;
//        TradeOrderBlock _BuyOrderBlock;
//        TradeBlock _TradeBlock;
//        ReceiveTransferBlock _ReceiveTransferBlockAcc2;

//        const string connectionString = "mongodb://lyra-cosmos-db:W2ZXq7ldikf2Ncmgtq3T3DoSbjD1Nen70MwMG7s4LePCCArSlfdJAgUqVqlBMotLVy8I2kRkXMGLgeKP9hY1Bg==@lyra-cosmos-db.documents.azure.com:10255/?ssl=true&replicaSet=globaldb";

//        [ClassInitialize]
//        public static void ClassInitialize(TestContext testContext)
//        {
//            PrivateKey1 = "DQDP23xgHmLSsdm64qu1UsMteA5qDfgTiFRQRbjnfKstkg4LN";
//            AccountId1 = Signatures.GetAccountIdFromPrivateKey(PrivateKey1);

//            PrivateKey2 = "2CcBpc2vn8uXeiXp7sW15w3wFYCWt36VufrKuPBzqnxVQto64H";
//            AccountId2 = Signatures.GetAccountIdFromPrivateKey(PrivateKey2);

//            NodeGlobalParameters.Network_Id = NETWORK_ID;

//            //serviceDatabase = new LiteAccountDatabase();
//            serviceDatabase = new MongoServiceAccountDatabase(connectionString, "admin", "service_account", NETWORK_ID);

//            serviceAccount = new ServiceAccount(serviceDatabase, NETWORK_ID);
//            serviceAccount.Delete(ServiceAccount.SERVICE_ACCOUNT_NAME);
//            serviceAccount.InitializeServiceAccount("");
//        }

//        public Authorizer_Tests()
//        {
//        }

//        [TestInitialize]
//        public void TestInitialize()
//        {
//            Console.WriteLine("TestInitialize");

//            //Account1:
//            _FirstGenesisBlock = null;
//            _USDTokenBlock = null;
//            _SendTransferBlock = null;
//            _SellOrderBlock = null;
//            _ExecuteTradeOrderBlock = null;
//            _ReceiveTransferBlockAcc1 = null;

//            // Account2:
//            _OpenAccountBlock = null;
//            _BuyOrderBlock = null;
//            _TradeBlock = null;
//            _ReceiveTransferBlockAcc2 = null;

//            _DiscountTokenBlock = null;

            


//            accountCollection = new LiteAccountCollection("");
//            accountCollection.Delete();
//            tradeMatchEngine = new TradeMatchEngine(accountCollection, serviceAccount);
//        }

//        [TestCleanup]
//        public void TestCleanup()
//        {
//        }


//            private string SignAPICall(string PrivateKey)
//        {
//            var rpc = new RPCMethods(serviceAccount, accountCollection, tradeMatchEngine);
//            var result = rpc.GetSyncHeight().Result;
//            if (result.ResultCode != APIResultCodes.Success)
//                return "";

//            return Signatures.GetSignature(PrivateKey, result.SyncHash);
//        }

//        [TestMethod]
//        [ExpectedException(typeof(FormatException))]
//        public void PrivateKeyFormat()
//        {
//            var private_key = "1234567890";
//            var account_id = Signatures.GetAccountIdFromPrivateKey(private_key);
//        }

//        [TestMethod]
//        [ExpectedException(typeof(FormatException))]
//        public void PrivateKeyChecksum()
//        {
//            var private_key = "TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT";
//            var account_id = Signatures.GetAccountIdFromPrivateKey(private_key);
//        }

//        [TestMethod]
//        public void FirstGenesisBlock()
//        {
//            var result = CreateFirstGenesisBlock();

//            Assert.AreEqual(APIResultCodes.Success, result);
//            //            Assert.AreEqual(1800000000 * Math.Pow(10, 2), _FirstGenesisBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE]);
//            Assert.AreEqual(1800000000, _FirstGenesisBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE]);
//        }

//        [TestMethod]
//        public void DuplicateFirstGenesisBlockWithTheSameAccount()
//        {
//            CreateFirstGenesisBlock();

//            var authorizer = new GenesisAuthorizer(serviceAccount, accountCollection);

//            var block = CreateGenesisBlock();

//            var result = authorizer.Authorize<LyraTokenGenesisBlock>(ref block);

//            Assert.AreEqual(APIResultCodes.AccountAlreadyExists, result);
//        }

//        [TestMethod]
//        public void DuplicateFirstGenesisBlockWithDifferentAccount()
//        {

//            CreateFirstGenesisBlock();

//            var authorizer = new GenesisAuthorizer(serviceAccount, accountCollection);

//            PrivateKey1 = Signatures.GeneratePrivateKey();
//            AccountId1 = Signatures.GetAccountIdFromPrivateKey(PrivateKey1);

//            var block = CreateGenesisBlock();

//            var result = authorizer.Authorize<LyraTokenGenesisBlock>(ref block);

//            Assert.AreEqual(APIResultCodes.TokenGenesisBlockAlreadyExists, result);
//        }

//        [TestMethod]
//        public void USDToken_1()
//        {
//            CreateFirstGenesisBlock();

//            var result = CreateUSDToken(false);

//            Assert.AreEqual(APIResultCodes.Success, result);

//            var previous_balance = _FirstGenesisBlock.Balances[_FirstGenesisBlock.Ticker];
//            var new_balance = _USDTokenBlock.Balances[_FirstGenesisBlock.Ticker];
//            Assert.AreEqual(0, previous_balance - (new_balance + serviceAccount.GetLastServiceBlock().TokenGenerationFee));

//            //Assert.AreEqual((1800000000 - 100) * Math.Pow(10, 2), _USDTokenBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE]);
//            //Assert.AreEqual(100000 * Math.Pow(10, USD_PRECISION), _USDTokenBlock.Balances["USD"]);
//            Assert.AreEqual((1800000000 - 100), _USDTokenBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE]);
//            Assert.AreEqual(100000, _USDTokenBlock.Balances["Custom.USD"]);


//        }

//        [TestMethod] // Invalid Non fungible key
//        public void USDToken_2()
//        {
//            CreateFirstGenesisBlock();

//            var result = CreateUSDToken(true);

//            Assert.AreEqual(APIResultCodes.InvalidNonFungiblePublicKey, result);
//        }

//        [TestMethod] // Valid Non fungible
//        public void USDToken_3()
//        {
//            CreateFirstGenesisBlock();

//            var result = CreateUSDToken(true, AccountId1);

//            Assert.AreEqual(APIResultCodes.Success, result);

//            var previous_balance = _FirstGenesisBlock.Balances[_FirstGenesisBlock.Ticker];
//            var new_balance = _USDTokenBlock.Balances[_FirstGenesisBlock.Ticker];
//            Assert.AreEqual(0, previous_balance - (new_balance + serviceAccount.GetLastServiceBlock().TokenGenerationFee));

//            //Assert.AreEqual((1800000000 - 100) * Math.Pow(10, 2), _USDTokenBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE]);
//            //Assert.AreEqual(100000 * Math.Pow(10, USD_PRECISION), _USDTokenBlock.Balances["USD"]);
//            Assert.AreEqual((1800000000 - 100), _USDTokenBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE]);
//            Assert.AreEqual(100000, _USDTokenBlock.Balances["Custom.USD"]);


//        }

//        [TestMethod]
//        public void DuplicateUSDToken()
//        {
//            CreateFirstGenesisBlock();
//            CreateUSDToken(false);

//            TokenGenesisBlock duplicate = CreateUSDTokenBlock(_USDTokenBlock, false, null);
//            var authorizer = new NewTokenAuthorizer(serviceAccount, accountCollection);
//            var result = authorizer.Authorize<TokenGenesisBlock>(ref duplicate);

//            Assert.AreEqual(APIResultCodes.TokenGenesisBlockAlreadyExists, result);
//        }

//        [TestMethod]
//        public void SendTransfer()
//        {
//            CreateFirstGenesisBlock();
//            CreateUSDToken(false);

//            //decimal send_amount = 50000; // LGT

//            var result = ProcessSendGRFT();

//            Assert.AreEqual(APIResultCodes.Success, result);

//            var previous_balance = _USDTokenBlock.Balances[_FirstGenesisBlock.Ticker];
//            var new_balance = _SendTransferBlock.Balances[_FirstGenesisBlock.Ticker];

//            //Assert.AreEqual(0, previous_balance - (new_balance + serviceAccount.GetLastServiceBlock().TransferFee + send_amount * Math.Pow(10, 2)));

//            //Assert.AreEqual((1800000000 - 100 - 50000 - 1) * Math.Pow(10, 2), _SendTransferBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE]);
//            //Assert.AreEqual(100000 * Math.Pow(10, USD_PRECISION), _SendTransferBlock.Balances["USD"]);
//            Assert.AreEqual((1800000000 - 100 - 50000 - 1), _SendTransferBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE]);
//            Assert.AreEqual(100000, _SendTransferBlock.Balances["Custom.USD"]);

//        }

//        [TestMethod]
//        public void GetBlockByHash()
//        {
//            CreateFirstGenesisBlock();
//            CreateUSDToken(false);

//            string hash = _FirstGenesisBlock.Hash;
//            var result = accountCollection.FindBlockByHash(hash);
//            Assert.AreEqual(hash, result.Hash);


//        }


//        [TestMethod]
//        public void OpenSecondAccount()
//        {
//            CreateFirstGenesisBlock();
//            CreateUSDToken(false);
//            //SendTransfer();
//            ProcessSendGRFT();
//            var result = ProcessReceiveGRFT();
//            Assert.AreEqual(APIResultCodes.Success, result);

//            //Assert.AreEqual(50000 * Math.Pow(10, 2), _OpenAccountBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE]);
//            Assert.AreEqual(50000, _OpenAccountBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE]);
//        }

//        [TestMethod]
//        public void SellTradeOrderBlock() // from account 1
//        {
//            CreateFirstGenesisBlock();
//            CreateUSDToken(false);
//            //SendTransfer();
//            ProcessSendGRFT();

//            var result = SellOrderUSDAcc1();

//            Assert.AreEqual(APIResultCodes.Success, result);

//            //Assert.AreEqual(1799949898.80 * Math.Pow(10, 2), _SellOrderBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE]);
//            //Assert.AreEqual(99900 * Math.Pow(10, USD_PRECISION), _SellOrderBlock.Balances["USD"]);
//            Assert.AreEqual(1799949898.80m, _SellOrderBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE]);
//            Assert.AreEqual(99900m, _SellOrderBlock.Balances["Custom.USD"]);

//        }

//        [TestMethod]
//        public void BuyTradeOrderBlock() // 
//        {
//            CreateFirstGenesisBlock();
//            CreateUSDToken(false);
//            //SendTransfer();
//            ProcessSendGRFT();
//            ProcessReceiveGRFT();
//            var result = BuyOrderUSDAcc2();

//            Assert.AreEqual(APIResultCodes.Success, result);

//            //            Assert.AreEqual((50000 - 100 * 5) * Math.Pow(10, 2), _BuyOrderBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE]);
//            Assert.AreEqual((50000 - 100 * 5), _BuyOrderBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE]);
//        }

//        [TestMethod]
//        public void SellAndBuyTradeOrderBlock() // sell from Account 1, and buy from account 2
//        {
//            CreateFirstGenesisBlock();
//            CreateUSDToken(false);
//            //SendTransfer();
//            ProcessSendGRFT();
//            ProcessReceiveGRFT();
//            SellOrderUSDAcc1();
//            var result = BuyOrderUSDAcc2();

//            Assert.AreEqual(APIResultCodes.TradeOrderMatchFound, result);
//        }

//        [TestMethod]
//        public void Trade() // 
//        {
//            CreateFirstGenesisBlock();
//            CreateUSDToken(false);
//            //SendTransfer();
//            ProcessSendGRFT();
//            ProcessReceiveGRFT();
//            SellOrderUSDAcc1();
//            BuyOrderUSDAcc2();

//            Assert.AreNotEqual(null, _TradeBlock);

//            var result = ProcessTrade();

//            Assert.AreEqual(APIResultCodes.Success, result);

//            //Assert.AreEqual(49500 * Math.Pow(10, 2), _TradeBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE]);
//            Assert.AreEqual(49500, _TradeBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE]);
//        }

//        [TestMethod]
//        public void RPC_Test_LookForNewTrade_NoTokensSpecified() // 
//        {
//            CreateFirstGenesisBlock();
//            CreateUSDToken(false);
//            //SendTransfer();
//            ProcessSendGRFT();
//            ProcessReceiveGRFT();
//            SellOrderUSDAcc1();
//            BuyOrderUSDAcc2();
//            ProcessTrade();

//            var rpc = new RPCMethods(serviceAccount, accountCollection, tradeMatchEngine);
//            var result = rpc.LookForNewTrade(AccountId1, null, null, SignAPICall(PrivateKey1)).Result;

//            Assert.AreEqual(APIResultCodes.Success, result.ResultCode);
//        }

//        [TestMethod]
//        public void RPC_Test_LookForNewTrade_BuyTokensSpecified() // 
//        {
//            CreateFirstGenesisBlock();
//            CreateUSDToken(false);
//            //SendTransfer();
//            ProcessSendGRFT();
//            ProcessReceiveGRFT();
//            SellOrderUSDAcc1();
//            BuyOrderUSDAcc2();
//            ProcessTrade();

//            var rpc = new RPCMethods(serviceAccount, accountCollection, tradeMatchEngine);
//            var result = rpc.LookForNewTrade(AccountId1, "Custom.USD", null, SignAPICall(PrivateKey1)).Result;

//            Assert.AreEqual(APIResultCodes.Success, result.ResultCode);
//        }

//        [TestMethod]
//        public void RPC_Test_LookForNewTrade_SellTokensSpecified() // 
//        {
//            CreateFirstGenesisBlock();
//            CreateUSDToken(false);
//            //SendTransfer();
//            ProcessSendGRFT();
//            ProcessReceiveGRFT();
//            SellOrderUSDAcc1();
//            BuyOrderUSDAcc2();
//            ProcessTrade();

//            var rpc = new RPCMethods(serviceAccount, accountCollection, tradeMatchEngine);
//            var result = rpc.LookForNewTrade(AccountId1, null, TokenGenesisBlock.LYRA_TICKER_CODE, SignAPICall(PrivateKey1)).Result;

//            Assert.AreEqual(APIResultCodes.Success, result.ResultCode);
//        }

//        [TestMethod]
//        public void RPC_Test_LookForNewTrade_BothTokensSpecified() // 
//        {
//            CreateFirstGenesisBlock();
//            CreateUSDToken(false);
//            //SendTransfer();
//            ProcessSendGRFT();
//            ProcessReceiveGRFT();
//            SellOrderUSDAcc1();
//            BuyOrderUSDAcc2();
//            ProcessTrade();

//            var rpc = new RPCMethods(serviceAccount, accountCollection, tradeMatchEngine);
//            var result = rpc.LookForNewTrade(AccountId1, "Custom.USD", TokenGenesisBlock.LYRA_TICKER_CODE, SignAPICall(PrivateKey1)).Result;

//            Assert.AreEqual(APIResultCodes.Success, result.ResultCode);
//        }

//        [TestMethod]
//        public void RPC_Test_LookForNewTrade_WrongAccount() // 
//        {
//            CreateFirstGenesisBlock();
//            CreateUSDToken(false);
//            //SendTransfer();
//            ProcessSendGRFT();
//            ProcessReceiveGRFT();
//            SellOrderUSDAcc1();
//            BuyOrderUSDAcc2();
//            ProcessTrade();

//            var rpc = new RPCMethods(serviceAccount, accountCollection, tradeMatchEngine);
//            var result = rpc.LookForNewTrade(AccountId2, null, null, SignAPICall(PrivateKey2)).Result;

//            Assert.AreEqual(APIResultCodes.NoTradesFound, result.ResultCode);
//        }


//        [TestMethod]
//        public void RPC_Test_LookForNewTrade_WrongBuyTokensSpecified() // 
//        {
//            CreateFirstGenesisBlock();
//            CreateUSDToken(false);
//            //SendTransfer();
//            ProcessSendGRFT();
//            ProcessReceiveGRFT();
//            SellOrderUSDAcc1();
//            BuyOrderUSDAcc2();
//            ProcessTrade();

//            var rpc = new RPCMethods(serviceAccount, accountCollection, tradeMatchEngine);
//            var result = rpc.LookForNewTrade(AccountId1, TokenGenesisBlock.LYRA_TICKER_CODE, null, SignAPICall(PrivateKey1)).Result;

//            Assert.AreEqual(APIResultCodes.NoTradesFound, result.ResultCode);
//        }

//        [TestMethod]
//        public void RPC_Test_LookForNewTrade_WrongSellTokensSpecified() // 
//        {
//            CreateFirstGenesisBlock();
//            CreateUSDToken(false);
//            //SendTransfer();
//            ProcessSendGRFT();
//            ProcessReceiveGRFT();
//            SellOrderUSDAcc1();
//            BuyOrderUSDAcc2();
//            ProcessTrade();

//            var rpc = new RPCMethods(serviceAccount, accountCollection, tradeMatchEngine);
//            var result = rpc.LookForNewTrade(AccountId1, null, "Custom.USD", SignAPICall(PrivateKey1)).Result;

//            Assert.AreEqual(APIResultCodes.NoTradesFound, result.ResultCode);
//        }

//        [TestMethod]
//        public void ExecuteTrade_1() // 
//        {
//            CreateFirstGenesisBlock();
//            CreateUSDToken(false);
//            //SendTransfer();
//            ProcessSendGRFT();
//            ProcessReceiveGRFT();
//            SellOrderUSDAcc1();
//            BuyOrderUSDAcc2();
//            ProcessTrade();

//            var result = ProcessExecuteTrade();

//            Assert.AreEqual(APIResultCodes.Success, result);

//            //Assert.AreEqual(1799949898.80 * Math.Pow(10, 2), _ExecuteTradeOrderBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE]);
//            //Assert.AreEqual(99900 * Math.Pow(10, USD_PRECISION), _ExecuteTradeOrderBlock.Balances["USD"]);
//            Assert.AreEqual(1799949898.80m, _ExecuteTradeOrderBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE]);
//            Assert.AreEqual(99900m, _ExecuteTradeOrderBlock.Balances["Custom.USD"]);
//        }

//        [TestMethod]
//        public void ExecuteTrade_2() // 
//        {
//            CreateFirstGenesisBlock();
//            CreateUSDToken(true, AccountId1);
//            //SendTransfer();
//            ProcessSendGRFT();
//            ProcessReceiveGRFT();
//            SellOrderUSDAcc1();
//            BuyOrderUSDAcc2();
//            ProcessTrade();

//            var result = ProcessExecuteTrade();

//            Assert.AreEqual(APIResultCodes.MissingNonFungibleToken, result);

//            //Assert.AreEqual(1799949898.80 * Math.Pow(10, 2), _ExecuteTradeOrderBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE]);
//            //Assert.AreEqual(99900 * Math.Pow(10, USD_PRECISION), _ExecuteTradeOrderBlock.Balances["USD"]);
//            Assert.AreEqual(1799949898.80m, _ExecuteTradeOrderBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE]);
//            Assert.AreEqual(99900m, _ExecuteTradeOrderBlock.Balances["Custom.USD"]);
//        }

//        [TestMethod]
//        public void ExecuteTrade_3() // 
//        {
//            CreateFirstGenesisBlock();
//            CreateUSDToken(true, AccountId1);
//            //SendTransfer();
//            ProcessSendGRFT();
//            ProcessReceiveGRFT();
//            SellOrderUSDAcc1();
//            BuyOrderUSDAcc2();
//            ProcessTrade();

//            var discount_token = new NonFungibleToken();
//            discount_token.TokenCode = "Custom.USD";
//            discount_token.Denomination = 100;// * (long)Math.Pow(10, USD_PRECISION);
//            discount_token.ExpirationDate = DateTime.Now;
//            discount_token.RedemptionCode = "12345";

//            discount_token.SerialNumber = discount_token.CalculateHash();
//            discount_token.Sign(PrivateKey1);

//            var result = ProcessExecuteTrade(discount_token);

//            Assert.AreEqual(APIResultCodes.Success, result);

//            //Assert.AreEqual(1799949898.80 * Math.Pow(10, 2), _ExecuteTradeOrderBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE]);
//            //Assert.AreEqual(99900 * Math.Pow(10, USD_PRECISION), _ExecuteTradeOrderBlock.Balances["USD"]);
//            Assert.AreEqual(1799949898.80m, _ExecuteTradeOrderBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE]);
//            Assert.AreEqual(99900m, _ExecuteTradeOrderBlock.Balances["Custom.USD"]);
//        }

//        [TestMethod]
//        public void ReceiveTradeAcc1() // 
//        {
//            CreateFirstGenesisBlock();
//            CreateUSDToken(false);
//            //SendTransfer();
//            ProcessSendGRFT();
//            ProcessReceiveGRFT();
//            SellOrderUSDAcc1();
//            BuyOrderUSDAcc2();
//            ProcessTrade();
//            ProcessExecuteTrade();

//            var result = ProcessReceiveAcc1();

//            Assert.AreEqual(APIResultCodes.Success, result);

//            //Assert.AreEqual(1799950398.80 * Math.Pow(10, 2), _ReceiveTransferBlockAcc1.Balances[TokenGenesisBlock.LYRA_TICKER_CODE]);
//            //Assert.AreEqual(99900 * Math.Pow(10, USD_PRECISION), _ReceiveTransferBlockAcc1.Balances["USD"]);
//            Assert.AreEqual(1799950398.80m, _ReceiveTransferBlockAcc1.Balances[TokenGenesisBlock.LYRA_TICKER_CODE]);
//            Assert.AreEqual(99900m, _ReceiveTransferBlockAcc1.Balances["Custom.USD"]);
//        }

//        [TestMethod]
//        public void ReceiveTradeAcc2() // 
//        {
//            CreateFirstGenesisBlock();
//            CreateUSDToken(false);
//            //SendTransfer();
//            ProcessSendGRFT();
//            ProcessReceiveGRFT();
//            SellOrderUSDAcc1();
//            BuyOrderUSDAcc2();
//            ProcessTrade();
//            ProcessExecuteTrade();
//            ProcessReceiveAcc1();

//            var result = ProcessReceiveAcc2();

//            Assert.AreEqual(APIResultCodes.Success, result);

//            //Assert.AreEqual(49500 * Math.Pow(10, 2), _ReceiveTransferBlockAcc2.Balances[TokenGenesisBlock.LYRA_TICKER_CODE]);
//            //Assert.AreEqual(100 * Math.Pow(10, USD_PRECISION), _ReceiveTransferBlockAcc2.Balances["USD"]);
//            Assert.AreEqual(49500, _ReceiveTransferBlockAcc2.Balances[TokenGenesisBlock.LYRA_TICKER_CODE]);
//            Assert.AreEqual(100, _ReceiveTransferBlockAcc2.Balances["Custom.USD"]);
//        }

//        APIResultCodes ProcessReceiveAcc2()
//        {
//            var authorizer = new ReceiveTransferAuthorizer(serviceAccount, accountCollection);
//            var block = CreateReceiveBlockAcc2();
//            return authorizer.Authorize<ReceiveTransferBlock>(ref block);
//        }

//        ReceiveTransferBlock CreateReceiveBlockAcc2()
//        {
//            _ReceiveTransferBlockAcc2 = new ReceiveTransferBlock
//            {
//                AccountID = AccountId2,
//                ServiceHash = string.Empty,
//                SourceHash = _ExecuteTradeOrderBlock.Hash,
//                Balances = new Dictionary<string, decimal>()
//            };

//            _ReceiveTransferBlockAcc2.Balances.Add(_TradeBlock.BuyTokenCode, _TradeBlock.BuyAmount);

//            // transfer unchanged token balances from the previous block
//            foreach (var balance in _TradeBlock.Balances)
//                if (!(_ReceiveTransferBlockAcc2.Balances.ContainsKey(balance.Key)))
//                    _ReceiveTransferBlockAcc2.Balances.Add(balance.Key, balance.Value);

//            _ReceiveTransferBlockAcc2.InitializeBlock(_TradeBlock, PrivateKey2, NETWORK_ID);

//            //_ReceiveTransferBlockAcc2.Signature = Signatures.GetSignature(PrivateKey2, _ReceiveTransferBlockAcc2.Hash);

//            return _ReceiveTransferBlockAcc2;

//        }


//        APIResultCodes ProcessReceiveAcc1()
//        {
//            var authorizer = new ReceiveTransferAuthorizer(serviceAccount, accountCollection);
//            var block = CreateReceiveBlockAcc1();
//            return authorizer.Authorize<ReceiveTransferBlock>(ref block);
//        }

//        ReceiveTransferBlock CreateReceiveBlockAcc1()
//        {
//            _ReceiveTransferBlockAcc1 = new ReceiveTransferBlock
//            {
//                AccountID = AccountId1,
//                ServiceHash = string.Empty,
//                SourceHash = _TradeBlock.Hash,
//                Balances = new Dictionary<string, decimal>(),
//                FeeType = AuthorizationFeeTypes.NoFee
//            };

//            //TransactionInfo transaction = _TradeBlock.GetTransaction(_OpenAccountBlock);

//            //_ReceiveTransferBlockAcc1.Balances.Add(transaction.TokenCode, _ExecuteTradeOrderBlock.Balances[transaction.TokenCode]+ transaction.Amount);
//            _ReceiveTransferBlockAcc1.Balances.Add(_TradeBlock.SellTokenCode, _ExecuteTradeOrderBlock.Balances[_TradeBlock.SellTokenCode] + _TradeBlock.SellAmount);

//            // transfer unchanged token balances from the previous block
//            foreach (var balance in _ExecuteTradeOrderBlock.Balances)
//                if (!(_ReceiveTransferBlockAcc1.Balances.ContainsKey(balance.Key)))
//                    _ReceiveTransferBlockAcc1.Balances.Add(balance.Key, balance.Value);

//            _ReceiveTransferBlockAcc1.InitializeBlock(_ExecuteTradeOrderBlock, PrivateKey1, NETWORK_ID);

//            //_ReceiveTransferBlockAcc1.Signature = Signatures.GetSignature(PrivateKey1, _ReceiveTransferBlockAcc1.Hash);

//            return _ReceiveTransferBlockAcc1;

//        }


//        APIResultCodes ProcessExecuteTrade(NonFungibleToken discount_token = null)
//        {
//            var authorizer = new ExecuteTradeOrderAuthorizer(serviceAccount, accountCollection, tradeMatchEngine);
//            var block = CreateExecuteTradeBlock(_SellOrderBlock, _TradeBlock, _SellOrderBlock, discount_token);
//            return authorizer.Authorize<ExecuteTradeOrderBlock>(ref block);
//        }

//        ExecuteTradeOrderBlock CreateExecuteTradeBlock(TransactionBlock previousBlock, TradeBlock trade, TradeOrderBlock order, NonFungibleToken discount_token = null)
//        {
//            _ExecuteTradeOrderBlock = new ExecuteTradeOrderBlock()
//            {
//                AccountID = AccountId1,
//                DestinationAccountId = AccountId2,
//                Balances = new Dictionary<string, decimal>(),
//                TradeId = trade.Hash,
//                TradeOrderId = trade.TradeOrderId,
//                SellTokenCode = "Custom.USD",
//                //BuyTokenCode = TokenGenesisBlock.LYRA_TICKER_CODE,
//                SellAmount = 100,// * (long)Math.Pow(10, USD_PRECISION),
//                //BuyAmount = 500 * 100
//                Fee = serviceAccount.GetLastServiceBlock().TradeFee * 2,
//                FeeType = AuthorizationFeeTypes.BothParties
//            };


//            // no change in USD balance as the entire Tx amount was previously "locked" by the original order 
//            _ExecuteTradeOrderBlock.Balances.Add("Custom.USD", previousBlock.Balances["Custom.USD"]);
//            // no change in LGT balance as the fee amount was previously "locked" by the original order
//            _ExecuteTradeOrderBlock.Balances.Add(TokenGenesisBlock.LYRA_TICKER_CODE, previousBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE]);

//            // The fee, which was "reserved" by the originakl order, now is really paid
//            //_ExecuteTradeOrderBlock.Fee = (long) (0.1 * Math.Pow(10, 2) * 2);

//            if (order.CoverAnotherTradersFee)
//            {
//                _ExecuteTradeOrderBlock.Fee = serviceAccount.GetLastServiceBlock().TradeFee * 2;
//                _ExecuteTradeOrderBlock.FeeType = AuthorizationFeeTypes.BothParties;

//            }
//            else
//            if (order.AnotherTraderWillCoverFee)
//            {
//                _ExecuteTradeOrderBlock.Fee = 0;
//                _ExecuteTradeOrderBlock.FeeType = AuthorizationFeeTypes.NoFee;
//            }
//            else
//            {
//                _ExecuteTradeOrderBlock.Fee = serviceAccount.GetLastServiceBlock().TradeFee;
//                _ExecuteTradeOrderBlock.FeeType = AuthorizationFeeTypes.Regular;
//            }

//            _ExecuteTradeOrderBlock.FeeCode = TokenGenesisBlock.LYRA_TICKER_CODE;

//            _ExecuteTradeOrderBlock.NonFungibleToken = discount_token;

//            _ExecuteTradeOrderBlock.InitializeBlock(previousBlock, PrivateKey1, NETWORK_ID);
//            // _ExecuteTradeOrderBlock.Signature = Signatures.GetSignature(PrivateKey1, _ExecuteTradeOrderBlock.Hash);

//            return _ExecuteTradeOrderBlock;
//        }

//        APIResultCodes ProcessTrade()
//        {
//            var authorizer = new TradeAuthorizer(serviceAccount, accountCollection, tradeMatchEngine);
//            var block = CreateTradeBlock(_OpenAccountBlock);
//            return authorizer.Authorize<TradeBlock>(ref block);
//        }

//        // Buy 100 USD, at 5 LGT per 1 USD (= sell 500 LGT)
//        TradeBlock CreateTradeBlock(TransactionBlock previousBlock)
//        {
//            //int TradeFee;
//            //if (_SellOrderBlock.CoverAnotherTradersFee)
//            //{
//            //    TradeFee = 0;
//            //    _TradeBlock.FeeType = AuthorizationFeeTypes.NoFee;
//            //}
//            //else
//            //{
//            //    TradeFee = serviceAccount.GetLastServiceBlock().TradeFee;
//            //    _TradeBlock.FeeType = AuthorizationFeeTypes.Regular;
//            //}
//            //_TradeBlock.Fee = TradeFee;

//            //sbyte precision = _FirstGenesisBlock.Precision;

//            decimal balance_change = _TradeBlock.SellAmount;
//            if (_TradeBlock.SellTokenCode == TokenGenesisBlock.LYRA_TICKER_CODE)
//                balance_change = balance_change + _TradeBlock.Fee;

//            _TradeBlock.Balances.Add(_FirstGenesisBlock.Ticker, previousBlock.Balances[_FirstGenesisBlock.Ticker] - balance_change);

//            // transfer unchanged token balances from the previous block
//            foreach (var balance in previousBlock.Balances)
//                if (!(_TradeBlock.Balances.ContainsKey(balance.Key)))
//                    _TradeBlock.Balances.Add(balance.Key, balance.Value);

//            _TradeBlock.InitializeBlock(previousBlock, PrivateKey2, NETWORK_ID);
//            //_TradeBlock.Signature = Signatures.GetSignature(PrivateKey2, _TradeBlock.Hash);

//            return _TradeBlock;
//        }


//        APIResultCodes SellOrderUSDAcc1()
//        {
//            var authorizer = new TradeOrderAuthorizer(serviceAccount, accountCollection, tradeMatchEngine);
//            _SellOrderBlock = CreateSellOrderBlock(_SendTransferBlock);
//            return authorizer.Authorize<TradeOrderBlock>(ref _SellOrderBlock);
//        }

//        APIResultCodes BuyOrderUSDAcc2()
//        {
//            var authorizer = new TradeOrderAuthorizer(serviceAccount, accountCollection, tradeMatchEngine);
//            _BuyOrderBlock = CreateBuyOrderBlock(_OpenAccountBlock);
//            var result = authorizer.Authorize<TradeOrderBlock>(ref _BuyOrderBlock);
//            _TradeBlock = authorizer.MatchTradeBlock;

//            return result;
//        }


//        APIResultCodes ProcessSendGRFT()
//        {
//            _SendTransferBlock = CreateSendGRFTBlock(50000, _USDTokenBlock);
//            var authorizer = new SendTransferAuthorizer(serviceAccount, accountCollection);
//            return authorizer.Authorize<SendTransferBlock>(ref _SendTransferBlock);
//        }

//        APIResultCodes ProcessReceiveGRFT()
//        {
//            _OpenAccountBlock = CreateNewAccountBlock();
//            var authorizer = new NewAccountAuthorizer(serviceAccount, accountCollection);
//            return authorizer.Authorize<OpenWithReceiveTransferBlock>(ref _OpenAccountBlock);
//        }

//        APIResultCodes CreateUSDToken(bool isNonFungible, string nonfungible_public_key = null)
//        {
//            _USDTokenBlock = CreateUSDTokenBlock(_FirstGenesisBlock, isNonFungible, nonfungible_public_key);
//            var authorizer = new NewTokenAuthorizer(serviceAccount, accountCollection);

//            return authorizer.Authorize<TokenGenesisBlock>(ref _USDTokenBlock);
//        }
              



//        APIResultCodes CreateFirstGenesisBlock()
//        {

            

//            var authorizer = new GenesisAuthorizer(serviceAccount, accountCollection);

//            _FirstGenesisBlock = CreateGenesisBlock();

//            return authorizer.Authorize<LyraTokenGenesisBlock>(ref _FirstGenesisBlock);
//        }

//        LyraTokenGenesisBlock CreateGenesisBlock()
//        {
//            var block = new LyraTokenGenesisBlock
//            {
//                AccountType = AccountTypes.Standard,
//                Ticker = TokenGenesisBlock.LYRA_TICKER_CODE,
//                DomainName = "Lyra",
//                ContractType = ContractTypes.Cryptocurrency,
//                Description = "Representation of GRAFT token in Lyra",
//                Precision = TokenGenesisBlock.LYRA_PRECISION,
//                //Balances.Add // =  //10000000000, 
//                IsFinalSupply = true,
//                //CustomFee = 0,
//                //CustomFeeAccountId = string.Empty,
//                AccountID = AccountId1,
//                Balances = new Dictionary<string, decimal>(),
//                ServiceHash = string.Empty

//            };
//            // TO DO - set service hash
//            //var transaction = new TransactionInfo() { TokenCode = block.Ticker, Amount = 1800000000 * (long)Math.Pow(10, TokenGenesisBlock.LYRA_PRECISION) };
//            var transaction = new TransactionInfo() { TokenCode = block.Ticker, Amount = 1800000000 };
//            block.Balances.Add(transaction.TokenCode, transaction.Amount);

//            block.InitializeBlock(null, PrivateKey1, NETWORK_ID);

//            //block.Signature = Signatures.GetSignature(PrivateKey1, block.Hash);

//            return block;
//        }

//        TokenGenesisBlock CreateUSDTokenBlock(TransactionBlock previousBlock, bool isNonFungible, string nonfungible_public_key)
//        {
//            var TokenGenerationFee = serviceAccount.GetLastServiceBlock().TokenGenerationFee;

//            TokenGenesisBlock tokenBlock = new TokenGenesisBlock
//            {
//                Ticker = "Custom.USD",
//                DomainName = "Custom",
//                ContractType = ContractTypes.Cryptocurrency,
//                Description = "USD TEST",
//                Precision = 0,
//                IsFinalSupply = true,
//                AccountID = AccountId1,
//                Balances = new Dictionary<string, decimal>(),
//                ServiceHash = string.Empty,
//                Fee = TokenGenerationFee,
//                FeeType = AuthorizationFeeTypes.Regular,
//                IsNonFungible = isNonFungible,
//                NonFungibleKey = nonfungible_public_key
//            };

//            var transaction = new TransactionInfo() { TokenCode = "Custom.USD", Amount = 100000 };

//            tokenBlock.Balances.Add(transaction.TokenCode, transaction.Amount); // This is current supply in atomic units (1,000,000.00)
//            tokenBlock.Balances.Add(TokenGenesisBlock.LYRA_TICKER_CODE, previousBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE] - TokenGenerationFee);

//            tokenBlock.InitializeBlock(previousBlock, PrivateKey1, NETWORK_ID);

//            // tokenBlock.Signature = Signatures.GetSignature(PrivateKey1, tokenBlock.Hash);

//            return tokenBlock;
//        }

//        SendTransferBlock CreateSendGRFTBlock(decimal Amount, TransactionBlock previousBlock)
//        {
//            decimal TransferFee = serviceAccount.GetLastServiceBlock().TransferFee;

//            sbyte precision = _FirstGenesisBlock.Precision;

//            decimal balance_change = Amount;//(long)(Amount * Math.Pow(10, precision));

//            balance_change += TransferFee;

//            SendTransferBlock sendBlock = new SendTransferBlock
//            {
//                AccountID = AccountId1,
//                ServiceHash = string.Empty,
//                DestinationAccountId = AccountId2,
//                Balances = new Dictionary<string, decimal>(),
//                //PaymentID = string.Empty,
//                Fee = TransferFee,
//                FeeType = AuthorizationFeeTypes.Regular,
//                FeeCode = TokenGenesisBlock.LYRA_TICKER_CODE
//            };

//            sendBlock.Balances.Add(_FirstGenesisBlock.Ticker, previousBlock.Balances[_FirstGenesisBlock.Ticker] - balance_change);

//            // transfer unchanged token balances from the previous block
//            foreach (var balance in previousBlock.Balances)
//                if (!(sendBlock.Balances.ContainsKey(balance.Key)))
//                    sendBlock.Balances.Add(balance.Key, balance.Value);

//            sendBlock.InitializeBlock(previousBlock, PrivateKey1, NETWORK_ID);

//            //sendBlock.Signature = Signatures.GetSignature(PrivateKey1, sendBlock.Hash);

//            return sendBlock;

//        }



//        OpenWithReceiveTransferBlock CreateNewAccountBlock()
//        {
//            var openReceiveBlock = new OpenWithReceiveTransferBlock
//            {
//                AccountType = AccountTypes.Standard,
//                AccountID = AccountId2,
//                ServiceHash = string.Empty,
//                SourceHash = _SendTransferBlock.Hash,
//                Fee = 0,
//                FeeType = AuthorizationFeeTypes.NoFee,
//                Balances = new Dictionary<string, decimal>()
//            };

//            openReceiveBlock.Balances.Add(_FirstGenesisBlock.Ticker, _SendTransferBlock.GetTransaction(_USDTokenBlock).Amount);

//            openReceiveBlock.InitializeBlock(null, PrivateKey2, NETWORK_ID);

//            //openReceiveBlock.Signature = Signatures.GetSignature(PrivateKey2, openReceiveBlock.Hash);

//            return openReceiveBlock;
//        }

//        // Let's sell 100 USD for 5 LGT per USD
//        TradeOrderBlock CreateSellOrderBlock(TransactionBlock previousBlock)
//        {
//            decimal MaxAmount = 100;
//            decimal MinAmount = 0;
//            decimal Price = 5;

//            sbyte sell_token_precision = _USDTokenBlock.Precision;
//            sbyte buy_token_precision = _FirstGenesisBlock.Precision;

//            //long atomic_amount; // that's the amount we "lock" (send to no one) no matter it's buy or sell order

//            // For sell order: how many buy tokens are needed to buy one sell token
//            //long atomic_price; // that's the price (selling or buying depending on order); the price of the order and the trade should match

//            //long max_atomic_amount; // that's the max buy for buy order and max sell for sell order
//            //long min_atomic_amount; // that's the minimum amount of the trade (we don;t want to pay fees for a thousand of trades with frusctions

//            // sell order locks (sends to nowhere) the MaxAmount or sell tokens
//            //atomic_amount = (long)(MaxAmount * (decimal)Math.Pow(10, sell_token_precision));
//            //max_atomic_amount = atomic_amount;

//            //min_atomic_amount = (long)(MinAmount * (decimal)Math.Pow(10, sell_token_precision));

//            // For sell order: how many buy tokens are needed to buy one sell token
//            //atomic_price = (long)(Price * (decimal)Math.Pow(10, buy_token_precision));


//            // Let's handle the fees. We don't pay fees for placing orders but we have to reserve funds so we could pay a fee for trading 
//            decimal trading_fee = serviceAccount.GetLastServiceBlock().TradeFee;
//            trading_fee *= 2;

//            var tradeBlock = new TradeOrderBlock
//            {
//                AccountID = AccountId1,
//                ServiceHash = string.Empty,
//                DestinationAccountId = string.Empty, // we are sending to nowhere
//                Balances = new Dictionary<string, decimal>(),
//                //PaymentID = string.Empty,
//                Fee = 0, // We don't pay fees for placing orders
//                FeeCode = TokenGenesisBlock.LYRA_TICKER_CODE,
//                FeeType = AuthorizationFeeTypes.NoFee,
//                TradeAmount = MaxAmount,
//                MinTradeAmount = MinAmount,
//                Price = Price,
//                MaxQuantity = 1,
//                SellTokenCode = _USDTokenBlock.Ticker,
//                BuyTokenCode = _FirstGenesisBlock.Ticker,
//                OrderType = TradeOrderTypes.Sell,
//                CoverAnotherTradersFee = true,
//                AnotherTraderWillCoverFee = false
//            };

//            tradeBlock.Balances.Add(tradeBlock.SellTokenCode, previousBlock.Balances[tradeBlock.SellTokenCode] - MaxAmount);

//            // We have to count for the fee here to make sure we lock enough funds to pay fee later in ExecuteTradeOrder or Trade Block.
//            // for customer tokens, we pay fee in LYR (unless they are accepted by authorizers as a fee - TO DO)
//            tradeBlock.Balances.Add(TokenGenesisBlock.LYRA_TICKER_CODE, previousBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE] - trading_fee);

//            // transfer unchanged token balances from the previous block
//            foreach (var balance in previousBlock.Balances)
//                if (!(tradeBlock.Balances.ContainsKey(balance.Key)))
//                    tradeBlock.Balances.Add(balance.Key, balance.Value);

//            tradeBlock.InitializeBlock(previousBlock, PrivateKey1, NETWORK_ID);
//            //tradeBlock.Signature = Signatures.GetSignature(PrivateKey1, tradeBlock.Hash);

//            return tradeBlock;
//        }

//        // Let's buy 100 USD for 5 LGT per USD
//        TradeOrderBlock CreateBuyOrderBlock(TransactionBlock previousBlock)
//        {
//            decimal TradeAmount = 100; // Buy 100 USD

//            decimal Price = 5; // pay (sell) 5 LGT per 1 USD
//            //bool CoverAnotherTradersFee = false;

//            sbyte sell_token_precision = _FirstGenesisBlock.Precision;
//            sbyte buy_token_precision = _USDTokenBlock.Precision;

//            //long atomic_sell_amount; // that's the amount we "lock" (send to no one) no matter it's buy or sell order

//            //long atomic_price; // that's the price (selling or buying depending on order); the price of the order and the trade should match

//            // long atomic_trade_amount; // that's the max buy for buy order and max sell for sell order



//            // For buy order: how many sell tokens are needed to buy one buy token
//            //atomic_price = (long)(Price * (decimal)Math.Pow(10, sell_token_precision));
//            //atomic_trade_amount = (long)(TradeAmount * (decimal)Math.Pow(10, buy_token_precision));

//            // buy order locks (sends to nowhere) the MaxAmount of Sell tokens multiplied by Price
//            decimal sell_amount = TradeAmount * Price; //(long)(TradeAmount * (long)Price * (decimal)Math.Pow(10, sell_token_precision));

//            // Let's handle the fees. We don't pay fees for placing orders but we have to reserve funds so we could pay a fee for trading 
//            //int trading_fee = serviceAccount.GetLastServiceBlock().TradeFee;
//            decimal trading_fee = 0;
//            //if (CoverAnotherTradersFee)
//            //    trading_fee *= 2;

//            var tradeBlock = new TradeOrderBlock
//            {
//                AccountID = AccountId2,
//                ServiceHash = string.Empty,
//                DestinationAccountId = string.Empty, // we are sending to nowhere
//                Balances = new Dictionary<string, decimal>(),

//                Fee = 0, // We don't pay fees for placing orders
//                FeeCode = TokenGenesisBlock.LYRA_TICKER_CODE,
//                FeeType = AuthorizationFeeTypes.NoFee,
//                TradeAmount = TradeAmount,
//                MinTradeAmount = 0,
//                Price = Price,
//                MaxQuantity = 1,
//                SellTokenCode = _FirstGenesisBlock.Ticker,
//                BuyTokenCode = _USDTokenBlock.Ticker,
//                OrderType = TradeOrderTypes.Buy,
//                CoverAnotherTradersFee = false,
//                AnotherTraderWillCoverFee = true
//            };

//            tradeBlock.Balances.Add(tradeBlock.SellTokenCode, previousBlock.Balances[tradeBlock.SellTokenCode] - (sell_amount + trading_fee));

//            // transfer unchanged token balances from the previous block
//            foreach (var balance in previousBlock.Balances)
//                if (!(tradeBlock.Balances.ContainsKey(balance.Key)))
//                    tradeBlock.Balances.Add(balance.Key, balance.Value);

//            tradeBlock.InitializeBlock(previousBlock, PrivateKey2, NETWORK_ID);
//            // tradeBlock.Signature = Signatures.GetSignature(PrivateKey2, tradeBlock.Hash);

//            return tradeBlock;
//        }


//    }
//}