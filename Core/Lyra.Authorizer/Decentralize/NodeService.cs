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
using Lyra.Authorizer.Services;
using System.IO;

namespace Lyra.Authorizer.Decentralize
{
    public class NodeService : BackgroundService, LeaderElectionAware
    {
        public static NodeService Instance { get; private set; } 
        public static DealEngine Dealer { get; private set; }
        private const int ZOOKEEPER_CONNECTION_TIMEOUT = 2000;

        private LyraNodeConfig _config;

        private INodeAPI _dataApi;
        public MongoClient client;
        private IMongoDatabase _db;
        ServiceAccount _serviceAccount;

        AutoResetEvent _waitOrder;
        ILogger _log;
        GossipListener _gossiper;

        ZooKeeperClusteringSiloOptions _zkClusterOptions;
        private ZooKeeperWatcher _watcher;
        private LeaderElectionSupport _leader;

        public string Leader { get; private set; }
        private ConsensusRuntimeConfig _consensus;

        public NodeService(IOptions<LyraNodeConfig> config,
            IOptions<ZooKeeperClusteringSiloOptions> zkOptions,
            ServiceAccount serviceAccount,
            ILogger<NodeService> logger,
            GossipListener gossiper,
            ConsensusRuntimeConfig consensus
            )
        {
            if (Instance == null)
                Instance = this;
            else
                throw new InvalidOperationException("Should not do this");

            _config = config.Value;
            _zkClusterOptions = zkOptions.Value;
            //_dataApi = dataApi;
            _log = logger;
            _serviceAccount = serviceAccount;
            _gossiper = gossiper;
            _consensus = consensus;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _waitOrder = new AutoResetEvent(false);
            try
            {
                _log.LogInformation($"NodeService: ExecuteAsync Called.");

                await Task.Delay(15000);// wait for silo to startup
                await _serviceAccount.StartAsync(false, null);
                await Task.Delay(1000);
                await _gossiper.Init(_config.Orleans.EndPoint.AdvertisedIPAddress);

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

                _watcher = new ZooKeeperWatcher(_log);
                await UsingZookeeper(_zkClusterOptions.ConnectionString, async (zk) => {
                    // get Lyra network configurations from /lyra
                    // {"mode":"permissioned","seeds":["node1","node2"]}
                    var cfg = await zk.getDataAsync("/lyra");
                    var runtimeConfig = JsonConvert.DeserializeObject<ConsensusRuntimeConfig>(Encoding.ASCII.GetString(cfg.Data));
                    // do copy because the object is global
                    _consensus.Mode = runtimeConfig.Mode;
                    _consensus.Seeds = runtimeConfig.Seeds;
                    _consensus.CurrentSeed = runtimeConfig.CurrentSeed;
                    _consensus.PrimaryAuthorizerNodes = runtimeConfig.PrimaryAuthorizerNodes;
                    _consensus.BackupAuthorizerNodes = runtimeConfig.BackupAuthorizerNodes;
                    _consensus.VotingNodes = runtimeConfig.VotingNodes;

                    _log.LogInformation($"NodeService: Got runtimeconfig success.");
                });

                // all seeds do node election
                if (_consensus.Seeds.Contains(_config.Orleans.EndPoint.AdvertisedIPAddress))
                {
                    while(true)     // we do nothing without zk
                    {
                        try
                        {
                            var electRoot = "/lyra/seedelect";
                            var zk = new ZooKeeper(_zkClusterOptions.ConnectionString, ZOOKEEPER_CONNECTION_TIMEOUT, _watcher);
                            var stat = await zk.existsAsync(electRoot);
                            if (stat == null)
                                await zk.createAsync(electRoot, new byte[0], ZooDefs.Ids.OPEN_ACL_UNSAFE, CreateMode.PERSISTENT);
                            _leader = new LeaderElectionSupport(zk, electRoot, _config.Orleans.EndPoint.AdvertisedIPAddress);

                            _leader.addListener(this);
                            await _leader.start();

                            break;
                        }
                        catch(Exception ex)
                        {
                            _log.Fail(Orleans.ErrorCode.MembershipShutDownFailure, ex.Message);
                            await Task.Delay(1000);
                        }
                    }

                }
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
                    
                    if (Leader == _config.Orleans.EndPoint.AdvertisedIPAddress)
                        _serviceAccount.IsNodeFullySynced = true;

                    if (Leader == _consensus.CurrentSeed)
                        return;

                    _consensus.CurrentSeed = Leader;
                    var seedMsg = new ChatMsg
                    {
                        From = _serviceAccount.AccountId,
                        Type = ChatMessageType.SeedChanged,
                        Text = Leader
                    };
                    await _gossiper.SendMessage(seedMsg);
                    break;
                default:
                    break;
            }            
        }

        private Task UsingZookeeper(string connectString, Func<ZooKeeper, Task> zkMethod)
        {
            return ZooKeeper.Using(connectString, ZOOKEEPER_CONNECTION_TIMEOUT, _watcher, zkMethod);
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
