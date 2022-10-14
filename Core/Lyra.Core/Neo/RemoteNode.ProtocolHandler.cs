using Akka.Actor;
using Lyra;
using Lyra.Core.Decentralize;
using Neo.Cryptography;
using Neo.IO.Caching;
using Neo.Network.P2P.Capabilities;
using Neo.Network.P2P.Payloads;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;

namespace Neo.Network.P2P
{
    partial class RemoteNode
    {
        private class Timer { }
        private class PendingKnownHashesCollection : KeyedCollection<UInt256, (UInt256, DateTime)>
        {
            protected override UInt256 GetKeyForItem((UInt256, DateTime) item)
            {
                return item.Item1;
            }
        }

        private readonly PendingKnownHashesCollection pendingKnownHashes = new PendingKnownHashesCollection();
        //private readonly HashSetCache<UInt256> knownHashes = new HashSetCache<UInt256>(DagSystem.Singleton.Storage.MemPool.Capacity * 2 / 5);
        //private readonly HashSetCache<UInt256> sentHashes = new HashSetCache<UInt256>(DagSystem.Singleton.Storage.MemPool.Capacity * 2 / 5);
        private bool verack = false;
        //private BloomFilter bloom_filter;

        private static readonly TimeSpan TimerInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan PendingTimeout = TimeSpan.FromMinutes(1);

        private readonly ICancelable timer = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(TimerInterval, TimerInterval, Context.Self, new Timer(), ActorRefs.NoSender);

        private void OnMessage(Message msg)
        {
            //foreach (IP2PPlugin plugin in Plugin.P2PPlugins)
            //    if (!plugin.OnP2PMessage(msg))
            //        return;
            if (Version == null)
            {
                if (msg.Command != MessageCommand.Version)
                    throw new ProtocolViolationException();
                OnVersionMessageReceived((VersionPayload)msg.Payload);
                return;
            }
            if (!verack)
            {
                if (msg.Command != MessageCommand.Verack)
                    throw new ProtocolViolationException();
                OnVerackMessageReceived();
                return;
            }
            switch (msg.Command)
            {
                case MessageCommand.Addr:
                    OnAddrMessageReceived((AddrPayload)msg.Payload);
                    break;
                case MessageCommand.Consensus:
                    OnSignedMessageReceived((SourceSignedMessage)msg.Payload);
                    break;
                //case MessageCommand.Block:
                //    OnInventoryReceived((Block)msg.Payload);
                //    break;
                //case MessageCommand.Consensus:
                //    OnInventoryReceived((ConsensusPayload)msg.Payload);
                //    break;
                //case MessageCommand.FilterAdd:
                //    OnFilterAddMessageReceived((FilterAddPayload)msg.Payload);
                //    break;
                case MessageCommand.FilterClear:
                    OnFilterClearMessageReceived();
                    break;
                //case MessageCommand.FilterLoad:
                //    OnFilterLoadMessageReceived((FilterLoadPayload)msg.Payload);
                //    break;
                case MessageCommand.GetAddr:
                    OnGetAddrMessageReceived();
                    break;
                //case MessageCommand.GetBlocks:
                //    OnGetBlocksMessageReceived((GetBlocksPayload)msg.Payload);
                //    break;
                //case MessageCommand.GetBlockData:
                //    OnGetBlockDataMessageReceived((GetBlockDataPayload)msg.Payload);
                //    break;
                //case MessageCommand.GetData:
                //    OnGetDataMessageReceived((InvPayload)msg.Payload);
                //    break;
                //case MessageCommand.GetHeaders:
                //    OnGetHeadersMessageReceived((GetBlocksPayload)msg.Payload);
                //    break;
                //case MessageCommand.Headers:
                //    OnHeadersMessageReceived((HeadersPayload)msg.Payload);
                //    break;
                //case MessageCommand.Inv:
                //    OnInvMessageReceived((InvPayload)msg.Payload);
                //    break;
                //case MessageCommand.Mempool:
                //    OnMemPoolMessageReceived();
                //    break;
                case MessageCommand.Ping:
                    OnPingMessageReceived((PingPayload)msg.Payload);
                    break;
                case MessageCommand.Pong:
                    OnPongMessageReceived((PingPayload)msg.Payload);
                    break;
                //case MessageCommand.Transaction:
                //    if (msg.Payload.Size <= Transaction.MaxTransactionSize)
                //        OnInventoryReceived((Transaction)msg.Payload);
                //    break;
                case MessageCommand.Verack:
                case MessageCommand.Version:
                    throw new ProtocolViolationException();
                case MessageCommand.Alert:
                case MessageCommand.MerkleBlock:
                case MessageCommand.NotFound:
                case MessageCommand.Reject:
                default: break;
            }
        }

