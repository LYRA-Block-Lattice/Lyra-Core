﻿using Lyra.Core.API;
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
using System.IO;
using Lyra.Data.Crypto;
using Newtonsoft.Json;
using System.Web.Http;
using Newtonsoft.Json.Converters;
using Microsoft.AspNetCore.SignalR;

namespace Lyra.Core.Decentralize
{
    public class NodeService : BackgroundService
    {
        //public static NodeService Instance { get; private set; } 
        //private INodeAPI _dataApi;
        AutoResetEvent _waitOrder;
        ILogger _log;
        IHostEnv _hostEnv;
        IAccountCollectionAsync _store;
        //IHubContext<LyraEventHub, ILyraEvent> _lyraEventContext { get; }

        public string Leader { get; private set; }

        public static DagSystem Dag;

        public NodeService(ILogger<NodeService> logger, IHostEnv hostEnv, IAccountCollectionAsync store)
        {
            //if (Instance == null)
            //    Instance = this;
            //else
            //    throw new InvalidOperationException("Should not do this");

            _log = logger;
            _hostEnv = hostEnv;
            _store = store;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _waitOrder = new AutoResetEvent(false);
            try
            {
                var networkId = LyraNodeConfig.GetNetworkId();

                _log.LogInformation($"{LyraGlobal.PRODUCTNAME} {LyraGlobal.NODE_VERSION} Mode: {Neo.Settings.Default.LyraNode.Lyra.Mode}: Starting node daemon for {networkId}.");

                // something must be initialized first

                Wallet PosWallet;

                string lyrawalletfolder = Wallet.GetFullFolderName(networkId, "wallets");

                if (!Directory.Exists(lyrawalletfolder))
                    Directory.CreateDirectory(lyrawalletfolder);

                var walletStore = new SecuredWalletStore(lyrawalletfolder);
                if (!walletStore.Exists(Neo.Settings.Default.LyraNode.Lyra.Wallet.Name))
                {
                    _log.LogInformation($"Creating wallet for {networkId}.");

                    (var privateKey, var publicKey) = Signatures.GenerateWallet();

                    _log.LogInformation($"The new wallet {Neo.Settings.Default.LyraNode.Lyra.Wallet.Name} for {networkId} was created.");
                    //Console.WriteLine($"Private Key: {privateKey}");
                    _log.LogInformation($"Account ID: {publicKey}");

                    walletStore.Create(Neo.Settings.Default.LyraNode.Lyra.Wallet.Name, Neo.Settings.Default.LyraNode.Lyra.Wallet.Password, networkId, privateKey, publicKey, "");
                    _log.LogInformation($"Wallet saved to: {lyrawalletfolder}{Neo.Settings.Default.LyraNode.Lyra.Wallet.Name}.lyrawallet");
                }

                PosWallet = Wallet.Open(walletStore, 
                    Neo.Settings.Default.LyraNode.Lyra.Wallet.Name, 
                    Neo.Settings.Default.LyraNode.Lyra.Wallet.Password,
                    LyraRestClient.Create(networkId, "", "NodeService", "1.0", LyraGlobal.SelectNode(networkId) + "Node/"));
                _log.LogInformation($"Staking wallet: {PosWallet.AccountId}");
                PosWallet.SetVoteFor(PosWallet.AccountId);

                //var blcokcount = await _store.GetBlockCountAsync();
                //if (blcokcount > 0 && networkId == "devnet") // not genesis
                //{
                //    try
                //    {
                //        await PosWallet.SyncAsync(null);
                //    }
                //    catch { }
                //}                

                var localNode = DagSystem.ActorSystem.ActorOf(Neo.Network.P2P.LocalNode.Props());
                Dag = new DagSystem(_hostEnv, _store, PosWallet, localNode);
                _ = Task.Run(async () => await Dag.StartAsync()).ConfigureAwait(false);
                await Task.Delay(30000);
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
