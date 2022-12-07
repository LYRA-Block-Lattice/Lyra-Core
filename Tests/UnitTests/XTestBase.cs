using Akka.TestKit.Xunit2;
using FluentAssertions;
using Lyra;
using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Authorizers;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Core.Utils;
using Lyra.Core.WorkFlow;
using Lyra.Data.API;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.API.WorkFlow.UniMarket;
using Lyra.Data.Crypto;
using Lyra.Data.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Neo;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WorkflowCore.Interface;
using WorkflowCore.Services;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace UnitTests
{
    public class XTestBase : TestKit
    {
        protected readonly string testPrivateKey = "2LqBaZopCiPjBQ9tbqkqqyo4TSaXHUth3mdMJkhaBbMTf6Mr8u";
        protected readonly string testPublicKey = "LUTPLGNAP4vTzXh5tWVCmxUBh8zjGTR8PKsfA8E67QohNsd1U6nXPk4Q9jpFKsKfULaaT3hs6YK7WKm57QL5oarx8mZdbM";

        protected readonly string test2PrivateKey = "2XAGksPqMDxeSJVoE562TX7JzmCKna3i7AS9e4ZPmiTKQYATsy";
        protected string test2PublicKey = "LUTob2rWpFBZ6r3UxHhDYR8Utj4UDrmf1SFC25RpQxEfZNaA2WHCFtLVmURe1ty4ZNU9gBkCCrSt6ffiXKrRH3z9T3ZdXK";

        protected readonly string test3PrivateKey = "2iWkVkodnhcvQvzQSnBKMU3PhMfhEfWVMRWC1S21qg4cNR9UxC";
        protected string test3PublicKey = "LUTnKnTaeZ95MaCCeA4Y7RZeLo5PrmAipuvaaHMvrpk3awbc7VBSWNRRuhQuA5qy5SGNh7imC71jaMCdttMN1a6DrSPTP6";

        protected readonly string test4PrivateKey = "yEEj2uvCQji75Qps4jZdPRZj7KtFoeW2dh7pmfXjEuYXK9Uz3";
        protected string test4PublicKey = "LUT5jYomQHCJQhG3Co7GadEtohpwwYtyYz1vABHGeDkLDpSJGXFfpYgD9XckRXQg2Hv2Yrb2Ade3jbecZpLf4hbVho6b5n";

        protected string fiat = "fiat/USD";

        IHostEnv _env;
        protected ConsensusService cs;
        protected IAccountCollectionAsync store;
        private DagSystem sys;

        protected string networkId;
        protected Wallet genesisWallet;
        protected Wallet testWallet;
        protected Wallet test2Wallet;
        protected Wallet test3Wallet;
        protected Wallet test4Wallet;

        protected Random _rand = new Random();

        protected ILyraAPI client;

        protected bool _authResult = true;
        protected StringBuilder _sbAuthResults = new StringBuilder();

        protected AuthResult _lastAuthResult;
        AutoResetEvent _newAuth = new AutoResetEvent(false);

        protected string _currentTestTask;
        string _workflowKey;
        AutoResetEvent _workflowEnds = new AutoResetEvent(false);
        List<string> _endedWorkflows = new List<string>();

        LyraEventClient _eventClient;
        protected DealerClient dealer;

        public void TestSetup()
        {
            var serilogLogger = new LoggerConfiguration()
                //.MinimumLevel.Verbose()
                .WriteTo.Console()
                .WriteTo.File("c:\\tmp\\unittestlog.txt")
                .CreateLogger();

            SimpleLogger.Factory = new LoggerFactory();
            SimpleLogger.Factory.AddSerilog(serilogLogger);

            var probe = CreateTestProbe();
            var ta = new TestAuthorizer(probe);
            sys = ta.TheDagSystem;
            sys.StartConsensus();
            store = ta.TheDagSystem.Storage;

            //IServiceProvider serviceProvider = ConfigureServices();

            ////start the workflow host
            //var host = serviceProvider.GetService<IWorkflowHost>();

            ////var alltypes = typeof(DebiWorkflow)
            ////    .Assembly.GetTypes()
            ////    .Where(t => t.IsSubclassOf(typeof(DebiWorkflow)) && !t.IsAbstract);

            //foreach (var type in BrokerFactory.DynWorkFlows.Values.Select(a => a.GetType()))
            //{
            //    var methodInfo = typeof(WorkflowHost).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            //        .Where(a => a.Name == "RegisterWorkflow")
            //        .Last();

            //    var genericMethodInfo = methodInfo.MakeGenericMethod(type, typeof(LyraContext));

            //    genericMethodInfo.Invoke(host, new object[] { });
            //}

            //host.OnStepError += cs.Host_OnStepError;
            //host.OnLifeCycleEvent += cs.Host_OnLifeCycleEvent;
            //host.Start();

            //_env = serviceProvider.GetService<IHostEnv>();
            //_env.SetWorkflowHost(host);

            //host.StartWorkflow("HelloWorld", 1, null, null);
        }

        //object lifeo = new object();
        //private void Host_OnLifeCycleEvent(WorkflowCore.Models.LifeCycleEvents.LifeCycleEvent evt)
        //{
        //    lock (lifeo)
        //    {
        //        //Console.WriteLine($"Life: {evt.WorkflowInstanceId}: {evt.Reference}");
        //        if (evt.Reference == "end")
        //        {
        //            if (!_endedWorkflows.Contains(evt.WorkflowInstanceId))
        //            {
        //                _endedWorkflows.Add(evt.WorkflowInstanceId);
        //                var hash = evt.WorkflowDefinitionId;//cs.GetHashForWorkflow(evt.WorkflowInstanceId);
        //                //Console.WriteLine($"Unlock {hash}");
        //                //_lockedIdDict.Remove(hash);
        //                //Console.WriteLine($"Key is {hash} terminated. Set it. {_lockedIdDict.Count} locked.");
        //                //Console.WriteLine($"WF ended. {_lockedIdDict.Count} locked.");
        //                _workflowEnds.Set();
        //            }
        //        }
        //    }             
        //}

        //private void Host_OnStepError(WorkflowCore.Models.WorkflowInstance workflow, WorkflowCore.Models.WorkflowStep step, Exception exception)
        //{
        //    Console.WriteLine($"Workflow Host Error: {workflow.Id} {step.Name} {exception}");
        //    _workflowEnds.Set();
        //}

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
            services.AddTransient<SubmitBlock>();
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
        protected void ResetAuthFail()
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

        //protected Dictionary<string, List<string>> _lockedIdDict = new Dictionary<string, List<string>>();
        private async Task<AuthResult> LockAuth(DagSystem sys, Block block)
        {
            AuthorizingMsg msg = new AuthorizingMsg
            {
                From = cs.GetDagSystem().PosWallet.AccountId,
                Block = block,
                BlockHash = block.Hash!,
                MsgType = ChatMessageType.AuthorizerPrePrepare
            };

            var statex = await cs.CreateAuthringStateAsync(msg, true);
            if (statex.result != APIResultCodes.Success)
                return new AuthResult { Result = statex.result };

            AuthResult LocalAuthResult = null;
            var auth = cs.AF.Create(block);
            var tmpResult = await auth.AuthorizeAsync(sys, block);
            LocalAuthResult = tmpResult;
            return LocalAuthResult;
        }

        protected async Task<AuthorizationAPIResult> AuthAsync(Block block)
        {
            try
            {
                _newAuth.Reset();
                if (block is TransactionBlock)
                {
                    var accid = block is TransactionBlock tb ? tb.AccountID : "";

                    _lastAuthResult = await LockAuth(sys, block);

                    //if(_lastAuthResult.Result != APIResultCodes.Success)
                        Console.WriteLine($"Auth ({DateTime.Now:mm:ss.ff}): Height: {block.Height} Result: {_lastAuthResult.Result} Hash: {block.Hash.Shorten()} Account ID: {accid.Shorten()} {block.BlockType} ");
                    //Assert.IsTrue(result.Item1 == Lyra.Core.Blocks.APIResultCodes.Success, $"Auth Failed: {result.Item1}");

                    if (_lastAuthResult.Result == APIResultCodes.Success)
                    {
                        await store.AddBlockAsync(block);
                        await cs.Worker_OnConsensusSuccessAsync(block, ConsensusResult.Yea, true);
                    }
                    else
                    {
                        _authResult = false;
                        _sbAuthResults.Append($"{_lastAuthResult.Result}, ");
                        Console.WriteLine($"Auth failed: {_lastAuthResult.Result}");
                        await cs.Worker_OnConsensusSuccessAsync(block, ConsensusResult.Nay, true);
                        //_workflowEnds.Set();
                    }

                    return new AuthorizationAPIResult
                    {
                        ResultCode = _lastAuthResult.Result,
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

        protected async Task CreateTestBlockchainAsync()
        {
            networkId = "xtest";
            while (cs == null)
            {
                await Task.Delay(1000);
                cs = ConsensusService.Singleton;
                cs.SetHostEnv(_env);
            }

            cs.OnBlockFinished += (b, ok) =>
            {
                Console.WriteLine($"OnBlockFinished fired {b} result {ok}");
                _newAuth.Set();
            };

            cs.OnWorkflowFinished += (wf, ok) =>
            {
                Console.WriteLine($"OnWorkflowFinished fired {wf} result {ok}");
                if (wf == _workflowKey)
                    _workflowEnds.Set();
                else
                    Console.WriteLine($"wf key different. expecting {_workflowKey} but {wf}");
            };

            // workflow init
            IServiceProvider serviceProvider = ConfigureServices();
            var host = serviceProvider.GetService<IWorkflowHost>();

            foreach (var type in BrokerFactory.DynWorkFlows.Values.Select(a => a.GetType()))
            {
                var methodInfo = typeof(WorkflowHost).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(a => a.Name == "RegisterWorkflow")
                    .Last();

                var genericMethodInfo = methodInfo.MakeGenericMethod(type, typeof(LyraContext));

                genericMethodInfo.Invoke(host, new object[] { });
            }

            _env = serviceProvider.GetService<IHostEnv>();
            cs.SetHostEnv(_env);
            _env.SetWorkflowHost(host);
            cs.StartWorkflowEngine();

            cs.TestSharedInit();

            await Task.Delay(100);

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
            var gg = await cs.GuildGenesisAsync();
            await AuthAsync(gg);
            var consGen = cs.CreateConsolidationGenesisBlock(svcGen, tokenGen, pf, gg);
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
            mock.Setup(x => x.GetBlockByHashAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string, string>((acct, hash, sign) => Task.FromResult(api.GetBlockByHashAsync(acct, hash, sign)).Result);
            mock.Setup(x => x.GetBlockByHashAsync(It.IsAny<string>()))
                .Returns<string>((hash) => Task.FromResult(api.GetBlockByHashAsync(hash)).Result);
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
            mock.Setup(x => x.FindOtcTradeByStatusAsync(It.IsAny<string>(), It.IsAny<OTCTradeStatus>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns<string, OTCTradeStatus, int, int>((daoid, status, page, pagesize) =>
                    Task.FromResult(api.FindOtcTradeByStatusAsync(daoid, status, page, pagesize)).Result);

            #region Universal Trade
            mock.Setup(x => x.GetUniOrdersByOwnerAsync(It.IsAny<string>()))
                .Returns<string>(accountId => Task.FromResult(api.GetUniOrdersByOwnerAsync(accountId)).Result);
            mock.Setup(x => x.FindTradableUniAsync())
                .Returns(() => Task.FromResult(api.FindTradableUniAsync()).Result);
            mock.Setup(x => x.FindUniTradeAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns<string, bool, int, int>((accountId, isOpen, page, pagesize) =>
                    Task.FromResult(api.FindUniTradeAsync(accountId, isOpen, page, pagesize)).Result);
            mock.Setup(x => x.FindUniTradeByStatusAsync(It.IsAny<string>(), It.IsAny<UniTradeStatus>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns<string, UniTradeStatus, int, int>((daoid, status, page, pagesize) =>
                    Task.FromResult(api.FindUniTradeByStatusAsync(daoid, status, page, pagesize)).Result);
            #endregion

            mock.Setup(x => x.FindAllVotesByDaoAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns<string, bool>((daoid, openOnly) =>
                    Task.FromResult(api.FindAllVotesByDaoAsync(daoid, openOnly)).Result);
            mock.Setup(x => x.FindAllVoteForTradeAsync(It.IsAny<string>()))
                .Returns<string>((tradeid) =>
                    Task.FromResult(api.FindAllVoteForTradeAsync(tradeid)).Result);
            
            mock.Setup(x => x.GetVoteSummaryAsync(It.IsAny<string>()))
                .Returns<string>((voteid) =>
                    Task.FromResult(api.GetVoteSummaryAsync(voteid)).Result);
            mock.Setup(x => x.FindExecForVoteAsync(It.IsAny<string>()))
                .Returns<string>((voteid) =>
                    Task.FromResult(api.FindExecForVoteAsync(voteid)).Result);

            mock.Setup(x => x.GetDealerByAccountIdAsync(It.IsAny<string>()))
                .Returns<string>(accountId => Task.FromResult(api.GetDealerByAccountIdAsync(accountId)).Result);

            mock.Setup(x => x.ReceiveTransferAsync(It.IsAny<ReceiveTransferBlock>()))
                .Returns<ReceiveTransferBlock>((a) => Task.FromResult(AuthAsync(a).GetAwaiter().GetResult()));
            mock.Setup(x => x.ReceiveTransferAndOpenAccountAsync(It.IsAny<OpenWithReceiveTransferBlock>()))
                .Returns<OpenWithReceiveTransferBlock>((a) => Task.FromResult(AuthAsync(a).GetAwaiter().GetResult()));
            mock.Setup(x => x.CreateTokenAsync(It.IsAny<TokenGenesisBlock>()))
                .Returns<TokenGenesisBlock>((a) => Task.FromResult(AuthAsync(a).GetAwaiter().GetResult()));

            // NFT
            mock.Setup(x => x.FindNFTGenesisSendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string, string>((accountId, ticker, serial) => Task.FromResult(api.FindNFTGenesisSendAsync(accountId, ticker, serial)).Result);

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
            networkId = TestConfig.networkId;
            client = networkId == "devnet" ?
                new LyraRestClient("win", "xunit", "1.0", "https://192.168.3.77:4504/api/Node/") :
                LyraRestClient.Create(networkId, "win", "xunit", "1.0");

            var walletStor = new AccountInMemoryStorage();
            Wallet.Create(walletStor, "gensisi", "1234", networkId, "sVfBfv913fdXQ5pKiGU3KxV8Ee2vmQL7iHWDT1t4NzTqvTzj2");

            genesisWallet = Wallet.Open(walletStor, "gensisi", "1234", client);
            var ret = await genesisWallet.SyncAsync(client);
            Assert.IsTrue(ret == APIResultCodes.Success, $"gensisis can't sync: {ret}");

            // make sure test and test2 has been registed to dealer
            var url = networkId == "devnet" ? "https://dealer.devnet.lyra.live:7070" : "https://dealertestnet.lyra.live/";
            dealer = new DealerClient(new Uri(new Uri(url), "/api/dealer/"));

            var lsb = await client.GetLastServiceBlockAsync();
            var rret = await dealer.RegisterAsync(testPublicKey, "unittest1", "Unit", "", "Test 1", "u1@", "111", "111", "",
                Signatures.GetSignature(testPrivateKey, (lsb.GetBlock().Hash), testPublicKey), "", ""
                );
            Assert.IsTrue(rret.Successful());
            var rret2 = await dealer.RegisterAsync(test2PublicKey, "unittest2", "Unit", "", "Test 2", "u1@", "222", "111", "",
                Signatures.GetSignature(test2PrivateKey, (lsb.GetBlock().Hash), test2PublicKey), "", ""
                );
            Assert.IsTrue(rret2.Successful());
        }

        protected async Task WaitBlock(string target)
        {
            Console.WriteLine($"Waiting for block: {target}");

            var ret = _newAuth.WaitOne(Debugger.IsAttached ? 300000 : 3000);

            Assert.IsTrue(ret, "block not authorized properly.");
        }

        protected async Task WaitWorkflow(string target)
        {
            await WaitWorkflow(null, target);
        }

        protected async Task<APIResultCodes> WaitWorkflow(string key, string target, APIResultCodes expected = APIResultCodes.Success)
        {
            _workflowKey = key;            

            Console.WriteLine($"\n{_currentTestTask} Waiting for workflow ({DateTime.Now:mm:ss.ff}):: key: {key}, target: {target}");

            _workflowEnds.Reset();
            var ret = _workflowEnds.WaitOne(Debugger.IsAttached ? 30000 : 20000);
            
            //Console.WriteLine($"Waited for workflow ({DateTime.Now:mm:ss.ff}):: {target}, Got it? {ret}");
            Assert.IsTrue(ret, $"{_currentTestTask} workflow {_workflowKey} not finished properly.");
            //if(checklock)
            //    Assert.IsTrue(_lockedIdDict.Count == 0, $"Pending locked ID: {_lockedIdDict.Count}");

            if(cs != null)
            {
                Console.WriteLine($"{_currentTestTask} Wait for workflow ({DateTime.Now:mm:ss.ff}):: key: {key}, target: {target}. Done. {cs.LockedCount} locked.");
                if (cs.LockedCount > 0)
                {
                    foreach (var l in cs.Lockedups)
                    {
                        Console.WriteLine($"Pending Locking: {l}");
                    }
                }
            }

            Console.WriteLine();

            var blksret = await client.GetBlocksByRelatedTxAsync(key);
            Assert.IsTrue(blksret.Successful(), $"Can't get related tx while wait for workflow: {blksret.ResultCode}");
            var blk1 = blksret.GetBlocks().FirstOrDefault();

            //Assert.IsTrue(_lastAuthResult.Result == APIResultCodes.Success, $"Last result is not success: {_lastAuthResult.Result}");
            Assert.IsNotNull(blk1, $"can't get wf recv block for key {key}");

            Assert.IsTrue(blk1.Tags.ContainsKey("auth"), "wf first block not contains auth tag.");
            var wfresult = Enum.Parse<APIResultCodes>(blk1.Tags["auth"]);
            Assert.IsTrue(wfresult == expected, $"{_currentTestTask} workflow result not expected: {wfresult}");
            return wfresult;
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

        protected async Task SetupWallets(string networkId)
        {
            if(networkId == "xtest")
            {
                TestSetup();
                await Task.Delay(100);      // make sure mongodb clean all.
                await CreateTestBlockchainAsync();
            }                
            else
                await CreateDevnet();

            // test 1 wallet
            var walletStor2 = new AccountInMemoryStorage();
            Wallet.Create(walletStor2, "xunit", "1234", networkId, testPrivateKey);
            testWallet = Wallet.Open(walletStor2, "xunit", "1234", client);
            testWallet.NoConsole = true;
            Assert.AreEqual(testWallet.AccountId, testPublicKey);

            await testWallet.SyncAsync(client);
            //Assert.AreEqual(testWallet.BaseBalance, tamount);
            var lastBalance = testWallet.BaseBalance;
            if(lastBalance == 0)
            {
                await genesisWallet.SendAsync(800, testWallet.AccountId);
                await genesisWallet.SendAsync(123, testWallet.AccountId);
            }

            if (networkId == "xunit")
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
        }

        protected async Task PrintBalancesForAsync(params string[] accountids)
        {
            foreach (var x in accountids)
            {
                var ret = await client.GetLastBlockAsync(x);
                if (ret.Successful())
                {
                    var block = ret.GetBlock() as TransactionBlock;
                    PrintBalance(block.BlockType.ToString(), block);
                }
                else
                {
                    Console.WriteLine($"Print Balance failed: {ret.ResultCode}");
                }
            }
        }

        protected void PrintBalances(params TransactionBlock[] blocks)
        {
            foreach(var x in blocks)
            {
                PrintBalance(x.BlockType.ToString(), x);
            }
        }

        protected void PrintBalance(string name, TransactionBlock trans)
        {
            Console.WriteLine($"Balance: {name}, {trans.BalanceToReadString()}");
        }

        protected async Task SetupEventsListener()
        {
            var port = TestConfig.networkId == "mainnet" ? 5504 : 4504;
            var url = $"https://{TestConfig.networkId}.lyra.live:{port}/events";
            _eventClient = new LyraEventClient(LyraEventHelper.CreateConnection(new Uri(url)));

            _eventClient.RegisterOnEvent(async evt => await ProcessEventAsync(evt));

            await _eventClient.StartAsync();
        }

        private async Task ProcessEventAsync(EventContainer evt)
        {
            try
            {
                var obj = evt.Get();
                if (obj is WorkflowEvent wf)
                {
                    Console.WriteLine($"Workflow {wf.Key} State: {wf.State}, Message: {wf.Message}");

                    if (wf.State == "Exited")
                    {
                        if(_workflowKey != null && _workflowKey == wf.Key)
                            _workflowEnds.Set();

                        //if(_workflowKey == null)
                        //    _workflowEnds.Set();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ProcessEventAsync: {ex}");
            }
        }
    }
}
