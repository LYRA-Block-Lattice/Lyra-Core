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
                var networkId = Environment.GetEnvironmentVariable("WIZDAG_NETWORK");
                _log.LogInformation($"NodeService: ExecuteAsync Called.");

                // something must be initialized first
                new AuthorizersFactory().Init();

                var walletStore = new LiteAccountDatabase();
                var tmpWallet = new Wallet(walletStore, networkId);
                string lyrawalletfolder = BaseAccount.GetFullFolderName(networkId, "wallets");
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
                    var acctWallet = new ExchangeAccountWallet(memStor, networkId);
                    acctWallet.AccountName = "tmpAcct";
                    acctWallet.RestoreAccount("", tmpWallet.PrivateKey);
                    acctWallet.OpenAccount("", acctWallet.AccountName);
                    acctWallet.VoteFor = tmpWallet.VoteFor;

                    Console.WriteLine("Sync wallet for " + acctWallet.AccountId);
                    var rpcClient = await LyraRestClient.CreateAsync(networkId, Environment.OSVersion.Platform.ToString(), "WizDAG Client Cli", "1.0a");
                    await acctWallet.Sync(rpcClient);

                    PosWallet = acctWallet;
                }              

                var sys = new DagSystem(networkId);
                sys.Start();

                if (_db == null)
                {
                    client = new MongoClient(Neo.Settings.Default.LyraNode.Lyra.Database.DexDBConnect);
                    _db = client.GetDatabase("Dex");

                    var exchangeAccounts = _db.GetCollection<ExchangeAccount>("exchangeAccounts");
                    var queue = _db.GetCollection<ExchangeOrder>("queuedDexOrders");
                    var finished = _db.GetCollection<ExchangeOrder>("finishedDexOrders");

                    // TODO: make it DI
                    Dealer = new DealEngine(exchangeAccounts, queue, finished);
                    Dealer.OnNewOrder += (s, a) => _waitOrder.Set();
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
    }
}
