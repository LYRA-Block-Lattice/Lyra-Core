using Lyra.Core.API;
using Lyra.Exchange;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Lyra.Core.Utils;
using Lyra.Core.Accounts;
using Lyra.Core.Exchange;
using Lyra.Core.LiteDB;
using Neo;
using System.Linq;

namespace Lyra.Core.Decentralize
{
    public class NodeService : BackgroundService
    {
        public static NodeService Instance { get; private set; } 
        public static DealEngine Dealer { get; private set; }
        public Wallet PosWallet { get; private set; }

        //private INodeAPI _dataApi;
        public MongoClient client;
        private IMongoDatabase _db;

        AutoResetEvent _waitOrder;
        ILogger _log;

        public string Leader { get; private set; }

        public NodeService(ILogger<NodeService> logger)
        {
            if (Instance == null)
                Instance = this;
            else
                throw new InvalidOperationException("Should not do this");

            _log = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _waitOrder = new AutoResetEvent(false);
            try
            {
                _log.LogInformation($"NodeService: ExecuteAsync Called.");

                // something must be initialized first
                new AuthorizersFactory().Init();

                var walletStore = new LiteAccountDatabase();
                var tmpWallet = new Wallet(walletStore, Neo.Settings.Default.LyraNode.Lyra.NetworkId);
                string lyrawalletfolder = BaseAccount.GetFullFolderName("wallets");
                tmpWallet.OpenAccount(lyrawalletfolder, Neo.Settings.Default.LyraNode.Lyra.Wallet.Name);

                if(ProtocolSettings.Default.StandbyValidators.Any(a => a == tmpWallet.AccountId))
                {
                    // not update balance for seed nodes.
                    PosWallet = tmpWallet;
                }
                else
                {
                    // create wallet and update balance
                    var memStor = new AccountInMemoryStorage();
                    var acctWallet = new ExchangeAccountWallet(memStor, Neo.Settings.Default.LyraNode.Lyra.NetworkId);
                    acctWallet.AccountName = "tmpAcct";
                    await acctWallet.RestoreAccountAsync("", tmpWallet.PrivateKey);
                    acctWallet.OpenAccount("", acctWallet.AccountName);

                    Console.WriteLine("Sync wallet for " + acctWallet.AccountId);
                    var rpcClient = await LyraRestClient.CreateAsync(Neo.Settings.Default.LyraNode.Lyra.NetworkId, Environment.OSVersion.Platform.ToString(), "Lyra Client Cli", "1.0a");
                    await acctWallet.Sync(rpcClient);

                    PosWallet = acctWallet;
                }              

                var sys = new DagSystem();
                sys.Start();

                if (_db == null)
                {
                    //BsonSerializer.RegisterSerializer(typeof(decimal), new DecimalSerializer(BsonType.Decimal128));
                    //BsonSerializer.RegisterSerializer(typeof(decimal?), new NullableSerializer<decimal>(new DecimalSerializer(BsonType.Decimal128)));

                    client = new MongoClient(Neo.Settings.Default.LyraNode.Lyra.Database.DexDBConnect);
                    _db = client.GetDatabase("Dex");

                    var exchangeAccounts = _db.GetCollection<ExchangeAccount>("exchangeAccounts");
                    var queue = _db.GetCollection<ExchangeOrder>("queuedDexOrders");
                    var finished = _db.GetCollection<ExchangeOrder>("finishedDexOrders");

                    // TODO: make it DI
                    Dealer = new DealEngine(exchangeAccounts, queue, finished);
                    Dealer.OnNewOrder += (s, a) => _waitOrder.Set();
                }

                //_watcher = new ZooKeeperWatcher(_log);
                //await UsingZookeeper(_zkClusterOptions.ConnectionString, async (zk) => {
                //    // get Lyra network configurations from /lyra
                //    // {"mode":"permissioned","seeds":["node1","node2"]}
                //    var cfg = await zk.getDataAsync("/lyra");
                //    var runtimeConfig = JsonConvert.DeserializeObject<ConsensusRuntimeConfig>(Encoding.ASCII.GetString(cfg.Data));
                //    // do copy because the object is global
                //    _consensus.Mode = runtimeConfig.Mode;
                //    _consensus.Seeds = runtimeConfig.Seeds;
                //    _consensus.CurrentSeed = runtimeConfig.CurrentSeed;
                //    _consensus.PrimaryAuthorizerNodes = runtimeConfig.PrimaryAuthorizerNodes;
                //    _consensus.BackupAuthorizerNodes = runtimeConfig.BackupAuthorizerNodes;
                //    _consensus.VotingNodes = runtimeConfig.VotingNodes;

                //    _log.LogInformation($"NodeService: Got runtimeconfig success.");
                //});

                // all seeds do node election
                //if (_consensus.Seeds.Contains(Neo.Settings.Default.LyraNode.Orleans.EndPoint.AdvertisedIPAddress))
                //{
                    //while(true)     // we do nothing without zk
                    //{
                    //    try
                    //    {
                    //        var electRoot = "/lyra/seedelect";
                    //        var zk = new ZooKeeper(_zkClusterOptions.ConnectionString, ZOOKEEPER_CONNECTION_TIMEOUT, _watcher);
                    //        var stat = await zk.existsAsync(electRoot);
                    //        if (stat == null)
                    //            await zk.createAsync(electRoot, new byte[0], ZooDefs.Ids.OPEN_ACL_UNSAFE, CreateMode.PERSISTENT);
                    //        _leader = new LeaderElectionSupport(zk, electRoot, Neo.Settings.Default.LyraNode.Orleans.EndPoint.AdvertisedIPAddress);

                    //        _leader.addListener(this);
                    //        await _leader.start();

                    //        break;
                    //    }
                    //    catch(Exception ex)
                    //    {
                    //        _log.Fail(Orleans.ErrorCode.MembershipShutDownFailure, ex.Message);
                    //        await Task.Delay(1000);
                    //    }
                    //}
                //}
            }
            catch (Exception ex)
            {
                throw new Exception("Error Initialize Node Service", ex);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                // do work
                if (_waitOrder.WaitOne(1000))
                {
                    _waitOrder.Reset();

                    await Dealer.MakeDealAsync();
                }
                else
                {
                    // no new order. do house keeping.
                }
            }
        }

