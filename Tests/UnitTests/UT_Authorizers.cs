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
using Lyra.Data.Blocks;
using Lyra.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Neo;
using Neo.Network.P2P;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;

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
        private AuthorizersFactory af;
        private DagSystem sys;

        private string networkId;
        private Wallet genesisWallet;
        private Wallet testWallet;
        private Wallet test2Wallet;
        private Wallet test3Wallet;

        Random _rand = new Random();

        ILyraAPI client;

        [TestInitialize]
        public void TestSetup()
        {
            SimpleLogger.Factory = new NullLoggerFactory();

            var probe = CreateTestProbe();
            var ta = new TestAuthorizer(probe);
            sys = ta.TheDagSystem;
            sys.StartConsensus();
            store = ta.TheDagSystem.Storage;

            af = new AuthorizersFactory();
            af.Init();
        }

        [TestCleanup]
        public void Cleanup()
        {
            //store.Delete(true);
            Shutdown();
        }

        private async Task<bool> AuthAsync(Block block)
        {
            if(block is TransactionBlock)
            {
                var accid = block is TransactionBlock tb ? tb.AccountID : "";
                Console.WriteLine($"Auth ({DateTime.Now:mm:ss.ff}): Hash: {block.Hash.Shorten()} {accid.Shorten()} {block.BlockType} Index: {block.Height}");
                var auth = af.Create(block.BlockType);
                var result = await auth.AuthorizeAsync(sys, block);
                Assert.IsTrue(result.Item1 == Lyra.Core.Blocks.APIResultCodes.Success, $"{result.Item1}");

                await store.AddBlockAsync(block);

                cs.Worker_OnConsensusSuccess(block, ConsensusResult.Yea, true);

                return result.Item1 == Lyra.Core.Blocks.APIResultCodes.Success;
            }
            else
            {
                // allow service block and consolidation block for now
                await store.AddBlockAsync(block);
                return true;
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
            cs.OnNewBlock += async (b) => (ConsensusResult.Yea, await AuthAsync(b) ? APIResultCodes.Success : APIResultCodes.UndefinedError);
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
                .Callback((SendTransferBlock block) => {
                    var t = Task.Run(async () => {
                        await AuthAsync(block);
                    });
                    Task.WaitAll(t);
                })
                .ReturnsAsync(new AuthorizationAPIResult { ResultCode = APIResultCodes.Success });
            mock.Setup(x => x.GetSyncHeightAsync())
                .ReturnsAsync(await api.GetSyncHeightAsync());

            mock.Setup(x => x.GetLastServiceBlockAsync())
                .Returns(() => Task.FromResult(api.GetLastServiceBlockAsync()).Result);
            mock.Setup(x => x.GetLastConsolidationBlockAsync())
                .Returns(() => Task.FromResult(api.GetLastConsolidationBlockAsync()).Result);

            mock.Setup(x => x.GetBlockHashesByTimeRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Returns<DateTime, DateTime>((acct, sign) => Task.FromResult(api.GetBlockHashesByTimeRangeAsync(acct, sign)).Result);

            // DEX
            mock.Setup(x => x.GetAllDexWalletsAsync())
                .Returns(() => Task.FromResult(api.GetAllDexWalletsAsync()).Result);
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

            mock.Setup(x => x.ReceiveTransferAsync(It.IsAny<ReceiveTransferBlock>()))
                .Callback((ReceiveTransferBlock block) => {
                    var t = Task.Run(async () => {
                        await AuthAsync(block);
                    });
                    Task.WaitAll(t);
                })
                .ReturnsAsync(new AuthorizationAPIResult { ResultCode = APIResultCodes.Success });
            mock.Setup(x => x.ReceiveTransferAndOpenAccountAsync(It.IsAny<OpenWithReceiveTransferBlock>()))
                .Callback((OpenWithReceiveTransferBlock block) => {
                    var t = Task.Run(async () => {
                        await AuthAsync(block);
                    });
                    Task.WaitAll(t);
                })
                .ReturnsAsync(new AuthorizationAPIResult { ResultCode = APIResultCodes.Success });
            mock.Setup(x => x.CreateTokenAsync(It.IsAny<TokenGenesisBlock>()))
                .Callback((TokenGenesisBlock block) => {
                    var t = Task.Run(async () => {
                        await AuthAsync(block);
                    });
                    Task.WaitAll(t);
                })
                .ReturnsAsync(new AuthorizationAPIResult { ResultCode = APIResultCodes.Success });

            var walletStor = new AccountInMemoryStorage();
            Wallet.Create(walletStor, "gensisi", "1234", networkId, sys.PosWallet.PrivateKey);

            genesisWallet = Wallet.Open(walletStor, "gensisi", "1234", client);
            await genesisWallet.SyncAsync(client);

            Assert.IsTrue(genesisWallet.BaseBalance > 1000000m);

            var tamount = 1000000m;
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

            await CreateConsolidation();
            await store.UpdateStatsAsync();
            var pending = await store.GetPendingReceiveAsync(testWallet.AccountId);
            Assert.AreEqual(923, pending);

            // test 2 wallet
            var walletStor3 = new AccountInMemoryStorage();
            Wallet.Create(walletStor3, "xunit2", "1234", networkId, test2PrivateKey);
            test2Wallet = Wallet.Open(walletStor3, "xunit2", "1234", client);
            Assert.AreEqual(test2Wallet.AccountId, test2PublicKey);

            await test2Wallet.SyncAsync(client);
            //Assert.AreEqual(test2Wallet.BaseBalance, tamount);

            //await TestPoolAsync();
            //await TestProfitingAndStaking();
            //await TestNodeFee();
            await TestDepositWithdraw();

            // let workflow to finish
            await Task.Delay(1000);
        }

        private async Task TestDepositWithdraw()
        {
            // prepare dex
            string lyrawalletfolder = Wallet.GetFullFolderName("xtest", "wallets");
            var walletStore = new SecuredWalletStore(lyrawalletfolder);
            var dexWallet = Wallet.Open(walletStore, "dex", "");
            await genesisWallet.SendAsync(10000000m, dexWallet.AccountId);
            await dexWallet.SyncAsync(genesisWallet.RPC);
            Assert.AreEqual(10000000m, dexWallet.BaseBalance);

            // external token genesis
            var tokenGenesisResult = await dexWallet.CreateTokenAsync("TRX", "tether", "", 8, 0, false, dexWallet.AccountId,
                "", "", ContractTypes.Cryptocurrency, null);
            Assert.IsTrue(tokenGenesisResult.Successful(), "dex token genesis failed");

            // create dex wallet
            await testWallet.SyncAsync(null);
            var crdexret = await testWallet.CreateDexWalletAsync("TRX", "default");
            Assert.IsTrue(crdexret.Successful());

            await Task.Delay(1000);
            var dexws = await testWallet.GetAllDexWalletsAsync();
            Assert.IsNotNull(dexws, "DEX Wallet not setup.");
            var wcnt = dexws.Count(a => a.ExtSymbol == "TRX" && a.ExtProvider == "default");
            Assert.AreEqual(1, wcnt, $"wallet not created properly. created: {wcnt}");

            // mint
            var dexbrk1 = dexws.First();
            var mintRet = await dexWallet.DexMintTokenAsync(dexbrk1.AccountID, 1000m);
            Assert.IsTrue(mintRet.Successful(), "Mint failed.");
            await Task.Delay(1000);

            var brk1lstret = await client.GetLastBlockAsync(dexbrk1.AccountID);
            Assert.IsTrue(brk1lstret.Successful());
            var brk1mint = brk1lstret.GetBlock() as TokenMintBlock;
            Assert.IsNotNull(brk1mint);
            Assert.AreEqual(2, brk1mint.Height, "No mint block generated.");
            Assert.AreEqual(1000, brk1mint.Balances["tether/TRX"].ToBalanceDecimal());
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
            Assert.IsTrue(svcret);

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
            Assert.IsTrue(crplret.Successful());
            await Task.Delay(2000);
            var pool = await testWallet.GetLiquidatePoolAsync(token0, "LYR");
            Assert.IsTrue(pool.PoolAccountId.StartsWith('L'));

            // add liquidate to pool
            var addpoolret = await testWallet.AddLiquidateToPoolAsync(token0, 1000000, "LYR", 5000);
            Assert.IsTrue(addpoolret.Successful());

            await Task.Delay(1000);

            // swap
            var poolx = await client.GetPoolAsync(token0, LyraGlobal.OFFICIALTICKERCODE);
            Assert.IsNotNull(poolx.PoolAccountId);
            var poolLatestBlock = poolx.GetBlock() as TransactionBlock;

            var cal2 = new SwapCalculator(LyraGlobal.OFFICIALTICKERCODE, token0, poolLatestBlock, LyraGlobal.OFFICIALTICKERCODE, 20, 0);
            var swapret = await testWallet.SwapTokenAsync("LYR", token0, "LYR", 20, cal2.SwapOutAmount);
            Assert.IsTrue(swapret.Successful());

            await Task.Delay(1000);

            // remove liquidate from pool
            var rmliqret = await testWallet.RemoveLiquidateFromPoolAsync(token0, "LYR");
            Assert.IsTrue(rmliqret.Successful());

            await Task.Delay(1000);
            await testWallet.SyncAsync(null);
        }
    }
}
