using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;
using Lyra.Core.Cryptography;
using Lyra.Exchange;
using Lyra.Authorizer.Accounts;
using Lyra.Authorizer.Authorizers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using org.apache.zookeeper.recipes.leader;
using org.apache.zookeeper;
using Microsoft.Extensions.Options;
using Orleans.Configuration;
using Orleans.Runtime;
using Newtonsoft.Json;
using System.Text;
using Lyra.Core.Utils;

namespace Lyra.Authorizer.Decentralize
{
    public class NodeService : BackgroundService, LeaderElectionAware
    {
        public static NodeService Instance { get; private set; } 
        public static DealEngine Dealer { get; private set; }
        private const int ZOOKEEPER_CONNECTION_TIMEOUT = 2000;

        private LyraConfig _config;

        private INodeAPI _dataApi;
        public MongoClient client;
        private IMongoDatabase _db;

        AutoResetEvent _waitOrder;
        ILogger _log;

        ZooKeeperClusteringSiloOptions _zkClusterOptions;
        private ZooKeeper _zk;
        private ZooKeeperWatcher _watcher;
        private LeaderElectionSupport _leader;

        public string Leader { get; private set; }
        public bool ModeConsensus { get; private set; }

        public LyraNetworkConfigration LyraNetworkConfig { get; set; }

        public NodeService(IOptions<LyraConfig> config,
            IOptions<ZooKeeperClusteringSiloOptions> zkOptions,
            ILogger<NodeService> logger)
        {
            if (Instance == null)
                Instance = this;
            else
                throw new InvalidOperationException("Should not do this");

            _config = config.Value;
            _zkClusterOptions = zkOptions.Value;
            //_dataApi = dataApi;
            _log = logger;

            //BaseAuthorizer.OnAuthorized += (s, e) =>
            //{
            //    if (e.Result is SendTransferBlock)
            //    {
            //        var block = e.Result as SendTransferBlock;
            //        _log.LogWarning("Transfer {0} from {1} to {2}", block.Index, block.AccountID, block.DestinationAccountId);
            //    }
            //};
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _waitOrder = new AutoResetEvent(false);
            try
            {
                await Task.Delay(100000000);
                _watcher = new ZooKeeperWatcher(_log);
                _zk = new ZooKeeper(_zkClusterOptions.ConnectionString, ZOOKEEPER_CONNECTION_TIMEOUT, _watcher);

                // get Lyra network configurations from /lyra
                var cfg = await _zk.getDataAsync("/lyra");
                LyraNetworkConfig = JsonConvert.DeserializeObject<LyraNetworkConfigration>(Encoding.ASCII.GetString(cfg.Data));
                ModeConsensus = LyraNetworkConfig.seed == "permissionless";

                // do node election
                var electRoot = "/elect";
                var stat = await _zk.existsAsync(electRoot);
                if (stat == null)
                    await _zk.createAsync(electRoot, new byte[0], ZooDefs.Ids.OPEN_ACL_UNSAFE, CreateMode.PERSISTENT);
                _leader = new LeaderElectionSupport(_zk, electRoot, Environment.MachineName);

                _leader.addListener(this);
                await _leader.start();

                if (_db == null)
                {
                    //BsonSerializer.RegisterSerializer(typeof(decimal), new DecimalSerializer(BsonType.Decimal128));
                    //BsonSerializer.RegisterSerializer(typeof(decimal?), new NullableSerializer<decimal>(new DecimalSerializer(BsonType.Decimal128)));

                    client = new MongoClient(_config.DexDBConnect);
                    _db = client.GetDatabase("Dex");

                    var exchangeAccounts = _db.GetCollection<ExchangeAccount>("exchangeAccounts");
                    var queue = _db.GetCollection<ExchangeOrder>("queuedDexOrders");
                    var finished = _db.GetCollection<ExchangeOrder>("finishedDexOrders");

                    Dealer = new DealEngine(_config, _dataApi, exchangeAccounts, queue, finished);
                    Dealer.OnNewOrder += (s, a) => _waitOrder.Set();
                }

                // check if this node needs sync with master
                var myIp = OrleansSettings.AppSetting["EndPoint:AdvertisedIPAddress"];
                // init api service
                await (_dataApi as ApiService).InitializeNodeAsync(myIp, LyraNetworkConfig.seed == myIp);
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

        public async Task onElectionEvent(ElectionEventType eventType)
        {
            switch(eventType)
            {
                case ElectionEventType.ELECTED_COMPLETE:
                    Leader = await _leader.getLeaderHostName();
                    break;
                default:
                    break;
            }            
        }

        // help class
        internal class ZooKeeperWatcher : Watcher
        {
            private readonly ILogger logger;
            public ZooKeeperWatcher(ILogger logger)
            {
                this.logger = logger;
            }

            public override Task process(WatchedEvent @event)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.Debug(@event.ToString());
                }
                return Task.CompletedTask;
            }
        }
    }
}
