using Akka.Actor;
using Akka.Configuration;
using Lyra.Core.Accounts;
using Lyra.Core.Decentralize;
using Lyra.Core.Utils;
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
    public class LyraSystem
    {
        public ActorSystem ActorSystem { get; } = ActorSystem.Create(nameof(LyraSystem),
    $"akka {{ log-dead-letters = off }}" +
    $"blockchain-mailbox {{ mailbox-type: \"{typeof(BlockchainMailbox).AssemblyQualifiedName}\" }}" +
    //$"task-manager-mailbox {{ mailbox-type: \"{typeof(TaskManagerMailbox).AssemblyQualifiedName}\" }}" +
    $"remote-node-mailbox {{ mailbox-type: \"{typeof(RemoteNodeMailbox).AssemblyQualifiedName}\" }}" +
    $"protocol-handler-mailbox {{ mailbox-type: \"{typeof(ProtocolHandlerMailbox).AssemblyQualifiedName}\" }}" +
    $"consensus-service-mailbox {{ mailbox-type: \"{typeof(ConsensusServiceMailbox).AssemblyQualifiedName}\" }}");

        public IActorRef TheBlockchain { get; }
        public IActorRef LocalNode { get; }
        internal IActorRef TaskManager { get; }
        public IActorRef Consensus { get; private set; }

        private ChannelsConfig start_message = null;
        private bool suspend = false;
        public static LyraSystem Singleton { get; private set; }

        public string NetworkId { get; private set; }

        public LyraSystem()
        {
            LocalNode = ActorSystem.ActorOf(Neo.Network.P2P.LocalNode.Props(this));
            TheBlockchain = ActorSystem.ActorOf(BlockChain.Props(this));

            NetworkId = Neo.Settings.Default.LyraNode.Lyra.NetworkId;
            Singleton = this;
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
                while (BlockChain.Singleton == null)
                {
                    await Task.Delay(100);
                }
                StartConsensus();

                TheBlockchain.Tell(new BlockChain.Startup());

                if (NodeService.Instance.PosWallet.AccountId == ProtocolSettings.Default.StandbyValidators[0])
                {
                    ActorSystem.Scheduler
                       .ScheduleTellRepeatedly(TimeSpan.FromSeconds(20),
                                 TimeSpan.FromSeconds(600),
                                 Consensus, new ConsensusService.Consolidate(), ActorRefs.NoSender); //or ActorRefs.Nobody or something else
                }
            });
        }

        public void StartConsensus()
        {
            Consensus = ActorSystem.ActorOf(ConsensusService.Props(this.LocalNode));
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