        private void OnAddrMessageReceived(AddrPayload payload)
        {
            Console.WriteLine($"OnAddrMessageReceived got {payload.AddressList.Length} addresses from payload.");
            system.LocalNode.Tell(new Peer.Peers
            {
                EndPoints = payload.AddressList.Select(p => p.EndPoint).Where(p => p.Port > 0)
            });
        }

        private void OnSignedMessageReceived(SourceSignedMessage msg)
        {
            //system.TaskManager.Tell(new TaskManager.TaskCompleted { Hash = inventory.Hash }, Context.Parent);
            system.LocalNode.Tell(new LocalNode.SignedMessageRelay { signedMessage = msg });
        }

        //private void OnFilterAddMessageReceived(FilterAddPayload payload)
        //{
        //    bloom_filter?.Add(payload.Data);
        //}

        private void OnFilterClearMessageReceived()
        {
            //bloom_filter = null;
        }

        //private void OnFilterLoadMessageReceived(FilterLoadPayload payload)
        //{
        //    bloom_filter = new BloomFilter(payload.Filter.Length * 8, payload.K, payload.Tweak, payload.Filter);
        //}

        /// <summary>
        /// Will be triggered when a MessageCommand.GetAddr message is received.
        /// Randomly select nodes from the local RemoteNodes and tells to RemoteNode actors a MessageCommand.Addr message.
        /// The message contains a list of networkAddresses from those selected random peers.
        /// </summary>
        private void OnGetAddrMessageReceived()
        {
            Random rand = new Random();
            IEnumerable<RemoteNode> peers = LocalNode.Singleton.RemoteNodes.Values
                .Where(p => p.ListenerTcpPort > 0)
                .GroupBy(p => p.Remote.Address, (k, g) => g.First())
                .OrderBy(p => rand.Next())
                .Take(AddrPayload.MaxCountToSend);
            NetworkAddressWithTime[] networkAddresses = peers.Select(p => NetworkAddressWithTime.Create(p.Listener.Address, p.Version.Timestamp, p.Version.Capabilities)).ToArray();

            Console.WriteLine($"OnGetAddrMessageReceived will send {networkAddresses.Length} addresses.");
            
            if (networkAddresses.Length == 0) return;
            EnqueueMessage(Message.Create(MessageCommand.Addr, AddrPayload.Create(networkAddresses)));
        }

/*        /// <summary>
        /// Will be triggered when a MessageCommand.GetBlocks message is received.
        /// Tell the specified number of blocks' hashes starting with the requested HashStart until payload.Count or MaxHashesCount
        /// Responses are sent to RemoteNode actor as MessageCommand.Inv Message.
        /// </summary>
        /// <param name="payload">A GetBlocksPayload including start block Hash and number of blocks requested.</param>
        private void OnGetBlocksMessageReceived(GetBlocksPayload payload)
        {
            UInt256 hash = payload.HashStart;
            // The default value of payload.Count is -1
            int count = payload.Count < 0 || payload.Count > InvPayload.MaxHashesCount ? InvPayload.MaxHashesCount : payload.Count;
            TrimmedBlock state = DagSystem.Singleton.Storage.View.Blocks.TryGet(hash);
            if (state == null) return;
            List<UInt256> hashes = new List<UInt256>();
            for (uint i = 1; i <= count; i++)
            {
                uint index = state.Index + i;
                if (index > DagSystem.Singleton.Storage.Height)
                    break;
                hash = DagSystem.Singleton.Storage.GetBlockHash(index);
                if (hash == null) break;
                hashes.Add(hash);
            }
            if (hashes.Count == 0) return;
            EnqueueMessage(Message.Create(MessageCommand.Inv, InvPayload.Create(InventoryType.Block, hashes.ToArray())));
        }

        private void OnGetBlockDataMessageReceived(GetBlockDataPayload payload)
        {
            for (uint i = payload.IndexStart, max = payload.IndexStart + payload.Count; i < max; i++)
            {
                Block block = DagSystem.Singleton.Storage.GetBlock(i);
                if (block == null)
                    break;

                if (bloom_filter == null)
                {
                    EnqueueMessage(Message.Create(MessageCommand.Block, block));
                }
                else
                {
                    BitArray flags = new BitArray(block.Transactions.Select(p => bloom_filter.Test(p)).ToArray());
                    EnqueueMessage(Message.Create(MessageCommand.MerkleBlock, MerkleBlockPayload.Create(block, flags)));
                }
            }
        }

        /// <summary>
        /// Will be triggered when a MessageCommand.GetData message is received.
        /// The payload includes an array of hash values.
        /// For different payload.Type (Tx, Block, Consensus), get the corresponding (Txs, Blocks, Consensus) and tell them to RemoteNode actor.
        /// </summary>
        /// <param name="payload">The payload containing the requested information.</param>
        private void OnGetDataMessageReceived(InvPayload payload)
        {
            var notFound = new List<UInt256>();
            foreach (UInt256 hash in payload.Hashes.Where(p => sentHashes.Add(p)))
            {
                switch (payload.Type)
                {
                    case InventoryType.TX:
                        Transaction tx = DagSystem.Singleton.Storage.GetTransaction(hash);
                        if (tx != null)
                            EnqueueMessage(Message.Create(MessageCommand.Transaction, tx));
                        else
                            notFound.Add(hash);
                        break;
                    case InventoryType.Block:
                        Block block = DagSystem.Singleton.Storage.GetBlock(hash);
                        if (block != null)
                        {
                            if (bloom_filter == null)
                            {
                                EnqueueMessage(Message.Create(MessageCommand.Block, block));
                            }
                            else
                            {
                                BitArray flags = new BitArray(block.Transactions.Select(p => bloom_filter.Test(p)).ToArray());
                                EnqueueMessage(Message.Create(MessageCommand.MerkleBlock, MerkleBlockPayload.Create(block, flags)));
                            }
                        }
                        else
                        {
                            notFound.Add(hash);
                        }
                        break;
                    default:
                        if (DagSystem.Singleton.Storage.RelayCache.TryGet(hash, out IInventory inventory))
                            EnqueueMessage(Message.Create((MessageCommand)payload.Type, inventory));
                        break;
                }
            }

            if (notFound.Count > 0)
            {
                foreach (InvPayload entry in InvPayload.CreateGroup(payload.Type, notFound.ToArray()))
                    EnqueueMessage(Message.Create(MessageCommand.NotFound, entry));
            }
        }

        /// <summary>
        /// Will be triggered when a MessageCommand.GetHeaders message is received.
        /// Tell the specified number of blocks' headers starting with the requested HashStart to RemoteNode actor.
        /// A limit set by HeadersPayload.MaxHeadersCount is also applied to the number of requested Headers, namely payload.Count.
        /// </summary>
        /// <param name="payload">A GetBlocksPayload including start block Hash and number of blocks' headers requested.</param>
        private void OnGetHeadersMessageReceived(GetBlocksPayload payload)
        {
            UInt256 hash = payload.HashStart;
            int count = payload.Count < 0 || payload.Count > HeadersPayload.MaxHeadersCount ? HeadersPayload.MaxHeadersCount : payload.Count;
            DataCache<UInt256, TrimmedBlock> cache = DagSystem.Singleton.Storage.View.Blocks;
            TrimmedBlock state = cache.TryGet(hash);
            if (state == null) return;
            List<Header> headers = new List<Header>();
            for (uint i = 1; i <= count; i++)
            {
                uint index = state.Index + i;
                hash = DagSystem.Singleton.Storage.GetBlockHash(index);
                if (hash == null) break;
                Header header = cache.TryGet(hash)?.Header;
                if (header == null) break;
                headers.Add(header);
            }
            if (headers.Count == 0) return;
            EnqueueMessage(Message.Create(MessageCommand.Headers, HeadersPayload.Create(headers.ToArray())));
        }

        private void OnHeadersMessageReceived(HeadersPayload payload)
        {
            if (payload.Headers.Length == 0) return;
            system.BlockChain.Tell(payload.Headers);
        }

        private void OnInventoryReceived(IInventory inventory)
        {
            system.TaskManager.Tell(new TaskManager.TaskCompleted { Hash = inventory.Hash });
            system.LocalNode.Tell(new LocalNode.Relay { Inventory = inventory });
            pendingKnownHashes.Remove(inventory.Hash);
            knownHashes.Add(inventory.Hash);
        }

        private void OnInvMessageReceived(InvPayload payload)
        {
            UInt256[] hashes = payload.Hashes.Where(p => !pendingKnownHashes.Contains(p) && !knownHashes.Contains(p) && !sentHashes.Contains(p)).ToArray();
            if (hashes.Length == 0) return;
            switch (payload.Type)
            {
                case InventoryType.Block:
                    using (SnapshotView snapshot = DagSystem.Singleton.Storage.GetSnapshot())
                        hashes = hashes.Where(p => !snapshot.ContainsBlock(p)).ToArray();
                    break;
                case InventoryType.TX:
                    using (SnapshotView snapshot = DagSystem.Singleton.Storage.GetSnapshot())
                        hashes = hashes.Where(p => !snapshot.ContainsTransaction(p)).ToArray();
                    break;
            }
            if (hashes.Length == 0) return;
            foreach (UInt256 hash in hashes)
                pendingKnownHashes.Add((hash, DateTime.UtcNow));
            system.TaskManager.Tell(new TaskManager.NewTasks { Payload = InvPayload.Create(payload.Type, hashes) });
        }

        private void OnMemPoolMessageReceived()
        {
            foreach (InvPayload payload in InvPayload.CreateGroup(InventoryType.TX, DagSystem.Singleton.Storage.MemPool.GetVerifiedTransactions().Select(p => p.Hash).ToArray()))
                EnqueueMessage(Message.Create(MessageCommand.Inv, payload));
        }*/

