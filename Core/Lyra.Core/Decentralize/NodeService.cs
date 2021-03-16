using Lyra.Core.API;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Lyra.Core.Utils;
using Lyra.Core.Accounts;
using Neo;
using System.Linq;
using Lyra.Data.Utils;
using Lyra.Data.API;

namespace Lyra.Core.Decentralize
{
    public class NodeService : BackgroundService
    {
        //public static NodeService Instance { get; private set; } 
        //private INodeAPI _dataApi;
        public MongoClient client;
        private IMongoDatabase _db;

        AutoResetEvent _waitOrder;
        ILogger _log;
        IHostEnv _hostEnv;

        public string Leader { get; private set; }

        public static DagSystem Dag;

        public NodeService(ILogger<NodeService> logger, IHostEnv hostEnv)
        {
            //if (Instance == null)
            //    Instance = this;
            //else
            //    throw new InvalidOperationException("Should not do this");

            _log = logger;
            _hostEnv = hostEnv;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _waitOrder = new AutoResetEvent(false);
            try
            {
                var networkId = LyraNodeConfig.GetNetworkId();

                _log.LogInformation($"{LyraGlobal.PRODUCTNAME} {LyraGlobal.NODE_VERSION} Mode: {Neo.Settings.Default.LyraNode.Lyra.Mode}: Starting node daemon for {networkId}.");

                // something must be initialized first
                new AuthorizersFactory().Init();

                string lyrawalletfolder = Wallet.GetFullFolderName(networkId, "wallets");
                var walletStore = new SecuredWalletStore(lyrawalletfolder);
                var tmpWallet = Wallet.Open(walletStore, Neo.Settings.Default.LyraNode.Lyra.Wallet.Name, Neo.Settings.Default.LyraNode.Lyra.Wallet.Password);

                Wallet PosWallet;
                if(true)//ProtocolSettings.Default.StandbyValidators[0] == tmpWallet.AccountId)
                {
                    // not update balance for seed nodes.
                    PosWallet = tmpWallet;
                }
                else
                {
                    //// create wallet and update balance
                    //var memStor = new AccountInMemoryStorage();
                    //Wallet.Create(memStor, "tmpAcct", "", networkId, tmpWallet.PrivateKey);
                    //var acctWallet = Wallet.Open(memStor, "tmpAcct", "");
                    //acctWallet.VoteFor = tmpWallet.VoteFor;

                    //Console.WriteLine("Sync wallet for " + acctWallet.AccountId);
                    //var rpcClient = LyraRestClient.Create(networkId, Environment.OSVersion.Platform.ToString(), $"{LyraGlobal.PRODUCTNAME} Client Cli", "1.0a");
                    //await acctWallet.Sync(rpcClient);

                    //PosWallet = acctWallet;
                }

                var store = new MongoAccountCollection();
                var localNode = DagSystem.ActorSystem.ActorOf(Neo.Network.P2P.LocalNode.Props());
                Dag = new DagSystem(_hostEnv, store, PosWallet, localNode);
                _ = Task.Run(async () => await Dag.StartAsync());
                await Task.Delay(30000);

                if (_db == null)
                {
                    client = new MongoClient(Neo.Settings.Default.LyraNode.Lyra.Database.DexDBConnect);
                    _db = client.GetDatabase("Dex");
                }
            }
            catch (Exception ex)
            {
                _log.LogCritical($"NodeService: Error Initialize! {ex}");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                // do work
                if (_waitOrder.WaitOne(1000))
                {
                    _waitOrder.Reset();

                }
                else
                {
                    // no new order. do house keeping.
                }
            }
        }
    }
}
