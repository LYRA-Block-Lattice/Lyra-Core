using Akka.Actor;
using Akka.TestKit;
using Akka.TestKit.Xunit2;
using FluentAssertions;
using Lyra;
using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Core.Utils;
using Lyra.Data.API;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.Blocks;
using Lyra.Data.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Neo;
using Neo.Network.P2P;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Lyra.Core.Accounts.Wallet;

namespace UnitTests
{
    [TestClass]
    public class UT_Authorizers : TestKit
    {
        readonly string testPrivateKey = "2LqBaZopCiPjBQ9tbqkqqyo4TSaXHUth3mdMJkhaBbMTf6Mr8u";
        readonly string testPublicKey = "LUTPLGNAP4vTzXh5tWVCmxUBh8zjGTR8PKsfA8E67QohNsd1U6nXPk4Q9jpFKsKfULaaT3hs6YK7WKm57QL5oarx8mZdbM";

        readonly string test2PrivateKey = "2XAGksPqMDxeSJVoE562TX7JzmCKna3i7AS9e4ZPmiTKQYATsy";
        string test2PublicKey = "LUTob2rWpFBZ6r3UxHhDYR8Utj4UDrmf1SFC25RpQxEfZNaA2WHCFtLVmURe1ty4ZNU9gBkCCrSt6ffiXKrRH3z9T3ZdXK";

        private ConsensusService cs;
        private IAccountCollectionAsync store;
        private DagSystem sys;

        private string networkId;
        private Wallet genesisWallet;
        private Wallet testWallet;
        private Wallet test2Wallet;
        private Wallet test3Wallet;

        Random _rand = new Random();

        ILyraAPI client;

        bool _authResult = true;
        StringBuilder _sbAuthResults = new StringBuilder();

        [TestInitialize]
        public void TestSetup()
        {
            SimpleLogger.Factory = new NullLoggerFactory();

            var probe = CreateTestProbe();
            var ta = new TestAuthorizer(probe);
            sys = ta.TheDagSystem;
            sys.StartConsensus();
            store = ta.TheDagSystem.Storage;

            //af = new AuthorizersFactory();
            //af.Init();
        }

        // when we create failure test case, call this
        private void ResetAuthFail()
        {
            _authResult = true;
            _sbAuthResults.Clear();
        }

        [TestCleanup]
        public void Cleanup()
        {
            //store.Delete(true);
            Shutdown();
        }

        private async Task<AuthorizationAPIResult> AuthAsync(Block block)
        {
            if(block is TransactionBlock)
            {
                var accid = block is TransactionBlock tb ? tb.AccountID : "";
                var auth = cs.AF.Create(block);
                var result = await auth.AuthorizeAsync(sys, block);

                Console.WriteLine($"Auth ({DateTime.Now:mm:ss.ff}): {block.BlockType} Index: {block.Height} Result: {result.Item1} Hash: {block.Hash.Shorten()} Account ID: {accid.Shorten()} ");
                //Assert.IsTrue(result.Item1 == Lyra.Core.Blocks.APIResultCodes.Success, $"Auth Failed: {result.Item1}");

                if (result.Item1 == APIResultCodes.Success)
                {
                    await store.AddBlockAsync(block);

                    cs.Worker_OnConsensusSuccess(block, ConsensusResult.Yea, true);
                }                    
                else
                {
                    _authResult = false;
                    _sbAuthResults.Append($"{result.Item1}, ");
                }

                return new AuthorizationAPIResult
                {
                    ResultCode = result.Item1,
                    TxHash = block.Hash,
                };
            }
            else
            {
                // allow service block and consolidation block for now
                await store.AddBlockAsync(block);
                return new AuthorizationAPIResult
                {
                    ResultCode = APIResultCodes.Success,
                    TxHash = block.Hash,
                };
            }
        }

