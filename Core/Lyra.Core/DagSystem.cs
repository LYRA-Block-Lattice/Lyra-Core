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
using Lyra.Core.Authorizers;
using Lyra.Data.API;
using Lyra.Core.API;
using Lyra.Core.Blocks;

namespace Lyra
{
    public delegate void BlockGeneratedEventHandler(Block block, Block prevBlock);
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

        public event BlockGeneratedEventHandler OnNewBlock;

        public bool FullStarted { get; private set; }
        public Wallet PosWallet { get; private set; }

        private ChannelsConfig start_message = null;
        private bool suspend = false;

        private ILogger _log;
        private IHostEnv _hostEnv;

        public IAccountCollectionAsync Storage { get; private set; }

        public TradeMatchEngine TradeEngine { get; private set; }

        public BlockChainState ConsensusState { get; private set; }
        public void UpdateConsensusState(BlockChainState state) => ConsensusState = state;

        public DagSystem(IHostEnv hostEnv, IAccountCollectionAsync store, Wallet posWallet, IActorRef localNode)
        {
            _hostEnv = hostEnv;
            _log = new SimpleLogger("DagSystem").Logger;
            FullStarted = false;

            Storage = new TracedStorage(store);
            PosWallet = posWallet;

            LocalNode = localNode;
            this.LocalNode.Tell(this);

            if(hostEnv != null)     // for unit test
            {
                TheBlockchain = ActorSystem.ActorOf(BlockChain.Props(this, Storage));
                TaskManager = ActorSystem.ActorOf(Neo.Network.P2P.TaskManager.Props(this));

                TradeEngine = new TradeMatchEngine(Storage);
            }
        }

        public async Task StartAsync()
        {
            StartNode(new ChannelsConfig
            {
                Tcp = new IPEndPoint(IPAddress.Any, Settings.Default.P2P.Port),
                WebSocket = new IPEndPoint(IPAddress.Any, Settings.Default.P2P.WsPort),
                MinDesiredConnections = Settings.Default.P2P.MinDesiredConnections,
                MaxConnections = Settings.Default.P2P.MaxConnections,
                MaxConnectionsPerAddress = Settings.Default.P2P.MaxConnectionsPerAddress
            });

            int waitCount = 60;
            while (Neo.Network.P2P.LocalNode.Singleton.ConnectedCount < 2)
            {
                _log.LogInformation($"{waitCount} Wait for p2p network startup. connected peer: {Neo.Network.P2P.LocalNode.Singleton.ConnectedCount}");
                await Task.Delay(1000);
                waitCount--;
                if (waitCount <= 0)
                    break;
            }
            _log.LogInformation($"p2p network connected peer: {Neo.Network.P2P.LocalNode.Singleton.ConnectedCount}");

            TheBlockchain.Tell(new BlockChain.Startup());

            StartConsensus();

            //if (DagSystem.Singleton.PosWallet.AccountId == ProtocolSettings.Default.StandbyValidators[0])
            //{
            //    ActorSystem.Scheduler
            //       .ScheduleTellRepeatedly(TimeSpan.FromSeconds(20),
            //                 TimeSpan.FromSeconds(600),
            //                 Consensus, new ConsensusService.Consolidate(), ActorRefs.NoSender); //or ActorRefs.Nobody or something else
            //}

            FullStarted = true;
        }

        public void StartConsensus()
        {
            Consensus = ActorSystem.ActorOf(ConsensusService.Props(this, this._hostEnv, this.LocalNode, TheBlockchain));
            Consensus.Tell(new ConsensusService.Startup { }, TheBlockchain);
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

        public void NewBlockGenerated(Block block)
        {
            try
            {
                Block prevBlock = null;
                if (block.PreviousHash != null)
                    prevBlock = Storage.FindBlockByHash(block.PreviousHash);
                OnNewBlock?.Invoke(block, prevBlock);
            }
            catch(Exception e)
            {
                _log.LogError($"In NewBlockGenerated: {e}");
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
