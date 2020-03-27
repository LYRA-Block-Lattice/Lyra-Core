using Akka.Actor;
using Lyra;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Core.Utils;
using Microsoft.Extensions.Logging;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Network.P2P
{
    public class LocalNode : Peer
    {
        public class SignedMessageRelay { public SourceSignedMessage signedMessage;  }
        public class Relay { public IInventory Inventory; }
        internal class RelayDirectly { public IInventory Inventory; }
        internal class SendDirectly { public IInventory Inventory; }

        public const uint ProtocolVersion = 0;
        private const int MaxCountFromSeedList = 5;
        private readonly IPEndPoint[] SeedList = new IPEndPoint[ProtocolSettings.Default.SeedList.Length];

        private static readonly object lockObj = new object();
        private readonly LyraSystem system;
        internal readonly ConcurrentDictionary<IActorRef, RemoteNode> RemoteNodes = new ConcurrentDictionary<IActorRef, RemoteNode>();

        public int ConnectedCount => RemoteNodes.Count;
        public int UnconnectedCount => UnconnectedPeers.Count;
        public static readonly uint Nonce;
        public static string UserAgent { get; set; }

        private static LocalNode singleton;

        private ILogger _log;
        public static LocalNode Singleton
        {
            get
            {
                while (singleton == null) Thread.Sleep(10);
                return singleton;
            }
        }

        static LocalNode()
        {
            Random rand = new Random();
            Nonce = (uint)rand.Next();
            UserAgent = $"/{Assembly.GetExecutingAssembly().GetName().Name}:{Assembly.GetExecutingAssembly().GetVersion()}/";
        }

        public LocalNode(LyraSystem system)
        {
            _log = new SimpleLogger("LocalNode").Logger;

            lock (lockObj)
            {
                if (singleton != null)
                    throw new InvalidOperationException();
                this.system = system;
                singleton = this;

                // Start dns resolution in parallel

                for (int i = 0; i < ProtocolSettings.Default.SeedList.Length; i++)
                {
                    int index = i;
                    Task.Run(() => SeedList[index] = GetIpEndPoint(ProtocolSettings.Default.SeedList[index]));
                }
            }
        }

        /// <summary>
        /// Packs a MessageCommand to a full Message with an optional ISerializable payload.
        /// Forwards it to <see cref="BroadcastMessage(Message message)"/>.
        /// </summary>
        /// <param name="command">The message command to be packed.</param>
        /// <param name="payload">Optional payload to be Serialized along the message.</param>
        private void BroadcastMessage(MessageCommand command, ISerializable payload = null)
        {
            BroadcastMessage(Message.Create(command, payload));
        }

        /// <summary>
        /// Broadcast a message to all connected nodes, namely <see cref="Connections"/>.
        /// </summary>
        /// <param name="message">The message to be broadcasted.</param>
        private void BroadcastMessage(Message message) => SendToRemoteNodes(message);

        /// <summary>
        /// Send message to all the RemoteNodes connected to other nodes, faster than ActorSelection.
        /// </summary>
        private void SendToRemoteNodes(object message)
        {
            foreach (var connection in RemoteNodes.Keys)
            {
                connection.Tell(message);
            }
        }

        private static IPEndPoint GetIPEndpointFromHostPort(string hostNameOrAddress, int port)
        {
            if (IPAddress.TryParse(hostNameOrAddress, out IPAddress ipAddress))
                return new IPEndPoint(ipAddress, port);
            IPHostEntry entry;
            try
            {
                entry = Dns.GetHostEntry(hostNameOrAddress);
            }
            catch (SocketException)
            {
                return null;
            }
            ipAddress = entry.AddressList.FirstOrDefault(p => p.AddressFamily == AddressFamily.InterNetwork || p.IsIPv6Teredo);
            if (ipAddress == null) return null;
            return new IPEndPoint(ipAddress, port);
        }

        internal static IPEndPoint GetIpEndPoint(string hostAndPort)
        {
            if (string.IsNullOrEmpty(hostAndPort)) return null;

            try
            {
                string[] p = hostAndPort.Split(':');
                return GetIPEndpointFromHostPort(p[0], int.Parse(p[1]));
            }
            catch { }

            return null;
        }

        public IEnumerable<RemoteNode> GetRemoteNodes()
        {
            return RemoteNodes.Values;
        }

        public IEnumerable<IPEndPoint> GetUnconnectedPeers()
        {
            return UnconnectedPeers;
        }

        /// <summary>
        /// Override of abstract class that is triggered when <see cref="UnconnectedPeers"/> is empty.
        /// Performs a BroadcastMessage with the command `MessageCommand.GetAddr`, which, eventually, tells all known connections.
        /// If there are no connected peers it will try with the default, respecting MaxCountFromSeedList limit.
        /// </summary>
        /// <param name="count">The count of peers required</param>
        protected override void NeedMorePeers(int count)
        {
            count = Math.Max(count, MaxCountFromSeedList);
            if (ConnectedPeers.Count > 0)
            {
                BroadcastMessage(MessageCommand.GetAddr);
            }
            else
            {
                // Will call AddPeers with default SeedList set cached on <see cref="ProtocolSettings"/>.
                // It will try to add those, sequentially, to the list of currently unconnected ones.

                Random rand = new Random();
                AddPeers(SeedList.Where(u => u != null).OrderBy(p => rand.Next()).Take(count));
            }
        }

        protected override void OnReceive(object message)
        {
            //_log.LogInformation($"LocalNode OnReceive {message.GetType().Name}");
            base.OnReceive(message);
            switch (message)
            {
                case SourceSignedMessage signedMsg:
                    BroadcastMessage(MessageCommand.Consensus, signedMsg);
                    break;
                case SignedMessageRelay signedMessageRelay:
                    if(system.Consensus != null && signedMessageRelay != null)
                        system.Consensus.Tell(signedMessageRelay);
                    break;
                case Message msg:
                    BroadcastMessage(msg);
                    break;
                case Relay relay:
                    OnRelay(relay.Inventory);
                    break;
                case RelayDirectly relay:
                    OnRelayDirectly(relay.Inventory);
                    break;
                case SendDirectly send:
                    OnSendDirectly(send.Inventory);
                    break;
                //case RelayResultReason _:
                //    break;
            }
        }

        /// <summary>
        /// For Transaction type of IInventory, it will tell Transaction to the actor of Consensus.
        /// Otherwise, tell the inventory to the actor of Blockchain.
        /// There are, currently, three implementations of IInventory: TX, Block and ConsensusPayload.
        /// </summary>
        /// <param name="inventory">The inventory to be relayed.</param>
        private void OnRelay(IInventory inventory)
        {
            if (inventory is Transaction transaction)
                system.Consensus?.Tell(transaction);
            system.TheBlockchain.Tell(inventory);
        }

        private void OnRelayDirectly(IInventory inventory)
        {
            var message = new RemoteNode.Relay { Inventory = inventory };
            // When relaying a block, if the block's index is greater than 'LastBlockIndex' of the RemoteNode, relay the block;
            // otherwise, don't relay.
            if (inventory is Block block)
            {
                foreach (KeyValuePair<IActorRef, RemoteNode> kvp in RemoteNodes)
                {
                    if (block.Height > kvp.Value.LastBlockIndex)
                        kvp.Key.Tell(message);
                }
            }
            else
                SendToRemoteNodes(message);
        }

        private void OnSendDirectly(IInventory inventory) => SendToRemoteNodes(inventory);

        public static Props Props(LyraSystem system)
        {
            return Akka.Actor.Props.Create(() => new LocalNode(system));
        }

        protected override Props ProtocolProps(object connection, IPEndPoint remote, IPEndPoint local)
        {
            return RemoteNode.Props(system, connection, remote, local);
        }
    }
}