        //public async Task onElectionEvent(ElectionEventType eventType)
        //{
        //    switch(eventType)
        //    {
        //        case ElectionEventType.ELECTED_COMPLETE:
        //            Leader = await _leader.getLeaderHostName();
                    
        //            if (Leader == Neo.Settings.Default.LyraNode.Orleans.EndPoint.AdvertisedIPAddress)
        //                _serviceAccount.IsNodeFullySynced = true;

        //            if (Leader == _consensus.CurrentSeed)
        //                return;

        //            _consensus.CurrentSeed = Leader;
        //            var seedMsg = new ChatMsg
        //            {
        //                From = _serviceAccount.AccountId,
        //                Type = ChatMessageType.SeedChanged,
        //                Text = Leader
        //            };
        //            await _gossiper.SendMessage(seedMsg);
        //            break;
        //        default:
        //            break;
        //    }            
        //}

        //private Task UsingZookeeper(string connectString, Func<ZooKeeper, Task> zkMethod)
        //{
        //    return ZooKeeper.Using(connectString, ZOOKEEPER_CONNECTION_TIMEOUT, _watcher, zkMethod);
        //}

        //// help class
        //internal class ZooKeeperWatcher : Watcher
        //{
        //    private readonly ILogger logger;
        //    public ZooKeeperWatcher(ILogger logger)
        //    {
        //        this.logger = logger;
        //    }

        //    public override Task process(WatchedEvent @event)
        //    {
        //        if (logger.IsEnabled(LogLevel.Debug))
        //        {
        //            logger.Debug(@event.ToString());
        //        }
        //        return Task.CompletedTask;
        //    }
        //}
    }
}