        private void OnPingMessageReceived(PingPayload payload)
        {
            UpdateLastBlockIndex(payload);
            EnqueueMessage(Message.Create(MessageCommand.Pong, PingPayload.Create(0/*DagSystem.Singleton.Storage.Height*/, payload.Nonce)));
        }

        private void OnPongMessageReceived(PingPayload payload)
        {
            UpdateLastBlockIndex(payload);
        }

        private void OnVerackMessageReceived()
        {
            verack = true;
            system.TaskManager.Tell(new TaskManager.Register { Version = Version });
            CheckMessageQueue();
        }

        private void OnVersionMessageReceived(VersionPayload payload)
        {
            if(null == payload)
            {
                Disconnect(true);
                return;
            }

            Version = payload;
            foreach (NodeCapability capability in payload.Capabilities)
            {
                switch (capability)
                {
                    case FullNodeCapability fullNodeCapability:
                        IsFullNode = true;
                        LastBlockIndex = fullNodeCapability.StartHeight;
                        break;
                    case ServerCapability serverCapability:
                        if (serverCapability.Type == NodeCapabilityType.TcpServer)
                            ListenerTcpPort = serverCapability.Port;
                        break;
                }
            }
            if (payload.Nonce == LocalNode.Nonce || payload.Magic != ProtocolSettings.Default.Magic)
            {
                Disconnect(true);
                return;
            }
            if (LocalNode.Singleton.RemoteNodes.Values.Where(p => p != this).Any(p => p.Remote.Address.Equals(Remote.Address) && p.Version?.Nonce == payload.Nonce))
            {
                Disconnect(true);
                return;
            }
            SendMessage(Message.Create(MessageCommand.Verack));
        }

        private void RefreshPendingKnownHashes()
        {
            while (pendingKnownHashes.Count > 0)
            {
                var (_, time) = pendingKnownHashes[0];
                if (DateTime.UtcNow - time <= PendingTimeout)
                    break;
                pendingKnownHashes.RemoveAt(0);
            }
        }

        private void UpdateLastBlockIndex(PingPayload payload)
        {
            if (payload.LastBlockIndex > LastBlockIndex)
            {
                LastBlockIndex = payload.LastBlockIndex;
                system.TaskManager.Tell(new TaskManager.Update { LastBlockIndex = LastBlockIndex });
            }
        }
    }
}
