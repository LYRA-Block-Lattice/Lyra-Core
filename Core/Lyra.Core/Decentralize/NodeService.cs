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

namespace Lyra.Core.Decentralize
{
    public class NodeService : BackgroundService
    {
        public static NodeService Instance { get; private set; } 
        public static DealEngine Dealer { get; private set; }
        public Wallet PosWallet { get; private set; }
        private LyraNodeConfig _config;

        private INodeAPI _dataApi;
        public MongoClient client;
        private IMongoDatabase _db;

        AutoResetEvent _waitOrder;
        ILogger _log;

        public string Leader { get; private set; }
        private ConsensusRuntimeConfig _consensus;

        public NodeService(IOptions<LyraNodeConfig> config,
            ILogger<NodeService> logger,
            ConsensusRuntimeConfig consensus
            )
        {
            if (Instance == null)
                Instance = this;
            else
                throw new InvalidOperationException("Should not do this");

            _config = config.Value;
            _consensus = consensus;
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
                PosWallet = new Wallet(walletStore, _config.Lyra.NetworkId);
                string lyra_folder = BaseAccount.GetFullFolderName("Lyra-CLI-" + _config.Lyra.NetworkId);
                string full_path = BaseAccount.GetFullPath(lyra_folder);
                PosWallet.OpenAccount(full_path, _config.Lyra.Wallet.Name);

                var sys = new LyraSystem(_config);
                sys.Start();

                if (_db == null)
                {
                    //BsonSerializer.RegisterSerializer(typeof(decimal), new DecimalSerializer(BsonType.Decimal128));
                    //BsonSerializer.RegisterSerializer(typeof(decimal?), new NullableSerializer<decimal>(new DecimalSerializer(BsonType.Decimal128)));

                    client = new MongoClient(_config.Lyra.Database.DexDBConnect);
                    _db = client.GetDatabase("Dex");

                    var exchangeAccounts = _db.GetCollection<ExchangeAccount>("exchangeAccounts");
                    var queue = _db.GetCollection<ExchangeOrder>("queuedDexOrders");
                    var finished = _db.GetCollection<ExchangeOrder>("finishedDexOrders");

                    // TODO: make it DI
                    //Dealer = new DealEngine(_config, _dataApi, exchangeAccounts, queue, finished);
                    //Dealer.OnNewOrder += (s, a) => _waitOrder.Set();
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
                //if (_consensus.Seeds.Contains(_config.Orleans.EndPoint.AdvertisedIPAddress))
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
                    //        _leader = new LeaderElectionSupport(zk, electRoot, _config.Orleans.EndPoint.AdvertisedIPAddress);

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
                    
        //            if (Leader == _config.Orleans.EndPoint.AdvertisedIPAddress)
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