        private async Task CreateTestBlockchainAsync()
        {
            networkId = "xtest";
            while (cs == null)
            {
                await Task.Delay(1000);
                cs = ConsensusService.Instance;
            }
            cs.OnNewBlock += async (b) => (ConsensusResult.Yea, (await AuthAsync(b)).ResultCode);
            //{
            //    var result = ;
            //    //return Task.FromResult( (ConsensusResult.Yea, result) );
            //}
            cs.Board.CurrentLeader = sys.PosWallet.AccountId;
            cs.Board.LeaderCandidate = sys.PosWallet.AccountId;
            ProtocolSettings.Default.StandbyValidators[0] = cs.Board.CurrentLeader;

            var svcGen = await cs.CreateServiceGenesisBlockAsync();
            //await AuthAsync(svcGen);
            await store.AddBlockAsync(svcGen);
            var tokenGen = cs.CreateLyraTokenGenesisBlock(svcGen);
            await AuthAsync(tokenGen);
            var pf = await cs.CreatePoolFactoryBlockAsync();
            await AuthAsync(pf);
            var consGen = cs.CreateConsolidationGenesisBlock(svcGen, tokenGen, pf);
            await AuthAsync(consGen);
            //await store.AddBlockAsync(consGen);

            NodeService.Dag = sys;
            var api = new NodeAPI();
            var apisvc = new ApiService(NullLogger<ApiService>.Instance);
            var mock = new Mock<ILyraAPI>();
            client = mock.Object;
            mock.Setup(x => x.SendTransferAsync(It.IsAny<SendTransferBlock>()))
                .Returns<SendTransferBlock>((a) => Task.FromResult(AuthAsync(a).GetAwaiter().GetResult()));
            mock.Setup(x => x.GetSyncHeightAsync())
                .ReturnsAsync(await api.GetSyncHeightAsync());

            mock.Setup(x => x.GetLastServiceBlockAsync())
                .Returns(() => Task.FromResult(api.GetLastServiceBlockAsync()).Result);
            mock.Setup(x => x.GetLastConsolidationBlockAsync())
                .Returns(() => Task.FromResult(api.GetLastConsolidationBlockAsync()).Result);

            mock.Setup(x => x.GetBlockHashesByTimeRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Returns<DateTime, DateTime>((acct, sign) => Task.FromResult(api.GetBlockHashesByTimeRangeAsync(acct, sign)).Result);

            // DEX
            mock.Setup(x => x.GetAllDexWalletsAsync(It.IsAny<string>()))
                .Returns<string>((owner) => Task.FromResult(api.GetAllDexWalletsAsync(owner)).Result);
            mock.Setup(x => x.FindDexWalletAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string, string>((owner, symbol, provider) => Task.FromResult(api.FindDexWalletAsync(owner, symbol, provider)).Result);

            mock.Setup(x => x.GetLastBlockAsync(It.IsAny<string>()))
                .Returns<string>(acct => Task.FromResult(api.GetLastBlockAsync(acct)).Result);
            mock.Setup(x => x.GetBlockBySourceHashAsync(It.IsAny<string>()))
                .Returns<string>(acct => Task.FromResult(api.GetBlockBySourceHashAsync(acct)).Result);
            mock.Setup(x => x.GetBlocksByRelatedTxAsync(It.IsAny<string>()))
                .Returns<string>(acct => Task.FromResult(api.GetBlocksByRelatedTxAsync(acct)).Result);
            mock.Setup(x => x.LookForNewTransfer2Async(It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string>((acct, sign) => Task.FromResult(api.LookForNewTransfer2Async(acct, sign)).Result);
            mock.Setup(x => x.GetTokenGenesisBlockAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string, string>((acct, token, sign) => Task.FromResult(api.GetTokenGenesisBlockAsync(acct, token, sign)).Result);
            mock.Setup(x => x.GetPoolAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string>((acct, sign) => Task.FromResult(api.GetPoolAsync(acct, sign)).Result);

            // dao
            mock.Setup(x => x.GetDaoByNameAsync(It.IsAny<string>()))
                .Returns<string>(name => Task.FromResult(api.GetDaoByNameAsync(name)).Result);
            mock.Setup(x => x.GetOtcOrdersByOwnerAsync(It.IsAny<string>()))
                .Returns<string>(accountId => Task.FromResult(api.GetOtcOrdersByOwnerAsync(accountId)).Result);

            mock.Setup(x => x.ReceiveTransferAsync(It.IsAny<ReceiveTransferBlock>()))
                .Returns<ReceiveTransferBlock>((a) => Task.FromResult(AuthAsync(a).GetAwaiter().GetResult()));
            mock.Setup(x => x.ReceiveTransferAndOpenAccountAsync(It.IsAny<OpenWithReceiveTransferBlock>()))
                .Returns<OpenWithReceiveTransferBlock>((a) => Task.FromResult(AuthAsync(a).GetAwaiter().GetResult()));
            mock.Setup(x => x.CreateTokenAsync(It.IsAny<TokenGenesisBlock>()))
                .Returns<TokenGenesisBlock>((a) => Task.FromResult(AuthAsync(a).GetAwaiter().GetResult()));

            var walletStor = new AccountInMemoryStorage();
            Wallet.Create(walletStor, "gensisi", "1234", networkId, sys.PosWallet.PrivateKey);

            genesisWallet = Wallet.Open(walletStor, "gensisi", "1234", client);
            await genesisWallet.SyncAsync(client);

            Assert.IsTrue(genesisWallet.BaseBalance > 1000000m);

            var tamount = 1000000000m;
            var sendResult = await genesisWallet.SendAsync(tamount, testPublicKey);
            Assert.IsTrue(sendResult.Successful(), $"send error {sendResult.ResultCode}");
            var sendResult2 = await genesisWallet.SendAsync(tamount, test2PublicKey);
            Assert.IsTrue(sendResult2.Successful(), $"send error {sendResult.ResultCode}");
        }

        private async Task CreateDevnet()
        {
            networkId = "devnet";
            client = new LyraRestClient("win", "xunit", "1.0", "https://192.168.3.77:4504/api/Node/");

            var walletStor = new AccountInMemoryStorage();
            Wallet.Create(walletStor, "gensisi", "1234", networkId, "sVfBfv913fdXQ5pKiGU3KxV8Ee2vmQL7iHWDT1t4NzTqvTzj2");

            genesisWallet = Wallet.Open(walletStor, "gensisi", "1234", client);
            var ret = await genesisWallet.SyncAsync(client);
            Assert.IsTrue(ret == APIResultCodes.Success);
        }

        [TestMethod]
        public async Task FullTest()
        {
            await CreateTestBlockchainAsync();
            //await CreateDevnet();

            // test 1 wallet
            var walletStor2 = new AccountInMemoryStorage();
            Wallet.Create(walletStor2, "xunit", "1234", networkId, testPrivateKey);
            testWallet = Wallet.Open(walletStor2, "xunit", "1234", client);
            Assert.AreEqual(testWallet.AccountId, testPublicKey);

            await testWallet.SyncAsync(client);
            //Assert.AreEqual(testWallet.BaseBalance, tamount);
            var lastBalance = testWallet.BaseBalance;
            await genesisWallet.SendAsync(800, testWallet.AccountId);
            await genesisWallet.SendAsync(123, testWallet.AccountId);

            if(networkId == "xunit")
            {
                await CreateConsolidation();
                await store.UpdateStatsAsync();
                var pending = await store.GetPendingReceiveAsync(testWallet.AccountId);
                Assert.AreEqual(923, pending);
            }

            // test 2 wallet
            var walletStor3 = new AccountInMemoryStorage();
            Wallet.Create(walletStor3, "xunit2", "1234", networkId, test2PrivateKey);
            test2Wallet = Wallet.Open(walletStor3, "xunit2", "1234", client);
            Assert.AreEqual(test2Wallet.AccountId, test2PublicKey);

            await test2Wallet.SyncAsync(client);
            //Assert.AreEqual(test2Wallet.BaseBalance, tamount);

            await TestOTCTrade();
            await TestPoolAsync();
            await TestProfitingAndStaking();
            await TestNodeFee();
            ////await TestDepositWithdraw();

            // let workflow to finish
            await Task.Delay(1000);

            
        }

        private async Task TestOTCTrade()
        {
            var crypto = "unittest/ETH";
            // init. create token to sell
            var tokenGenesisResult = await testWallet.CreateTokenAsync("ETH", "unittest", "", 8, 100000, false, testWallet.AccountId,
                    "", "", ContractTypes.Cryptocurrency, null);
            Assert.IsTrue(tokenGenesisResult.Successful(), $"test otc token genesis failed: {tokenGenesisResult.ResultCode}");
            await Task.Delay(100);
            await testWallet.SyncAsync(null);
            var testbalance = testWallet.BaseBalance;

            // first create a DAO
            var name = "First DAO";
            var dcret = await testWallet.CreateDAOAsync(name);
            Assert.IsTrue(dcret.Successful(), $"failed to create DAO: {dcret.ResultCode}");

            await Task.Delay(100);

            var daoret = await testWallet.RPC.GetDaoByNameAsync(name);
            Assert.IsTrue(daoret.Successful(), $"Can't get DAO: {daoret.ResultCode}");

            var dao1 = daoret.GetBlock() as DaoRecvBlock;

            var order = new OTCOrder
            {
                daoid = dao1.AccountID,
                dir = Direction.Sell,
                crypto = crypto,
                fiat = "USD",
                priceType = PriceType.Fixed,
                price = 2000,
                amount = 10,
                sellerCollateral = 1000000,
            };

            await Task.Delay(100);
            var ret = await testWallet.CreateOTCOrderAsync(order);
            Assert.IsTrue(ret.Successful(), $"Can't create order: {ret.ResultCode}");

            await Task.Delay(200);
            var otcret = await testWallet.RPC.GetOtcOrdersByOwnerAsync(testWallet.AccountId);
            Assert.IsTrue(otcret.Successful(), $"Can't get otc gensis block. {otcret.ResultCode}");
            var otcs = otcret.GetBlocks();
            Assert.IsTrue(otcs.Count() == 1 && otcs.First() is OtcOrderGenesis, $"otc gensis block not found.");

            // then DAO treasure should have the crypto
            var daoret3 = await testWallet.RPC.GetDaoByNameAsync(name);
            Assert.IsTrue(daoret3.Successful(), $"Can't get DAO: {daoret3.ResultCode}");
            var daot = daoret3.GetBlock() as DaoRecvBlock;
            Assert.IsTrue(daot.Balances.ContainsKey(crypto), "No collateral token in DAO treasure.");
            Assert.AreEqual(10m, daot.Balances[crypto].ToBalanceDecimal());

            var otcg = otcs.First() as OtcOrderGenesis;
            Assert.IsTrue(order.Equals(otcg.Order), "OTC order not equal.");
            await Task.Delay(100);

            // here comes a buyer, he who want to buy 1 BTC.
            var trade = new OTCTrade
            {
                daoid = dao1.AccountID,
                orderid = otcg.AccountID,
                dir = Direction.Buy,
                crypto = "unittest/ETH",
                fiat = "USD",
                priceType = PriceType.Fixed,
                price = 2000,
                amount = 1,
                buyerCollateral = 1000000
            };
            await test2Wallet.SyncAsync(null);
            var test2balance = test2Wallet.BaseBalance;
            var traderet = await test2Wallet.CreateOTCTradeAsync(trade);
            Assert.IsTrue(traderet.Successful(), $"OTC Trade error: {traderet.ResultCode}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(traderet.TxHash), "No TxHash for trade create.");

            await Task.Delay(200);
            // the otc order should now be amount 9
            var otcret2 = await testWallet.RPC.GetOtcOrdersByOwnerAsync(testWallet.AccountId);
            Assert.IsTrue(otcret2.Successful(), $"Can't get otc block. {otcret2.ResultCode}");
            var otcs2 = otcret2.GetBlocks();
            Assert.IsTrue(otcs2.Count() == 1 && otcs2.First() is IOtcOrder, $"otc block count not = 1.");
            var otcorderx = otcs2.First() as IOtcOrder;
            Assert.AreEqual(9, otcorderx.Order.amount);

            // get trade
            var related = await test2Wallet.RPC.GetBlocksByRelatedTxAsync(traderet.TxHash);
            Assert.IsTrue(related.Successful(), $"Can't get rleated tx for trade genesis: {related.ResultCode}");
            var blks = related.GetBlocks();
            var tradgen = blks.FirstOrDefault(a => a is OtcTradeGenesisBlock) as OtcTradeGenesisBlock;
            Assert.IsNotNull(tradgen, $"Can't get trade genesis: blks count: {blks.Count()}");
            Assert.AreEqual(trade, tradgen.Trade);
            Assert.AreEqual(OtcTradeStatus.Open, tradgen.Status);

            // buyer send payment indicator
            var payindret = await test2Wallet.OTCTradeBuyerPaymentSentAsync(tradgen.AccountID);
            Assert.IsTrue(payindret.Successful(), $"Pay sent indicator error: {payindret.ResultCode}");

            await Task.Delay(100);
            // status changed to BuyerPaid
            var trdlatest = await test2Wallet.RPC.GetLastBlockAsync(tradgen.AccountID);
            Assert.IsTrue(trdlatest.Successful(), $"Can't get trade latest block: {trdlatest.ResultCode}");
            Assert.AreEqual(OtcTradeStatus.BuyerPaid, (trdlatest.GetBlock() as IOtcTrade).Status,
                $"Trade statust not changed to BuyerPaid");

            // seller got the payment
            var gotpayret = await testWallet.OTCTradeSellerGotPaymentAsync(tradgen.AccountID);
            Assert.IsTrue(payindret.Successful(), $"Got Payment indicator error: {payindret.ResultCode}");

            await Task.Delay(200);
            // status changed to BuyerPaid
            var trdlatest2 = await test2Wallet.RPC.GetLastBlockAsync(tradgen.AccountID);
            Assert.IsTrue(trdlatest2.Successful(), $"Can't get trade latest block: {trdlatest2.ResultCode}");
            Assert.AreEqual(OtcTradeStatus.ProductReleased, (trdlatest2.GetBlock() as IOtcTrade).Status,
                $"Trade statust not changed to ProductReleased");

            await test2Wallet.SyncAsync(null);
            Assert.AreEqual(test2balance - 13, test2Wallet.BaseBalance, $"Test2 got collateral wrong. should be {test2balance} but {test2Wallet.BaseBalance}");

            // trade is ok. now its time to clase the order
            var closeret = await testWallet.CloseOTCOrderAsync(dao1.AccountID, otcg.AccountID);
            Assert.IsTrue(closeret.Successful(), $"Unable to close order: {closeret.ResultCode}");

            await Task.Delay(100);
            var ordfnlret = await testWallet.RPC.GetLastBlockAsync(otcg.AccountID);
            Assert.IsTrue(ordfnlret.Successful(), $"Can't get order latest block: {ordfnlret.ResultCode}");
            Assert.AreEqual(OtcOrderStatus.Closed, (ordfnlret.GetBlock() as IOtcOrder).Status,
                $"Order statust not changed to Closed");

            await testWallet.SyncAsync(null);
            var lyrshouldbe = testbalance - 10016;
            Assert.AreEqual(lyrshouldbe, testWallet.BaseBalance, $"Test got collateral wrong. should be {lyrshouldbe} but {testWallet.BaseBalance}");
            var bal2 = testWallet.GetLatestBlock().Balances[crypto].ToBalanceDecimal();
            Assert.AreEqual(100000m - 1m, bal2,
                $"testwallet balance of crypto should be {100000m - 1m} but {bal2}");

            await Task.Delay(100);
            Assert.IsTrue(_authResult, $"Authorizer failed: {_sbAuthResults}");
            ResetAuthFail();
        }

        private async Task TestDepositWithdraw()
        {
            // prepare dex
            string lyrawalletfolder = Wallet.GetFullFolderName(networkId, "wallets");
            var walletStore = new SecuredWalletStore(lyrawalletfolder);
            var dexWallet = Wallet.Open(walletStore, "dex", "");
            await genesisWallet.SendAsync(100000m, dexWallet.AccountId);
            await Task.Delay(1000);
            await dexWallet.SyncAsync(genesisWallet.RPC);
            Assert.IsTrue(dexWallet.BaseBalance >= 100000m);

            // external token genesis
            var tgexists = await client.GetTokenGenesisBlockAsync(null, "tether/TRX", null);
            if(!tgexists.Successful())
            {
                var tokenGenesisResult = await dexWallet.CreateTokenAsync("TRX", "tether", "", 8, 0, false, dexWallet.AccountId,
                        "", "", ContractTypes.Cryptocurrency, null);
                Assert.IsTrue(tokenGenesisResult.Successful(), "dex token genesis failed");
            }

            // create dex wallet
            await testWallet.SyncAsync(null);
            var crdexret = await testWallet.CreateDexWalletAsync("TRX", "native");
            Assert.IsTrue(crdexret.Successful());

            await Task.Delay(1000);
            var dexws = await testWallet.GetAllDexWalletsAsync(testWallet.AccountId);
            Assert.IsNotNull(dexws, "DEX Wallet not setup.");
            var wcnt = dexws.Count(a => (a as IDexWallet).ExtSymbol == "TRX" && (a as IDexWallet).ExtProvider == "native");
            Assert.AreEqual(1, wcnt, $"wallet not created properly. created: {wcnt}");

            // must fail
            //await testWallet.SyncAsync(null);
            //var getokretx = await testWallet.DexGetTokenAsync((dexws.First() as TransactionBlock).AccountID, 500m);
            //Assert.IsTrue(!getokretx.Successful(), "Should not success");

            // mint
            var dexbrk1 = dexws.First() as TransactionBlock;
            var mintRet = await dexWallet.DexMintTokenAsync(dexbrk1.AccountID, 1000m);
            Assert.IsTrue(mintRet.Successful(), "Mint failed.");
            await Task.Delay(1000);

            var brk1lstret = await client.GetLastBlockAsync(dexbrk1.AccountID);
            Assert.IsTrue(brk1lstret.Successful());
            var brk1mint = brk1lstret.GetBlock() as TokenMintBlock;
            Assert.IsNotNull(brk1mint);

            if(networkId == "xunit")
            {
                Assert.AreEqual(2, brk1mint.Height, "No mint block generated.");
                Assert.AreEqual(1000, brk1mint.Balances["tether/TRX"].ToBalanceDecimal());
            }

            // get minted token to owner wallet
            await testWallet.SyncAsync(null);
            var getokret = await testWallet.DexGetTokenAsync(dexbrk1.AccountID, 500m);
            Assert.IsTrue(getokret.Successful(), "error get ext token to own wallet");
            await Task.Delay(1500);
            await testWallet.SyncAsync(null);
            Assert.AreEqual(500m, testWallet.GetLatestBlock().Balances["tether/TRX"].ToBalanceDecimal(), "Ext token amount error");

            // put external token to dex wallet
            var putokret = await testWallet.DexPutTokenAsync(dexbrk1.AccountID, "tether/TRX", 500m);
            Assert.IsTrue(putokret.Successful(), "Put token error");
            await Task.Delay(1500);
            var brk1lstret2 = await client.GetLastBlockAsync(dexbrk1.AccountID);
            Assert.IsTrue(brk1lstret2.Successful());
            var brk1lastblk = brk1lstret2.GetBlock() as TransactionBlock;
            if(networkId == "xunit")
            {
                Assert.AreEqual(1000m, brk1lastblk.Balances["tether/TRX"].ToBalanceDecimal(), "brk1 ext tok balance error");
            }

            // withdraw token to external blockchain
            var wdwret = await testWallet.DexWithdrawTokenAsync(dexbrk1.AccountID, "Txxxxxxxxx", 1000m);
            Assert.IsTrue(wdwret.Successful(), "Error withdraw");
            await Task.Delay(1500);
            var brk1lstret3 = await client.GetLastBlockAsync(dexbrk1.AccountID);
            Assert.IsTrue(brk1lstret3.Successful());
            var brk1lastblk3 = brk1lstret3.GetBlock() as TokenBurnBlock;
            if(networkId == "xunit")
                Assert.AreEqual(0m, brk1lastblk3.Balances["tether/TRX"].ToBalanceDecimal(), "brk1 ext tok burn error");

        }

        private async Task CreateConsolidation()
        {
            await Task.Delay(1000);
            var lcon = await store.GetLastConsolidationBlockAsync();
            var unConsList = await testWallet.RPC.GetBlockHashesByTimeRangeAsync(lcon.TimeStamp.AddSeconds(18), DateTime.UtcNow);
            await cs.LeaderCreateConsolidateBlockAsync(lcon, DateTime.UtcNow, unConsList.Entities);
            var lcon2 = await testWallet.RPC.GetLastConsolidationBlockAsync();
            Assert.IsTrue(lcon.Height + 1 == lcon2.GetBlock().Height);
        }

        private async Task TestNodeFee()
        {
            // create service block
            var lsb = await testWallet.RPC.GetLastServiceBlockAsync();
            var svcb = await cs.CreateNewViewAsNewLeaderAsync();
            var svcret = await AuthAsync(svcb);
            Assert.IsTrue(svcret.Successful());

            var lsb2 = await testWallet.RPC.GetLastServiceBlockAsync();
            Assert.IsTrue(lsb.GetBlock().Height + 1 == lsb2.GetBlock().Height);

            var lconRet = await testWallet.RPC.GetLastConsolidationBlockAsync();
            Assert.IsTrue(lconRet.Successful());            

            await CreateConsolidation();

            // create a profiting account
            Console.WriteLine("Profiting gen");
            var crpftret = await genesisWallet.CreateProfitingAccountAsync($"moneycow{_rand.Next()}", ProfitingType.Node,
                0.5m, 50);
            Assert.IsTrue(crpftret.Successful());
            var pftblock = crpftret.GetBlock() as ProfitingBlock;
            Assert.IsTrue(pftblock.OwnerAccountId == genesisWallet.AccountId);

            await genesisWallet.CreateDividendsAsync(pftblock.AccountID);
            await Task.Delay(2 * 1000);
        }

        private async Task<IStaking> CreateStaking(Wallet w, string pftid, decimal amount)
        {
            var crstkret = await w.CreateStakingAccountAsync($"moneybag{_rand.Next()}", pftid, 30, true);
            Assert.IsTrue(crstkret.Successful());
            var stkblock = crstkret.GetBlock() as StakingBlock;
            Assert.IsTrue(stkblock.OwnerAccountId == w.AccountId);
            await Task.Delay(1000);

            var addstkret = await w.AddStakingAsync(stkblock.AccountID, amount);
            Assert.IsTrue(addstkret.Successful());
            await Task.Delay(1000);
            var stk = await w.GetStakingAsync(stkblock.AccountID);
            Assert.AreEqual(amount, (stk as TransactionBlock).Balances["LYR"].ToBalanceDecimal());
            return stk;
        }

        private async Task UnStaking(Wallet w, string stkid)
        {
            var balance = w.BaseBalance;
            var unstkret = await w.UnStakingAsync(stkid);
            Assert.IsTrue(unstkret.Successful());
            await Task.Delay(1000);
            await w.SyncAsync(null);
            var nb = balance + 2000m - 2;// * 0.988m; // two send fee
            //Assert.AreEqual(nb, w.BaseBalance);

            var stk2 = await w.GetStakingAsync(stkid);
            Assert.AreEqual((stk2 as TransactionBlock).Balances["LYR"].ToBalanceDecimal(), 0);
        }

        private async Task TestProfitingAndStaking()
        {
            var shareRito = 0.5m;
            var totalProfit = 10000m;

            // create a profiting account
            Console.WriteLine("Profiting gen");
            var crpftret = await testWallet.CreateProfitingAccountAsync($"moneycow{_rand.Next()}", ProfitingType.Node,
                shareRito, 50);
            Assert.IsTrue(crpftret.Successful());
            var pftblock = crpftret.GetBlock() as ProfitingBlock;
            Assert.IsTrue(pftblock.OwnerAccountId == testWallet.AccountId);

            Console.WriteLine("Staking 1");
            // create two staking account, add funds, and vote to it
            var stk = await CreateStaking(testWallet, pftblock.AccountID, 2000m);
            Console.WriteLine("Staking 2"); 
            var stk2 = await CreateStaking(test2Wallet, pftblock.AccountID, 2000m);

            // get the base balance
            await Task.Delay(1000);
            await testWallet.SyncAsync(null);
            await test2Wallet.SyncAsync(null);

            Console.WriteLine($"({DateTime.Now:mm:ss.ff}) send as profit");
            // send profit to profit account
            for(var i = 0; i < 1; i++)
            {
                var sendret = await genesisWallet.SendAsync(10000m, pftblock.AccountID);
                Assert.IsTrue(sendret.Successful());
            }

            Console.WriteLine($"({DateTime.Now:mm:ss.ff}) Dividend");
            // the owner try to get the dividends
            var getpftRet = await testWallet.CreateDividendsAsync(pftblock.AccountID);
            Assert.IsTrue(getpftRet.Successful(), $"Failed to get dividends: {getpftRet.ResultCode}");

            // then sync wallet and see if it gets a dividend
            await Task.Delay(3000);
            if (networkId == "devnet")
                await Task.Delay(3000);
            var bal1 = testWallet.BaseBalance;
            Console.WriteLine($"({DateTime.Now:mm:ss.ff}) Check balance");
            await testWallet.SyncAsync(null);
            var delta = testWallet.BaseBalance - bal1;
            //Assert.AreEqual(bal1 + 15000m, testWallet.BaseBalance);

            var bal2 = test2Wallet.BaseBalance;
            await test2Wallet.SyncAsync(null);
            //Assert.AreEqual(bal2 + totalProfit * shareRito / 2, test2Wallet.BaseBalance);

            await UnStaking(testWallet, (stk as TransactionBlock).AccountID);
            await UnStaking(test2Wallet, (stk2 as TransactionBlock).AccountID);
        }

        private async Task TestPoolAsync()
        {
            // create pool
            var token0 = "unnitest/test0";
            var token1 = "unnitest/test1";
            var secs0 = token0.Split('/');
            var result0 = await testWallet.CreateTokenAsync(secs0[1], secs0[0], "", 8, 50000000000, true, "", "", "", Lyra.Core.Blocks.ContractTypes.Cryptocurrency, null);
            Assert.IsTrue(result0.Successful(), "Failed to create token: " + result0.ResultCode);
            await testWallet.SyncAsync(null);

            var secs1 = token1.Split('/');
            var result1 = await testWallet.CreateTokenAsync(secs1[1], secs1[0], "", 8, 50000000000, true, "", "", "", Lyra.Core.Blocks.ContractTypes.Cryptocurrency, null);
            Assert.IsTrue(result0.Successful(), "Failed to create token: " + result1.ResultCode);
            await testWallet.SyncAsync(null);

            var crplret = await testWallet.CreateLiquidatePoolAsync(token0, "LYR");
            Assert.IsTrue(crplret.Successful(), $"Error create liquidate pool {crplret.ResultCode}");
            await Task.Delay(10000);
            var pool = await testWallet.GetLiquidatePoolAsync(token0, "LYR");
            Assert.IsTrue(pool.PoolAccountId != null && pool.PoolAccountId.StartsWith('L'), "Can't get pool created.");

            // add liquidate to pool
            var addpoolret = await testWallet.AddLiquidateToPoolAsync(token0, 1000000, "LYR", 5000);
            Assert.IsTrue(addpoolret.Successful());

            await Task.Delay(1000);

            // swap
            var poolx = await client.GetPoolAsync(token0, LyraGlobal.OFFICIALTICKERCODE);
            Assert.IsNotNull(poolx.PoolAccountId);
            var poolLatestBlock = poolx.GetBlock() as TransactionBlock;

            await testWallet.SyncAsync(null);
            await Task.Delay(1000);
            var oldtkn0 = testWallet.GetLatestBlock().Balances[token0].ToBalanceDecimal();
            var cal2 = new SwapCalculator(LyraGlobal.OFFICIALTICKERCODE, token0, poolLatestBlock, LyraGlobal.OFFICIALTICKERCODE, 20, 0);
            var swapret = await testWallet.SwapTokenAsync("LYR", token0, "LYR", 20, cal2.SwapOutAmount);
            Assert.IsTrue(swapret.Successful());
            await Task.Delay(2000);
            await testWallet.SyncAsync(null);
            await Task.Delay(1000);
            var gotamount = testWallet.GetLatestBlock().Balances[token0].ToBalanceDecimal() - oldtkn0;
            Console.WriteLine($"Got swapped amount {gotamount} {token0}");

            await Task.Delay(1000);

            // remove liquidate from pool
            var rmliqret = await testWallet.RemoveLiquidateFromPoolAsync(token0, "LYR");
            Assert.IsTrue(rmliqret.Successful());

            await Task.Delay(1000);
            await testWallet.SyncAsync(null);
        }
    }
}
