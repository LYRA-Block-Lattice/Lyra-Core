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
using Lyra.Core.WorkFlow;
using Lyra.Data.API;
using Lyra.Data.API.ODR;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.Blocks;
using Lyra.Data.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Neo;
using Neo.Network.P2P;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WorkflowCore.Interface;
using WorkflowCore.Services;
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

        readonly string test3PrivateKey = "2iWkVkodnhcvQvzQSnBKMU3PhMfhEfWVMRWC1S21qg4cNR9UxC";
        string test3PublicKey = "LUTnKnTaeZ95MaCCeA4Y7RZeLo5PrmAipuvaaHMvrpk3awbc7VBSWNRRuhQuA5qy5SGNh7imC71jaMCdttMN1a6DrSPTP6";

        readonly string test4PrivateKey = "yEEj2uvCQji75Qps4jZdPRZj7KtFoeW2dh7pmfXjEuYXK9Uz3";
        string test4PublicKey = "LUT5jYomQHCJQhG3Co7GadEtohpwwYtyYz1vABHGeDkLDpSJGXFfpYgD9XckRXQg2Hv2Yrb2Ade3jbecZpLf4hbVho6b5n";

        IHostEnv _env;
        private ConsensusService cs;
        private IAccountCollectionAsync store;
        private DagSystem sys;

        private string networkId;
        private Wallet genesisWallet;
        private Wallet testWallet;
        private Wallet test2Wallet;
        private Wallet test3Wallet;
        private Wallet test4Wallet;

        Random _rand = new Random();

        ILyraAPI client;

        bool _authResult = true;
        StringBuilder _sbAuthResults = new StringBuilder();

        AutoResetEvent _newAuth = new AutoResetEvent(false);
        AutoResetEvent _workflowEnds = new AutoResetEvent(false);
        List<string> _endedWorkflows = new List<string>();

        [TestInitialize]
        public void TestSetup()
        {
            SimpleLogger.Factory = new NullLoggerFactory();

            var probe = CreateTestProbe();
            var ta = new TestAuthorizer(probe);
            sys = ta.TheDagSystem;
            sys.StartConsensus();
            store = ta.TheDagSystem.Storage;

            // workflow init
            IServiceProvider serviceProvider = ConfigureServices();

            //start the workflow host
            var host = serviceProvider.GetService<IWorkflowHost>();

            var alltypes = typeof(DebiWorkflow)
                .Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(DebiWorkflow)) && !t.IsAbstract);

            foreach (var type in alltypes)
            {
                var methodInfo = typeof(WorkflowHost).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(a => a.Name == "RegisterWorkflow")
                    .Last();

                var genericMethodInfo = methodInfo.MakeGenericMethod(type, typeof(LyraContext));

                genericMethodInfo.Invoke(host, new object[] { });
            }
           
            host.OnStepError += Host_OnStepError;
            host.OnLifeCycleEvent += Host_OnLifeCycleEvent;
            host.Start();

            _env = serviceProvider.GetService<IHostEnv>();
            _env.SetWorkflowHost(host);

            //host.StartWorkflow("HelloWorld", 1, null, null);
        }

        object lifeo = new object();
        private void Host_OnLifeCycleEvent(WorkflowCore.Models.LifeCycleEvents.LifeCycleEvent evt)
        {
            lock(lifeo)
            {
                //Console.WriteLine($"Life: {evt.WorkflowInstanceId}: {evt.Reference}");
                if (evt.Reference == "end")
                {
                    if (!_endedWorkflows.Contains(evt.WorkflowInstanceId))
                    {
                        _endedWorkflows.Add(evt.WorkflowInstanceId);
                        var hash = cs.GetHashForWorkflow(evt.WorkflowInstanceId);
                        Console.WriteLine($"Key is {hash} terminated. Set it.");
                        _workflowEnds.Set();
                    }
                }
            }             
        }

        private void Host_OnStepError(WorkflowCore.Models.WorkflowInstance workflow, WorkflowCore.Models.WorkflowStep step, Exception exception)
        {
            Console.WriteLine($"Workflow Host Error: {workflow.Id} {step.Name} {exception}");
            _workflowEnds.Set();
        }

        private static IServiceProvider ConfigureServices()
        {
            //setup dependency injection
            IServiceCollection services = new ServiceCollection();
            services.AddLogging();
            services.AddWorkflow(cfg =>
            {
                //cfg.UseMongoDB(@"mongodb://localhost:27017", "workflow");
                cfg.UsePollInterval(new TimeSpan(0, 0, 0, 1));
                //cfg.UseElasticsearch(new ConnectionSettings(new Uri("http://elastic:9200")), "workflows");
            });

            services.AddTransient<Repeator>();
            services.AddTransient<ReqViewChange>();
            services.AddTransient<CustomMessage>();

            //services.AddTransient<DoSomething>();
            //services.AddTransient<IMyService, MyService>();
            services.AddSingleton<IHostEnv, TestEnv>();

            var serviceProvider = services.BuildServiceProvider();

            return serviceProvider;
        }

        //[TestMethod]
        //public void TestAuthorizerFactory()
        //{
        //    var af = new AuthorizersFactory();
        //    af.Init();
        //}

        // when we create failure test case, call this
        private void ResetAuthFail()
        {
            _authResult = true;
            _sbAuthResults.Clear();
            _workflowEnds.Reset();
        }

        [TestCleanup]
        public void Cleanup()
        {
            //store.Delete(true);
            Shutdown();
        }

        private async Task<AuthorizationAPIResult> AuthAsync(Block block)
        {
            try
            {
                _newAuth.Reset();
                if (block is TransactionBlock)
                {
                    var accid = block is TransactionBlock tb ? tb.AccountID : "";
                    var auth = cs.AF.Create(block);
                    var result = await auth.AuthorizeAsync(sys, block);

                    if(result.Item1 != APIResultCodes.Success)
                        Console.WriteLine($"Auth ({DateTime.Now:mm:ss.ff}): Height: {block.Height} Result: {result.Item1} Hash: {block.Hash.Shorten()} Account ID: {accid.Shorten()} {block.BlockType} ");
                    //Assert.IsTrue(result.Item1 == Lyra.Core.Blocks.APIResultCodes.Success, $"Auth Failed: {result.Item1}");

                    if (result.Item1 == APIResultCodes.Success)
                    {
                        await store.AddBlockAsync(block);
                        await cs.Worker_OnConsensusSuccessAsync(block, ConsensusResult.Yea, true);
                    }
                    else
                    {
                        _authResult = false;
                        _sbAuthResults.Append($"{result.Item1}, ");
                        Console.WriteLine($"Auth failed: {result.Item1}");
                        await cs.Worker_OnConsensusSuccessAsync(block, ConsensusResult.Nay, true);
                        _workflowEnds.Set();
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
            catch(Exception ex)
            {
                Console.WriteLine($"In AuthAsync: {ex}");
                await cs.Worker_OnConsensusSuccessAsync(block, ConsensusResult.Uncertain, true);

                return new AuthorizationAPIResult
                {
                    ResultCode = APIResultCodes.Exception,
                    TxHash = block.Hash,
                };
            }
            finally
            {
                _newAuth.Set();
            }
        }

        private async Task CreateTestBlockchainAsync()
        {
            networkId = "xtest";
            while (cs == null)
            {
                await Task.Delay(1000);
                cs = ConsensusService.Singleton;
                cs.SetHostEnv(_env);
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
            AccountHeightAPIResult ahr = null;
            mock.Setup(x => x.GetSyncHeightAsync())
                .Callback(async () => { ahr = await api.GetSyncHeightAsync(); })
                .ReturnsAsync(() => ahr);

            mock.Setup(x => x.GetLastServiceBlockAsync())
                .Returns(() => Task.FromResult(api.GetLastServiceBlockAsync()).Result);
            mock.Setup(x => x.GetLastConsolidationBlockAsync())
                .Returns(() => Task.FromResult(api.GetLastConsolidationBlockAsync()).Result);
            mock.Setup(x => x.GetBlockByIndexAsync(It.IsAny<string>(), It.IsAny<long>()))
                .Returns<string, long>((id, height) => Task.FromResult(api.GetBlockByIndexAsync(id, height)).Result);

            mock.Setup(x => x.GetBlockHashesByTimeRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Returns<DateTime, DateTime>((acct, sign) => Task.FromResult(api.GetBlockHashesByTimeRangeAsync(acct, sign)).Result);
            // brks
            mock.Setup(x => x.GetAllBrokerAccountsForOwnerAsync(It.IsAny<string>()))
                .Returns<string>(name => Task.FromResult(api.GetAllBrokerAccountsForOwnerAsync(name)).Result);

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
            mock.Setup(x => x.GetAllDaosAsync(It.IsAny<int>(), It.IsAny<int>()))
                .Returns<int, int>((page, pageSize) => Task.FromResult(api.GetAllDaosAsync(page, pageSize)).Result);
            mock.Setup(x => x.GetOtcOrdersByOwnerAsync(It.IsAny<string>()))
                .Returns<string>(accountId => Task.FromResult(api.GetOtcOrdersByOwnerAsync(accountId)).Result);
            mock.Setup(x => x.FindTradableOtcAsync())
                .Returns(() => Task.FromResult(api.FindTradableOtcAsync()).Result);
            mock.Setup(x => x.FindOtcTradeAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns<string, bool, int, int>((accountId, isOpen, page, pagesize) => 
                    Task.FromResult(api.FindOtcTradeAsync(accountId, isOpen, page, pagesize)).Result);
            mock.Setup(x => x.FindAllVotesByDaoAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns<string, bool>((daoid, openOnly) =>
                    Task.FromResult(api.FindAllVotesByDaoAsync(daoid, openOnly)).Result);

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
            Assert.IsTrue(sendResult2.Successful(), $"send error {sendResult2.ResultCode}");
            var sendResult3 = await genesisWallet.SendAsync(tamount, test3PublicKey);
            Assert.IsTrue(sendResult3.Successful(), $"send error {sendResult3.ResultCode}");
            var sendResult4 = await genesisWallet.SendAsync(tamount, test4PublicKey);
            Assert.IsTrue(sendResult4.Successful(), $"send error {sendResult4.ResultCode}");
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
            testWallet.NoConsole = true;
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
            test2Wallet.NoConsole = true;
            Assert.AreEqual(test2PublicKey, test2Wallet.AccountId);

            await test2Wallet.SyncAsync(client);
            //Assert.AreEqual(test2Wallet.BaseBalance, tamount);

            var walletStor4 = new AccountInMemoryStorage();
            Wallet.Create(walletStor4, "xunit2", "1234", networkId, test3PrivateKey);
            test3Wallet = Wallet.Open(walletStor4, "xunit2", "1234", client);
            test3Wallet.NoConsole = true;
            Assert.AreEqual(test3PublicKey, test3Wallet.AccountId);

            await test3Wallet.SyncAsync(client);

            var walletStor5 = new AccountInMemoryStorage();
            Wallet.Create(walletStor5, "xunit2", "1234", networkId, test4PrivateKey);
            test4Wallet = Wallet.Open(walletStor5, "xunit2", "1234", client);
            test4Wallet.NoConsole = true;
            Assert.AreEqual(test4PublicKey, test4Wallet.AccountId);

            await test4Wallet.SyncAsync(client);

            await TestOTCTrade();
            var tradeid = await TestOTCTradeDispute();   // test for dispute
            await TestVoting(tradeid);

            await TestPoolAsync();
            await TestProfitingAndStaking();
            await TestNodeFee();
            ////await TestDepositWithdraw();

            // let workflow to finish
            await Task.Delay(1000);            
        }

        private async Task WaitBlock(string target)
        {
            Console.WriteLine($"Waiting for block: {target}");
            var ret = _newAuth.WaitOne(1000);
            Assert.IsTrue(ret, "block not authorized properly.");
        }

        private async Task WaitWorkflow(string target)
        {
            Console.WriteLine($"\nWaiting for workflow ({DateTime.Now:mm:ss.ff}):: {target}");
#if DEBUG
            var ret = _workflowEnds.WaitOne(200000);
#else
            var ret = _workflowEnds.WaitOne(10000);
#endif
            Console.WriteLine($"Waited for workflow ({DateTime.Now:mm:ss.ff}):: {target}, Got it? {ret}");
            Assert.IsTrue(ret, "workflow not finished properly.");
            _workflowEnds.Reset();
        }

        private async Task TestVoting(string disputeTradeId)
        {
            // simulate a court by node owners
            // create a DAO for nodes
            var name = "Node Owners Club";
            var desc = "Doing great business!";
            var dcret = await genesisWallet.CreateDAOAsync(name, desc, 1, 10, 120, 120);
            Assert.IsTrue(dcret.Successful(), $"failed to create DAO: {dcret.ResultCode}");

            await WaitWorkflow("CreateDAOAsync");

            var nodesdaoret = await genesisWallet.RPC.GetDaoByNameAsync(name);
            Assert.IsTrue(nodesdaoret.Successful(), $"can't get dao: {nodesdaoret.ResultCode}");
            var nodesdao = nodesdaoret.GetBlock() as TransactionBlock;

            // join DAO / invest
            var invret = await testWallet.JoinDAOAsync(nodesdao.AccountID, 800000m);
            Assert.IsTrue(invret.Successful());

            await WaitWorkflow("JoinDAOAsync 1");

            nodesdaoret = await genesisWallet.RPC.GetDaoByNameAsync(name);
            Assert.IsTrue(nodesdaoret.Successful());
            nodesdao = nodesdaoret.GetBlock() as TransactionBlock;
            var treasure = (nodesdao as IDao).Treasure.ToRitoDecimalDict();
            Assert.AreEqual(1m, treasure[testPublicKey]);

            // another join DAO
            var invret2 = await test2Wallet.JoinDAOAsync(nodesdao.AccountID, 150000m);
            Assert.IsTrue(invret2.Successful());

            await WaitWorkflow("JoinDAOAsync 2");

            var invret3 = await test3Wallet.JoinDAOAsync(nodesdao.AccountID, 50000m);
            Assert.IsTrue(invret3.Successful());

            await WaitWorkflow("JoinDAOAsync 3");

            // then we expect the treasure rito
            nodesdaoret = await genesisWallet.RPC.GetDaoByNameAsync(name);
            Assert.IsTrue(nodesdaoret.Successful());
            nodesdao = nodesdaoret.GetBlock() as TransactionBlock;
            treasure = (nodesdao as IDao).Treasure.ToRitoDecimalDict();
            Assert.AreEqual(0.8m, treasure[testPublicKey]);
            Assert.AreEqual(0.15m, treasure[test2PublicKey]);
            Assert.AreEqual(0.05m, treasure[test3PublicKey]);

            // get the dispute trade
            var trdlatest = await test2Wallet.RPC.GetLastBlockAsync(disputeTradeId);
            Assert.IsTrue(trdlatest.Successful(), $"Can't get trade latest block: {trdlatest.ResultCode}");
            Assert.AreEqual(OTCTradeStatus.Dispute, (trdlatest.GetBlock() as IOtcTrade).OTStatus,
                $"Trade statust is not dispute");
            var trade = trdlatest.GetBlock() as IOtcTrade;

            // dispute trade
            // seller: testwallet
            // buyer: test2wallet
            // dispute created by: testwallet

            VotingSubject subject = new VotingSubject
            {
                Type = SubjectType.OTCDispute,
                DaoId = nodesdao.AccountID,
                Issuer = genesisWallet.AccountId,
                TimeSpan = 100,
                Title = "Now let vote on case ID 111",
                Description = "bla bla bla",
                Options = new [] { "Yay", "Nay"},
            };

            var resolution = new ODRResolution
            {
                RType = ResolutionType.OTCTrade,
                creator = genesisWallet.AccountId,
                tradeid = disputeTradeId,
                actions = new []
                {
                    new TransMove
                    {
                        to = trade.OwnerAccountId,
                        amount = 100,
                        desc = "compensate"
                    }
                }
            };

            var proposal = new VoteProposal
            {
                pptype = ProposalType.DisputeResolution,
                data = JsonConvert.SerializeObject(resolution),
            };

            var voteCrtRet = await genesisWallet.CreateVoteSubject(subject, proposal);

            await WaitWorkflow("Create Vote Subject Async");
            Assert.IsTrue(voteCrtRet.Successful(), "Create vote subject error");

            // then we will find the vote
            var votefindret = await genesisWallet.RPC.FindAllVotesByDaoAsync(nodesdao.AccountID, true);
            Assert.IsTrue(votefindret.Successful(), $"Can't find vote: {votefindret.ResultCode}");
            var votes = votefindret.GetBlocks();
            Assert.AreEqual(1, votes.Count());
            var curvote = votes.First() as IVoting;
            Assert.AreEqual(subject.Title, curvote.Subject.Title);

            var voteblksRet = await genesisWallet.RPC.GetBlocksByRelatedTxAsync(voteCrtRet.TxHash);
            var voteblk = voteblksRet.GetBlocks().Last() as TransactionBlock;
            var voteRet = await testWallet.Vote(voteblk.AccountID, 0);
            await WaitWorkflow("Vote on Subject Async");
            Assert.IsTrue(voteRet.Successful(), $"Vote error: {voteRet.ResultCode}");

            var voteRet2 = await test2Wallet.Vote(voteblk.AccountID, 0);
            await WaitWorkflow("Vote on Subject Async 2");
            Assert.IsTrue(voteRet2.Successful(), $"Vote error: {voteRet2.ResultCode}");

            var voteRet2x = await test2Wallet.Vote(voteblk.AccountID, 0);
            await WaitWorkflow("Vote on Subject Async 2x");
            Assert.IsTrue(!voteRet2x.Successful(), $"Vote 2x should error: {voteRet2x.ResultCode}");

            var voteRet3 = await test3Wallet.Vote(voteblk.AccountID, 1);
            await WaitWorkflow("Vote on Subject Async 3");
            Assert.IsTrue(voteRet3.Successful(), $"Vote error: {voteRet3.ResultCode}");

            var voteRet4 = await test4Wallet.Vote(voteblk.AccountID, 1);
            await WaitWorkflow("Vote on Subject Async 4");
            Assert.IsTrue(!voteRet4.Successful(), $"Vote 4 should error: {voteRet4.ResultCode}");

            // owner create resolution on vote result
            // vote keep as is.
            var summary = await test4Wallet.GetVoteSummary(voteblk.AccountID);
            Assert.IsNotNull(summary, "can't get vote summary.");
            Assert.IsTrue(summary.IsDecided, "should be decided.");
            Assert.AreEqual(0, summary.DecidedIndex, $"voting decided wrong option: {summary.DecidedIndex}");

            // trade should be dispute state
            var res1 = summary.Spec.Proposal.Deserialize() as ODRResolution;
            var latestTradeRet = await genesisWallet.RPC.GetLastBlockAsync(res1.tradeid);
            var latestTrade = latestTradeRet.GetBlock() as IOtcTrade;
            Assert.AreEqual(OTCTradeStatus.Dispute, latestTrade.OTStatus);

            await test2Wallet.SyncAsync(null);
            var beforeresolv = test2Wallet.BaseBalance;

            // then we execute the resolution depend on the voting result
            var odrRet = await genesisWallet.ExecuteResolution(res1);
            Assert.IsTrue(odrRet.Successful(), $"can't execute resolution: {odrRet.ResultCode}");

            await WaitWorkflow("ExecuteResolution");

            // now the state should be DisputeClosed 
            latestTradeRet = await genesisWallet.RPC.GetLastBlockAsync(res1.tradeid);
            latestTrade = latestTradeRet.GetBlock() as IOtcTrade;
            Assert.AreEqual(OTCTradeStatus.DisputeClosed, latestTrade.OTStatus);

            // testwallet should receive the compensate
            await test2Wallet.SyncAsync(null);
            var afterresolv = test2Wallet.BaseBalance;
            Assert.AreEqual(beforeresolv + 100m, afterresolv, $"compensate not received.");

            // test leave DAO
            var leaveret3 = await test3Wallet.LeaveDAOAsync(nodesdao.AccountID);
            Assert.IsTrue(leaveret3.Successful(), $"Can't leave DAO: {leaveret3.ResultCode}");
            await WaitWorkflow("LeaveDAOAsync 3");

            // then test3 should not exists in the treasure
            var nodesdaoret2 = await genesisWallet.RPC.GetDaoByNameAsync(name);
            Assert.IsTrue(nodesdaoret2.Successful());
            var nodesdao2 = nodesdaoret2.GetBlock() as TransactionBlock;
            var treasure2 = (nodesdao2 as IDao).Treasure.ToRitoDecimalDict();
            Assert.IsFalse(treasure2.ContainsKey(test3PublicKey), $"test 3 still exists.");

            ResetAuthFail();
        }

        private async Task TestOTCTrade()
        {
            var crypto = "unittest/ETH";
            // init. create token to sell
            var tokenGenesisResult = await testWallet.CreateTokenAsync("ETH", "unittest", "", 8, 100000, false, testWallet.AccountId,
                    "", "", ContractTypes.Cryptocurrency, null);
            Assert.IsTrue(tokenGenesisResult.Successful(), $"test otc token genesis failed: {tokenGenesisResult.ResultCode}");

            await WaitBlock("CreateTokenAsync");

            await testWallet.SyncAsync(null);
            var testbalance = testWallet.BaseBalance;

            // first create a DAO
            var name = "First DAO";
            var desc = "Doing great business!";
            var dcret = await testWallet.CreateDAOAsync(name, desc, 1, 10, 120, 120);
            Assert.IsTrue(dcret.Successful(), $"failed to create DAO: {dcret.ResultCode}");

            await WaitWorkflow("CreateDAOAsync");

            var daoret = await testWallet.RPC.GetDaoByNameAsync(name);
            Assert.IsTrue(daoret.Successful(), $"Can't get DAO: {daoret.ResultCode}");
            var daoblk = daoret.GetBlock() as DaoGenesisBlock;
            Assert.AreEqual(name, daoblk.Name);
            Assert.AreEqual(desc, daoblk.Description);

            //var dcretx = await testWallet.CreateDAOAsync(name, desc);
            //Assert.IsTrue(!dcretx.Successful(), $"should failed to create DAO: {dcretx.ResultCode}");

            //await WaitBlock("CreateDAOAsync Wrong");
            //ResetAuthFail();

            // test getalldao api
            var alldaoret = await testWallet.RPC.GetAllDaosAsync(0, 10);
            Assert.IsTrue(alldaoret.Successful(), $"can get all dao: {alldaoret.ResultCode}");
            var daos = alldaoret.GetBlocks();
            Assert.AreEqual(1, daos.Count(), $"can't find dao by GetAllDaosAsync");
            var dao0 = alldaoret.GetBlocks().First() as DaoGenesisBlock;
            Assert.IsTrue(daoblk.AuthCompare(dao0));

            // get dao by the IBroker api
            var brkblksret = await testWallet.RPC.GetAllBrokerAccountsForOwnerAsync(testWallet.AccountId);
            Assert.IsTrue(brkblksret.Successful(), $"Can't get DAO by brk api: {brkblksret.ResultCode}");
            var daoblk2 = brkblksret.GetBlocks().FirstOrDefault(a => a is DaoGenesisBlock) as DaoGenesisBlock;
            Assert.AreEqual(name, daoblk2.Name);
            Assert.AreEqual(desc, daoblk2.Description);

            var dao1 = daoret.GetBlock() as DaoRecvBlock;

            var order = new OTCOrder
            {
                daoId = dao1.AccountID,
                dir = TradeDirection.Sell,
                crypto = crypto,
                fiat = "USD",
                priceType = PriceType.Fixed,
                price = 2000,
                amount = 1,
                collateral = 25000000,
                payBy = new string[] { "Paypal" },
                limitMin = 100,
                limitMax = 2000,
            };

            var ret = await testWallet.CreateOTCOrderAsync(order);
            Assert.IsTrue(ret.Successful(), $"Can't create order: {ret.ResultCode}");

            await WaitWorkflow("CreateOTCOrderAsync");

            var otcret = await testWallet.RPC.GetOtcOrdersByOwnerAsync(testWallet.AccountId);
            Assert.IsTrue(otcret.Successful(), $"Can't get otc gensis block. {otcret.ResultCode}");
            var otcs = otcret.GetBlocks();
            Assert.IsTrue(otcs.Count() == 1 && otcs.First() is OTCOrderGenesisBlock, $"otc order gensis block not found.");

            // then DAO treasure should not have the crypto
            var daoret3 = await testWallet.RPC.GetDaoByNameAsync(name);
            Assert.IsTrue(daoret3.Successful(), $"Can't get DAO: {daoret3.ResultCode}");
            var daot = daoret3.GetBlock() as TransactionBlock;
            Assert.IsTrue(daot.Balances.ContainsKey(crypto), "No collateral token in DAO treasure.");
            Assert.AreEqual(0, daot.Balances[crypto].ToBalanceDecimal());

            var otcg = otcs.First() as OTCOrderGenesisBlock;
            Assert.IsTrue(order.Equals(otcg.Order), "OTC order not equal.");

            // here comes a buyer, he who want to buy 1 BTC.
            var tradableret = await testWallet.RPC.FindTradableOtcAsync();
            Assert.IsTrue(tradableret.Successful(), $"Can't find tradableorders: {tradableret.ResultCode}: {tradableret.ResultMessage}");
            var ords = tradableret.GetBlocks("orders");
            Assert.AreEqual(1, ords.Count(), "Order count not right");
            Assert.IsTrue((ords.First() as IOtcOrder).Order.Equals(order), "OTC order not equal.");

            var trade = new OTCTrade
            {
                daoId = dao1.AccountID,
                orderId = otcg.AccountID,
                orderOwnerId = otcg.OwnerAccountId,
                dir = TradeDirection.Buy,
                crypto = "unittest/ETH",
                fiat = "USD",
                price = 2000,
                amount = 0.1m,
                collateral = 15000000,
                pay = 200,
                payVia = "Paypal",
            };
            await test2Wallet.SyncAsync(null);
            var test2balance = test2Wallet.BaseBalance;
            var traderet = await test2Wallet.CreateOTCTradeAsync(trade);
            Assert.IsTrue(traderet.Successful(), $"OTC Trade error: {traderet.ResultCode}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(traderet.TxHash), "No TxHash for trade create.");

            await WaitWorkflow("CreateOTCTradeAsync");
            // the otc order should now be amount 9
            var otcret2 = await testWallet.RPC.GetOtcOrdersByOwnerAsync(testWallet.AccountId);
            Assert.IsTrue(otcret2.Successful(), $"Can't get otc block. {otcret2.ResultCode}");
            var otcs2 = otcret2.GetBlocks();
            Assert.IsTrue(otcs2.Count() == 1 && otcs2.First() is IOtcOrder, $"otc block count not = 1.");
            var otcorderx = otcs2.First() as IOtcOrder;
            Assert.AreEqual(0.9m, otcorderx.Order.amount, "order not processed");

            // get trade
            var related = await test2Wallet.RPC.GetBlocksByRelatedTxAsync(traderet.TxHash);
            Assert.IsTrue(related.Successful(), $"Can't get rleated tx for trade genesis: {related.ResultCode}");
            var blks = related.GetBlocks();
            var tradgen = blks.FirstOrDefault(a => a is OtcTradeGenesisBlock) as OtcTradeGenesisBlock;
            Assert.IsNotNull(tradgen, $"Can't get trade genesis: blks count: {blks.Count()}");
            Assert.AreEqual(trade, tradgen.Trade);
            Assert.AreEqual(OTCTradeStatus.Open, tradgen.OTStatus);

            // verify by api
            var tradeQueryRet = await test2Wallet.RPC.FindOtcTradeAsync(test2Wallet.AccountId, false, 0, 10);
            Assert.IsTrue(tradeQueryRet.Successful(), $"Can't query trade via FindOtcTradeAsync: {tradeQueryRet.ResultCode}");
            var tradeQueryResultBlocks = tradeQueryRet.GetBlocks();
            Assert.AreEqual(1, tradeQueryResultBlocks.Count());
            Assert.AreEqual(tradgen.AccountID, (tradeQueryResultBlocks.First() as TransactionBlock).AccountID);

            var tradeQueryRet2 = await testWallet.RPC.FindOtcTradeAsync(testWallet.AccountId, false, 0, 10);
            Assert.IsTrue(tradeQueryRet2.Successful(), $"Can't query trade via FindOtcTradeAsync: {tradeQueryRet2.ResultCode}");
            var tradeQueryResultBlocks2 = tradeQueryRet2.GetBlocks();
            Assert.AreEqual(1, tradeQueryResultBlocks2.Count());
            Assert.AreEqual(tradgen.AccountID, (tradeQueryResultBlocks2.First() as TransactionBlock).AccountID);

            // buyer send payment indicator
            var payindret = await test2Wallet.OTCTradeBuyerPaymentSentAsync(tradgen.AccountID);
            Assert.IsTrue(payindret.Successful(), $"Pay sent indicator error: {payindret.ResultCode}");

            await WaitWorkflow("OTCTradeBuyerPaymentSentAsync");
            // status changed to BuyerPaid
            var trdlatest = await test2Wallet.RPC.GetLastBlockAsync(tradgen.AccountID);
            Assert.IsTrue(trdlatest.Successful(), $"Can't get trade latest block: {trdlatest.ResultCode}");
            Assert.AreEqual(OTCTradeStatus.FiatSent, (trdlatest.GetBlock() as IOtcTrade).OTStatus,
                $"Trade statust not changed to BuyerPaid");

            // seller got the payment
            var gotpayret = await testWallet.OTCTradeSellerGotPaymentAsync(tradgen.AccountID);
            Assert.IsTrue(payindret.Successful(), $"Got Payment indicator error: {payindret.ResultCode}");

            await WaitWorkflow("OTCTradeSellerGotPaymentAsync");
            // status changed to BuyerPaid
            var trdlatest2 = await test2Wallet.RPC.GetLastBlockAsync(tradgen.AccountID);
            Assert.IsTrue(trdlatest2.Successful(), $"Can't get trade latest block: {trdlatest2.ResultCode}");
            Assert.AreEqual(OTCTradeStatus.CryptoReleased, (trdlatest2.GetBlock() as IOtcTrade).OTStatus,
                $"Trade status not changed to ProductReleased");

            await test2Wallet.SyncAsync(null);
            Assert.AreEqual(test2balance - 13, test2Wallet.BaseBalance, $"Test2 got collateral wrong. should be {test2balance} but {test2Wallet.BaseBalance}");

            // trade is ok. now its time to close the order
            var closeret = await testWallet.CloseOTCOrderAsync(dao1.AccountID, otcg.AccountID);
            Assert.IsTrue(closeret.Successful(), $"Unable to close order: {closeret.ResultCode}");

            await WaitWorkflow("CloseOTCOrderAsync");
            var ordfnlret = await testWallet.RPC.GetLastBlockAsync(otcg.AccountID);
            Assert.IsTrue(ordfnlret.Successful(), $"Can't get order latest block: {ordfnlret.ResultCode}");
            Assert.AreEqual(OTCOrderStatus.Closed, (ordfnlret.GetBlock() as IOtcOrder).OOStatus,
                $"Order status not changed to Closed");

            await testWallet.SyncAsync(null);
            var lyrshouldbe = testbalance - 10016;
            Assert.AreEqual(lyrshouldbe, testWallet.BaseBalance, $"Test got collateral wrong. should be {lyrshouldbe} but {testWallet.BaseBalance}");
            var bal2 = testWallet.GetLastSyncBlock().Balances[crypto].ToBalanceDecimal();
            Assert.AreEqual(100000m - 0.1m, bal2,
                $"testwallet balance of crypto should be {100000m - 0.1m} but {bal2}");

            await Task.Delay(100);
            Assert.IsTrue(_authResult, $"Authorizer failed: {_sbAuthResults}");
            ResetAuthFail();
        }

        private async Task<string> TestOTCTradeDispute()
        {
            var crypto = "unittest/ETH";
            // init. create token to sell
            //var tokenGenesisResult = await testWallet.CreateTokenAsync("ETH", "unittest", "", 8, 100000, false, testWallet.AccountId,
            //        "", "", ContractTypes.Cryptocurrency, null);
            //Assert.IsTrue(tokenGenesisResult.Successful(), $"test otc token genesis failed: {tokenGenesisResult.ResultCode}");

            //await WaitBlock("CreateTokenAsync");

            await testWallet.SyncAsync(null);
            var testbalance = testWallet.BaseBalance;

            // first create a DAO
            var name = "Second DAO";
            var desc = "Doing bad business!";
            var dcret = await testWallet.CreateDAOAsync(name, desc, 1, 10, 120, 120);
            Assert.IsTrue(dcret.Successful(), $"failed to create DAO: {dcret.ResultCode}");

            await WaitWorkflow("CreateDAOAsync");

            var daoret = await testWallet.RPC.GetDaoByNameAsync(name);
            Assert.IsTrue(daoret.Successful(), $"Can't get DAO: {daoret.ResultCode}");
            var daoblk = daoret.GetBlock() as DaoGenesisBlock;
            Assert.AreEqual(name, daoblk.Name);
            Assert.AreEqual(desc, daoblk.Description);

            var dcretx = await testWallet.CreateDAOAsync(name, desc, 1, 10, 120, 120);
            Assert.IsTrue(!dcretx.Successful(), $"should failed to create DAO: {dcretx.ResultCode}");

            await WaitBlock("CreateDAOAsync Wrong");
            ResetAuthFail();

            // get dao by the IBroker api
            var brkblksret = await testWallet.RPC.GetAllBrokerAccountsForOwnerAsync(testWallet.AccountId);
            Assert.IsTrue(brkblksret.Successful(), $"Can't get DAO by brk api: {brkblksret.ResultCode}");
            var daoblk2 = brkblksret.GetBlocks().Skip(1).FirstOrDefault(a => a is DaoGenesisBlock) as DaoGenesisBlock;
            Assert.AreEqual(name, daoblk2.Name);
            Assert.AreEqual(desc, daoblk2.Description);

            var dao1 = daoret.GetBlock() as DaoRecvBlock;

            var order = new OTCOrder
            {
                daoId = dao1.AccountID,
                dir = TradeDirection.Sell,
                crypto = crypto,
                fiat = "USD",
                priceType = PriceType.Fixed,
                price = 2000,
                amount = 2,
                collateral = 40000000,
                payBy = new string[] { "Paypal" },
                limitMin = 200,
                limitMax = 4000,
            };

            var ret = await testWallet.CreateOTCOrderAsync(order);
            Assert.IsTrue(ret.Successful(), $"Can't create order: {ret.ResultCode}");

            await WaitWorkflow("CreateOTCOrderAsync");

            var otcret = await testWallet.RPC.GetOtcOrdersByOwnerAsync(testWallet.AccountId);
            Assert.IsTrue(otcret.Successful(), $"Can't get otc gensis block. {otcret.ResultCode}");
            var otcs = otcret.GetBlocks();
            Assert.IsTrue(otcs.Count() == 2 && otcs.Last() is OTCOrderGenesisBlock, $"otc order gensis block not found.");

            // then DAO treasure should not have the crypto
            var daoret3 = await testWallet.RPC.GetDaoByNameAsync(name);
            Assert.IsTrue(daoret3.Successful(), $"Can't get DAO: {daoret3.ResultCode}");
            var daot = daoret3.GetBlock() as TransactionBlock;
            Assert.IsTrue(daot.Balances.ContainsKey(crypto), "No collateral token in DAO treasure.");
            Assert.AreEqual(0, daot.Balances[crypto].ToBalanceDecimal());

            var otcg = otcs.Last() as OTCOrderGenesisBlock;
            Assert.IsTrue(order.Equals(otcg.Order), "OTC order not equal.");

            // here comes a buyer, he who want to buy 1 BTC.
            var tradableret = await testWallet.RPC.FindTradableOtcAsync();
            Assert.IsTrue(tradableret.Successful(), $"Can't find tradableorders: {tradableret.ResultCode}: {tradableret.ResultMessage}");
            var ords = tradableret.GetBlocks("orders");
            Assert.AreEqual(1, ords.Count(), "Order count not right");
            Assert.IsTrue((ords.First() as IOtcOrder).Order.Equals(order), "OTC order not equal.");

            var trade = new OTCTrade
            {
                daoId = dao1.AccountID,
                orderId = otcg.AccountID,
                orderOwnerId = otcg.OwnerAccountId,
                dir = TradeDirection.Buy,
                crypto = "unittest/ETH",
                fiat = "USD",
                price = 2000,
                amount = 1,
                collateral = 40000000,
                pay = 2000,
                payVia = "Paypal",
            };
            await test2Wallet.SyncAsync(null);
            var test2balance = test2Wallet.BaseBalance;
            var traderet = await test2Wallet.CreateOTCTradeAsync(trade);
            Assert.IsTrue(traderet.Successful(), $"OTC Trade error: {traderet.ResultCode}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(traderet.TxHash), "No TxHash for trade create.");

            await WaitWorkflow("CreateOTCTradeAsync");
            // the otc order should now be amount 9
            var otcret2 = await testWallet.RPC.GetOtcOrdersByOwnerAsync(testWallet.AccountId);
            Assert.IsTrue(otcret2.Successful(), $"Can't get otc block. {otcret2.ResultCode}");
            var otcs2 = otcret2.GetBlocks();
            Assert.IsTrue(otcs2.Count() == 2 && otcs2.Last() is IOtcOrder, $"otc block count not = 1.");
            var otcorderx = otcs2.Last() as IOtcOrder;
            Assert.AreEqual(1m, otcorderx.Order.amount, "order not processed");

            // get trade
            var related = await test2Wallet.RPC.GetBlocksByRelatedTxAsync(traderet.TxHash);
            Assert.IsTrue(related.Successful(), $"Can't get rleated tx for trade genesis: {related.ResultCode}");
            var blks = related.GetBlocks();
            var tradgen = blks.FirstOrDefault(a => a is OtcTradeGenesisBlock) as OtcTradeGenesisBlock;
            Assert.IsNotNull(tradgen, $"Can't get trade genesis: blks count: {blks.Count()}");
            Assert.AreEqual(trade, tradgen.Trade);
            Assert.AreEqual(OTCTradeStatus.Open, tradgen.OTStatus);

            // verify by api
            var tradeQueryRet = await test2Wallet.RPC.FindOtcTradeAsync(test2Wallet.AccountId, false, 0, 10);
            Assert.IsTrue(tradeQueryRet.Successful(), $"Can't query trade via FindOtcTradeAsync: {tradeQueryRet.ResultCode}");
            var tradeQueryResultBlocks = tradeQueryRet.GetBlocks().OrderBy(a => a.TimeStamp);
            Assert.AreEqual(2, tradeQueryResultBlocks.Count());
            Assert.AreEqual(tradgen.AccountID, (tradeQueryResultBlocks.Last() as TransactionBlock).AccountID);

            var tradeQueryRet2 = await testWallet.RPC.FindOtcTradeAsync(testWallet.AccountId, false, 0, 10);
            Assert.IsTrue(tradeQueryRet2.Successful(), $"Can't query trade via FindOtcTradeAsync: {tradeQueryRet2.ResultCode}");
            var tradeQueryResultBlocks2 = tradeQueryRet2.GetBlocks().OrderBy(a => a.TimeStamp);
            Assert.AreEqual(2, tradeQueryResultBlocks2.Count());
            Assert.AreEqual(tradgen.AccountID, (tradeQueryResultBlocks2.Last() as TransactionBlock).AccountID);

            // buyer send payment indicator
            var payindret = await test2Wallet.OTCTradeBuyerPaymentSentAsync(tradgen.AccountID);
            Assert.IsTrue(payindret.Successful(), $"Pay sent indicator error: {payindret.ResultCode}");

            await WaitWorkflow("OTCTradeBuyerPaymentSentAsync");
            // status changed to BuyerPaid
            var trdlatest = await test2Wallet.RPC.GetLastBlockAsync(tradgen.AccountID);
            Assert.IsTrue(trdlatest.Successful(), $"Can't get trade latest block: {trdlatest.ResultCode}");
            Assert.AreEqual(OTCTradeStatus.FiatSent, (trdlatest.GetBlock() as IOtcTrade).OTStatus,
                $"Trade status not changed to BuyerPaid");

            // seller not got the payment. seller raise a dispute
            var crdptret = await testWallet.OTCTradeRaiseDisputeAsync(tradgen.AccountID);
            Assert.IsTrue(crdptret.Successful(), $"Raise dispute failed: {crdptret.ResultCode}");

            await WaitWorkflow("OTCTradeRaiseDisputeAsync");

            // then get the trade, the status should be dispute
            trdlatest = await testWallet.RPC.GetLastBlockAsync(tradgen.AccountID);
            Assert.IsTrue(trdlatest.Successful(), $"Can't get trade latest block: {trdlatest.ResultCode}");
            Assert.AreEqual(OTCTradeStatus.Dispute, (trdlatest.GetBlock() as IOtcTrade).OTStatus,
                $"Trade status not changed to Dispute");


            //// seller got the payment
            //var gotpayret = await testWallet.OTCTradeSellerGotPaymentAsync(tradgen.AccountID);
            //Assert.IsTrue(payindret.Successful(), $"Got Payment indicator error: {payindret.ResultCode}");

            //await WaitWorkflow("OTCTradeSellerGotPaymentAsync");
            //// status changed to BuyerPaid
            //var trdlatest2 = await test2Wallet.RPC.GetLastBlockAsync(tradgen.AccountID);
            //Assert.IsTrue(trdlatest2.Successful(), $"Can't get trade latest block: {trdlatest2.ResultCode}");
            //Assert.AreEqual(OTCTradeStatus.CryptoReleased, (trdlatest2.GetBlock() as IOtcTrade).OTStatus,
            //    $"Trade status not changed to ProductReleased");

            //await test2Wallet.SyncAsync(null);
            //Assert.AreEqual(test2balance - 13, test2Wallet.BaseBalance, $"Test2 got collateral wrong. should be {test2balance} but {test2Wallet.BaseBalance}");

            //// trade is ok. now its time to close the order
            //var closeret = await testWallet.CloseOTCOrderAsync(dao1.AccountID, otcg.AccountID);
            //Assert.IsTrue(closeret.Successful(), $"Unable to close order: {closeret.ResultCode}");

            //await WaitWorkflow("CloseOTCOrderAsync");
            //var ordfnlret = await testWallet.RPC.GetLastBlockAsync(otcg.AccountID);
            //Assert.IsTrue(ordfnlret.Successful(), $"Can't get order latest block: {ordfnlret.ResultCode}");
            //Assert.AreEqual(OTCOrderStatus.Closed, (ordfnlret.GetBlock() as IOtcOrder).OOStatus,
            //    $"Order status not changed to Closed");

            //await testWallet.SyncAsync(null);
            //var lyrshouldbe = testbalance - 10016;
            //Assert.AreEqual(lyrshouldbe, testWallet.BaseBalance, $"Test got collateral wrong. should be {lyrshouldbe} but {testWallet.BaseBalance}");
            //var bal2 = testWallet.GetLastSyncBlock().Balances[crypto].ToBalanceDecimal();
            //Assert.AreEqual(100000m - 1.1m, bal2,
            //    $"testwallet balance of crypto should be {100000m - 1.1m} but {bal2}");

            //await Task.Delay(100);
            //Assert.IsTrue(_authResult, $"Authorizer failed: {_sbAuthResults}");
            //ResetAuthFail();

            return tradgen.AccountID;
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
            Assert.AreEqual(500m, testWallet.GetLastSyncBlock().Balances["tether/TRX"].ToBalanceDecimal(), "Ext token amount error");

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
            await WaitWorkflow($"CreateStakingAccountAsync");

            var addstkret = await w.AddStakingAsync(stkblock.AccountID, amount);
            Assert.IsTrue(addstkret.Successful());
            await WaitWorkflow($"AddStakingAsync {addstkret.TxHash}");
            var stk = await w.GetStakingAsync(stkblock.AccountID);
            Assert.AreEqual(amount, (stk as TransactionBlock).Balances["LYR"].ToBalanceDecimal());
            return stk;
        }

        private async Task UnStaking(Wallet w, string stkid)
        {
            var balance = w.BaseBalance;
            var unstkret = await w.UnStakingAsync(stkid);
            Assert.IsTrue(unstkret.Successful());
            await WaitWorkflow($"UnStakingAsync {unstkret.TxHash}");
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
            Assert.IsTrue(crpftret.Successful(), $"Can't create profiting: {crpftret.ResultCode}");
            var pftblock = crpftret.GetBlock() as ProfitingBlock;
            Assert.IsTrue(pftblock.OwnerAccountId == testWallet.AccountId);

            Console.WriteLine("Staking 1");
            // create two staking account, add funds, and vote to it
            var stk = await CreateStaking(testWallet, pftblock.AccountID, 2000m);

            Console.WriteLine("Staking 2"); 
            var stk2 = await CreateStaking(test2Wallet, pftblock.AccountID, 2000m);

            // get the base balance
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
            await WaitWorkflow("CreateDividendsAsync");
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
            await WaitWorkflow("CreateLiquidatePoolAsync");
            var pool = await testWallet.GetLiquidatePoolAsync(token0, "LYR");
            Assert.IsTrue(pool.PoolAccountId != null && pool.PoolAccountId.StartsWith('L'), "Can't get pool created.");

            // add liquidate to pool
            var addpoolret = await testWallet.AddLiquidateToPoolAsync(token0, 1000000, "LYR", 5000);
            Assert.IsTrue(addpoolret.Successful());

            await WaitWorkflow("AddLiquidateToPoolAsync");

            // swap
            var poolx = await client.GetPoolAsync(token0, LyraGlobal.OFFICIALTICKERCODE);
            Assert.IsNotNull(poolx.PoolAccountId);
            var poolLatestBlock = poolx.GetBlock() as TransactionBlock;

            await testWallet.SyncAsync(null);

            var oldtkn0 = testWallet.GetLastSyncBlock().Balances[token0].ToBalanceDecimal();
            var cal2 = new SwapCalculator(LyraGlobal.OFFICIALTICKERCODE, token0, poolLatestBlock, LyraGlobal.OFFICIALTICKERCODE, 20, 0);
            var swapret = await testWallet.SwapTokenAsync("LYR", token0, "LYR", 20, cal2.SwapOutAmount);
            Assert.IsTrue(swapret.Successful());
            await WaitWorkflow("SwapTokenAsync");
            await testWallet.SyncAsync(null);

            var gotamount = testWallet.GetLastSyncBlock().Balances[token0].ToBalanceDecimal() - oldtkn0;
            Console.WriteLine($"Got swapped amount {gotamount} {token0}");

            // remove liquidate from pool
            var rmliqret = await testWallet.RemoveLiquidateFromPoolAsync(token0, "LYR");
            Assert.IsTrue(rmliqret.Successful());

            await testWallet.SyncAsync(null);
        }
    }
}
