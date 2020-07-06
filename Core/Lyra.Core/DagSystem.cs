using Akka.Actor;
using Akka.Configuration;
using Akka.Routing;
using Lyra.Core.Accounts;
using Lyra.Core.Decentralize;
using Lyra.Core.Utils;
using Microsoft.Extensions.Logging;
using Neo;
using Neo.IO.Actors;
using Neo.Network.P2P;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Settings = Neo.Settings;

namespace Lyra
{
    public class DagSystem
    {
        public static ActorSystem ActorSystem { get; } = ActorSystem.Create(nameof(DagSystem),
    $"akka {{ log-dead-letters = off }}" +
    $"blockchain-mailbox {{ mailbox-type: \"{typeof(BlockchainMailbox).AssemblyQualifiedName}\" }}" +
    $"task-manager-mailbox {{ mailbox-type: \"{typeof(TaskManagerMailbox).AssemblyQualifiedName}\" }}" +
    $"remote-node-mailbox {{ mailbox-type: \"{typeof(RemoteNodeMailbox).AssemblyQualifiedName}\" }}" +
    $"consensus-service-mailbox {{ mailbox-type: \"{typeof(ConsensusServiceMailbox).AssemblyQualifiedName}\" }}");

        internal IActorRef TheBlockchain { get; }
        public IActorRef LocalNode { get; }
        internal IActorRef TaskManager { get; }
        public IActorRef Consensus { get; private set; }

        public Wallet PosWallet { get; private set; }

        private ChannelsConfig start_message = null;
        private bool suspend = false;

        private ILogger _log;

        public IAccountCollectionAsync Storage { get; private set; }

        public DagSystem(string networkId, IAccountCollectionAsync store, Wallet posWallet, IActorRef localNode)
        {
            _log = new SimpleLogger("DagSystem").Logger;

            Storage = store;
            PosWallet = posWallet;

            LyraNodeConfig.Init(networkId);
            LocalNode = localNode;
            this.LocalNode.Tell(this);

            TheBlockchain = ActorSystem.ActorOf(BlockChain.Props(this, store));
            TaskManager = ActorSystem.ActorOf(Neo.Network.P2P.TaskManager.Props(this));
        }

        public void Start()
        {
            StartNode(new ChannelsConfig
            {
                Tcp = new IPEndPoint(IPAddress.Any, Settings.Default.P2P.Port),
                WebSocket = new IPEndPoint(IPAddress.Any, Settings.Default.P2P.WsPort),
                MinDesiredConnections = Settings.Default.P2P.MinDesiredConnections,
                MaxConnections = Settings.Default.P2P.MaxConnections,
                MaxConnectionsPerAddress = Settings.Default.P2P.MaxConnectionsPerAddress
            });

            Task.Run(async () =>
            {
                int waitCount = 60;
                while(Neo.Network.P2P.LocalNode.Singleton.ConnectedCount < 2)
                {
                    _log.LogWarning($"{waitCount} Wait for p2p network startup. connected peer: {Neo.Network.P2P.LocalNode.Singleton.ConnectedCount}");
                    await Task.Delay(1000);
                    waitCount--;
                    if (waitCount <= 0)
                        break;
                }
                _log.LogWarning($"p2p network connected peer: {Neo.Network.P2P.LocalNode.Singleton.ConnectedCount}");

                StartConsensus();

                TheBlockchain.Tell(new BlockChain.Startup());

                //if (DagSystem.Singleton.PosWallet.AccountId == ProtocolSettings.Default.StandbyValidators[0])
                //{
                //    ActorSystem.Scheduler
                //       .ScheduleTellRepeatedly(TimeSpan.FromSeconds(20),
                //                 TimeSpan.FromSeconds(600),
                //                 Consensus, new ConsensusService.Consolidate(), ActorRefs.NoSender); //or ActorRefs.Nobody or something else
                //}
            });
        }

        public void StartConsensus()
        {
            Consensus = ActorSystem.ActorOf(ConsensusService.Props(this, this.LocalNode, TheBlockchain));
            //Consensus.Tell(new ConsensusService.Start { IgnoreRecoveryLogs = ignoreRecoveryLogs }, Blockchain);
        }

        public void StartNode(ChannelsConfig config)
        {
            start_message = config;

            if (!suspend)
            {
                LocalNode.Tell(start_message);
                start_message = null;
            }
        }
    }

    public class Transaction
    {
        public UInt256 Hash;
        public List<object> Witnesses;
    }


    public class Snapshot
    {

    }
}
