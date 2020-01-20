using Akka.Actor;
using Akka.Cluster.Routing;
using Akka.Configuration;
using Akka.Routing;
using Clifton.Blockchain;
using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Authorizers;
using Lyra.Core.Blocks;
using Lyra.Core.Cryptography;
using Lyra.Core.Utils;
using Lyra.Shared;
using Microsoft.Extensions.Logging;
using Neo;
using Neo.IO.Actors;
using Neo.Network.P2P;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Neo.Network.P2P.LocalNode;

namespace Lyra.Core.Decentralize
{
    public class TransStats
    {
        public long ms { get; set; }
        public BlockTypes trans { get; set; }
    }
    // when out of sync, we adjust useed, continue to save blocks, and told blockchain to do sync.
    public enum ConsensusWorkingMode { Normal, OutofSyncWaiting }
    /// <summary>
    /// about seed generation: the seed0 seed will generate UIndex whild sending authorization message.
    /// </summary>
    public class ConsensusService : ReceiveActor
    {
        public class Consolidate { }
        public class AskForBillboard { }
        public class AskForStats { }
        public class BlockChainSynced { }
        public class Authorized { public bool IsSuccess { get; set; } }
        private readonly IActorRef _localNode;

        ILogger _log;

        IPBFTNet _pBFTNet;
        Orphanage _orphange;

        // hash, authState
        Dictionary<string, List<SourceSignedMessage>> _outOfOrderedMessages;
        ConcurrentDictionary<string, ConsensusWorker> _activeConsensus;
        Dictionary<string, ConsensusWorker> _cleanedConsensus;
        private static BillBoard _board;
        private List<TransStats> _stats;

        private long _UIndexSeed = -1;
        private object _seedLocker = new object();

        public long USeed
        {
            get
            {
                lock (_seedLocker)
                {
                    return _UIndexSeed;
                }
            }
            set
            {
                lock(_seedLocker)
                {
                    _UIndexSeed = value;
                }
            }
        }

        public bool IsThisNodeSeed0 => NodeService.Instance.PosWallet.AccountId == ProtocolSettings.Default.StandbyValidators[0];
        public ConsensusWorkingMode Mode { get; private set; }
        public static BillBoard Board { get => _board; set => _board = value; }
        public List<TransStats> Stats { get => _stats; set => _stats = value; }

        public ConsensusService(IActorRef localNode, IPBFTNet pBFTNet)
        {
            _localNode = localNode;
            _log = new SimpleLogger("ConsensusService").Logger;

            _outOfOrderedMessages = new Dictionary<string, List<SourceSignedMessage>>();
            _activeConsensus = new ConcurrentDictionary<string, ConsensusWorker>();
            _cleanedConsensus = new Dictionary<string, ConsensusWorker>();
            _stats = new List<TransStats>();

            while (BlockChain.Singleton == null)
                Task.Delay(100).Wait();

            Mode = ConsensusWorkingMode.OutofSyncWaiting;

            _orphange = new Orphanage(
                    async (state) => { var worker = new ConsensusWorker(this); worker.Create(state); },
                    async (msg1) => {
                        await OnNextConsensusMessageAsync(msg1);
                    },
                    async (msg2s) => {
                        foreach(var msg2 in msg2s)
                        {
                            var worker2 = GetWorker(msg2.BlockHash);
                            if (worker2 != null)
                                await worker2.OnPrepareAsync(msg2);
                        }
                    },
                    async (msg3s) => {
                        foreach(var msg3 in msg3s)
                        {
                            var worker3 = GetWorker(msg3.BlockHash);
                            if (worker3 != null)
                                worker3.OnCommit(msg3);
                        }
                    }
                );

            _pBFTNet = pBFTNet;
            _pBFTNet.RegisterMessageHandler(async (msg) =>
               {
                   _log.LogInformation($"Consensus: Got a pbft message: {msg.MsgType}");
                   // verify the signatures of msg. make sure it is from the right node.
                   if (!msg.VerifySignature(msg.From))
                   {
                       _log.LogInformation($"Consensus: bad signature: {msg.MsgType} Hash: {msg.Hash.Shorten()} by pubKey: {msg.From.Shorten()}");
                       return;
                   }

                   if (await _orphange.TryAddOneAsync(msg))
                       return;

                   await OnNextConsensusMessageAsync(msg);
               });

            ReceiveAsync<BlockChain.BlockAdded>(async (ba) => {
                await _orphange.BlockAddedAsync(ba.hash);
            });

            Receive<Consolidate>((_) =>
            {
                //_log.LogInformation("Doing Consolidate");
                //OnNodeActive(NodeService.Instance.PosWallet.AccountId);     // update billboard

                //if (Mode == ConsensusWorkingMode.Normal && _board != null && _board.CanDoConsensus)
                //{
                //    Task.Run(async () =>
                //    {
                //        await GenerateConsolidateBlockAsync();
                //    });
                //}

                //BroadCastBillBoard();
            });

            Receive<BillBoard>((bb) =>
            {
                _board = bb;
                foreach (var node in _board.AllNodes.Values)
                    {
                        if(node.AccountID != NodeService.Instance.PosWallet.AccountId)
                            _pBFTNet.AddPosNode(node);
                    }                    
            });

            Receive<AskForBillboard>((_) => Sender.Tell(_board));
            Receive<AskForStats>((_) => Sender.Tell(_stats));

            ReceiveAsync<SignedMessageRelay>(async relayMsg =>
            {
                if (relayMsg.signedMessage.Version == LyraGlobal.ProtocolVersion)
                    try
                    {
                        await OnNextConsensusMessageAsync(relayMsg.signedMessage);
                    }
                    catch(Exception ex)
                    {
                        _log.LogCritical("OnNextConsensusMessageAsync!!! " + ex.Message);
                    }
                else
                    _log.LogWarning("Protocol Version Mismatch. Do nothing.");
            });

            ReceiveAsync<BlockChainSynced>(async _ =>
            {
                Mode = ConsensusWorkingMode.Normal;
                USeed = (await BlockChain.Singleton.FindLatestBlockAsync()).UIndex + 1;

                _log.LogInformation($"The USeed is {USeed}");

                int waitCount = 60;
                while (LocalNode.Singleton.RemoteNodes.Count < 1 && waitCount > 0)
                {
                    _log.LogWarning("Not connected to Lyra Network. Delay sending... ");
                    await Task.Delay(1000);
                    waitCount--;
                }

                // declare to the network
                PosNode me = new PosNode(NodeService.Instance.PosWallet.AccountId);
                me.IP = Utilities.LocalIPAddress().ToString();
                var msg = new ChatMsg(NodeService.Instance.PosWallet.AccountId, ChatMessageType.NodeUp, JsonConvert.SerializeObject(me));

                Send2P2pNetwork(msg);
            });


            // if missing blocks
            // the block may be in a consens process. so we wait for 30 seconds to allow it to arrive.
            // the queue of orphan. 
            // when some block is done, the orphan queue is weaken to finish it's journey.

            ReceiveAsync<AuthState>(async state =>
            {
                //TODO: check  || _context.Board == null || !_context.Board.CanDoConsensus
                if (await _orphange.TryAddOneAsync(state))
                    return;

                var worker = new ConsensusWorker(this);
                worker.Create(state);
            });

            Task.Run(async () =>
            {
                HeartBeat();
                int count = 0;
                while (true)
                {
                    if (Mode == ConsensusWorkingMode.Normal)
                    {
                        await GenerateConsolidateBlockAsync();
                    }

                    // remove unresponsible node
                    if(_board != null)
                    {
                        bool changed = false;
                        foreach(var node in _board.AllNodes.Values.Where(a => !a.AbleToAuthorize).ToArray())
                        {
                            _board.AllNodes.Remove(node.AccountID);
                            _pBFTNet.RemovePosNode(node);
                            changed = true;
                        }
                        if(changed)
                        {
                            BroadCastBillBoard();
                        }
                    }

                    await Task.Delay(10000).ConfigureAwait(false);
                    count++;

                    if(count >= 3)
                    {
                        HeartBeat();
                        count = 0;
                    }
                }
            });
        }

        private void GetAllWorkers()
        {

        }

        private void HeartBeat()
        {
            OnNodeActive(NodeService.Instance.PosWallet.AccountId);     // update billboard

            // declare to the network
            var msg = new ChatMsg
            {
                From = NodeService.Instance.PosWallet.AccountId,
                MsgType = ChatMessageType.HeartBeat,
                Text = "I'm live"
            };

            Send2P2pNetwork(msg);

            if (IsThisNodeSeed0)
            {
                BroadCastBillBoard();
            }
        }

        private async Task GenerateConsolidateBlockAsync()
        {
            // should lock the uindex seed here.
            // after clean, if necessary, insert a consolidate block into the queue
            // next time do clean, if no null block before the consolidate block, then send out the consolidate block.
            // 2 phase consolidation
            var lastCons = await BlockChain.Singleton.GetSyncBlockAsync();
            ConsolidationBlock currentCons = null;
            try
            {
                Monitor.Enter(_seedLocker);

                // first clean cleaned states
                var cleaned = _cleanedConsensus.Values.ToArray();
                for (int i = 0; i < cleaned.Length; i++)
                {
                    var state = cleaned[i].State;
                    if (DateTime.Now - state.Created > TimeSpan.FromMinutes(2)) // 2 mins
                    {
                        _cleanedConsensus.Remove(state.InputMsg.Block.Hash);
                    }
                }

                var states = _activeConsensus.Values.ToArray();
                for (int i = 0; i < states.Length; i++)
                {
                    var state = states[i].State;    // TODO: check null
                    if (state != null && DateTime.Now - state.Created > TimeSpan.FromSeconds(15)) // consensus timeout
                    {
                        var finalResult = state.Consensus;
                        _activeConsensus.TryRemove(state.InputMsg.Block.Hash, out _);
                        state.Done.Set();

                        _cleanedConsensus.Add(state.InputMsg.Block.Hash, states[i]);

                        //if (finalResult == true)
                        //    continue;

                        //// replace the failed block with nulltrans
                        //var myAuthResult = state.OutputMsgs.FirstOrDefault(a => a.From == NodeService.Instance.PosWallet.AccountId);
                        //if (myAuthResult == null)
                        //{
                        //    // fatal error. should not happen
                        //    _log.LogError("No auth result from seed0. should not happen.");
                        //    continue;
                        //}

                        //var ndx = myAuthResult.BlockUIndex;
                        //if (ndx == 0)    // not got yet
                        //    continue;

                        //// check if the block is orphaned success block
                        //var existingBlock = BlockChain.Singleton.GetBlockByUIndex(ndx);
                        //if (existingBlock != null)
                        //{
                        //    _log.LogInformation($"in GenerateConsolidateBlock: orphaned message for {ndx} detected.");
                        //    continue;
                        //}
                        
                        // no need for this. just leave it as hole
                        //var nb = new NullTransactionBlock
                        //{
                        //    UIndex = ndx,
                        //    FailedBlockHash = myAuthResult.BlockHash,
                        //    NetworkId = lastCons.NetworkId,
                        //    ShardId = lastCons.ShardId,
                        //    ServiceHash = lastCons.ServiceHash,
                        //    AccountID = NodeService.Instance.PosWallet.AccountId,
                        //    PreviousConsolidateHash = lastCons.Hash
                        //};
                        //nb.InitializeBlock(null, NodeService.Instance.PosWallet.PrivateKey,
                        //    lastCons.NetworkId, lastCons.ShardId,
                        //    NodeService.Instance.PosWallet.AccountId);
                        //nb.UHash = SignableObject.CalculateHash($"{nb.UIndex}|{nb.Index}|{nb.Hash}");

                        //SendServiceBlock(nb);
                    }
                }

                //// if necessary, insert a new ConsolidateBlock
                //if (USeed - lastCons.UIndex > 1024 || DateTime.Now - lastCons.TimeStamp > TimeSpan.FromMinutes(10))
                //{
                //    var authGenesis = BlockChain.Singleton.GetLastServiceBlock();
                //    currentCons = new ConsolidationBlock
                //    {
                //        UIndex = USeed++,
                //        NetworkId = authGenesis.NetworkId,
                //        ShardId = authGenesis.ShardId,
                //        ServiceHash = authGenesis.Hash,
                //        SvcAccountID = NodeService.Instance.PosWallet.AccountId
                //    };
                //}
            }
            catch (Exception ex)
            {
                _log.LogError("Error In GenerateConsolidateBlock: " + ex.Message);
            }
            finally
            {
                Monitor.Exit(_seedLocker);
            }

            //if (currentCons != null)
            //{
            //    var mt = new MerkleTree();
            //    for (var ndx = lastCons.UIndex + 1; ndx < currentCons.UIndex; ndx++)      // TODO: handling "losing" block here
            //    {
            //        var block = BlockChain.Singleton.GetBlockByUIndex(ndx);
            //        if (block == null)
            //        {
            //            _log.LogError("GenerateConsolidateBlock Fatal Error!!! should not happend.");
            //            Task.Delay(100000000).Wait();
            //        }
            //        var mhash = MerkleHash.Create(block.UHash);
            //        mt.AppendLeaf(mhash);
            //    }

            //    currentCons.MerkelTreeHash = mt.BuildTree().ToString();
            //    currentCons.InitializeBlock(lastCons, NodeService.Instance.PosWallet.PrivateKey,
            //        currentCons.NetworkId, currentCons.ShardId,
            //        NodeService.Instance.PosWallet.AccountId);

            //    currentCons.UHash = SignableObject.CalculateHash($"{currentCons.UIndex}|{currentCons.Index}|{currentCons.Hash}");

            //    SendServiceBlock(currentCons);

            //    // use merkle tree to consolidate all previous blocks, from lastCons.UIndex to xx[consBlock.UIndex -1]xx may lost the newest block
            //    // if the block is old enough ( > 2 mins ), it should be replaced by NullTransactionBlock.
            //    // in fact we should reserve consolidate block number and wait 2min to do consolidating
            //    // all null block's previous block is the last consolidate block, it's index is counted from 1 related to previous block
            //}
        }

        //private AuthState SendServiceBlock(TransactionBlock svcBlock)
        //{
        //    AuthorizingMsg msg = new AuthorizingMsg
        //    {
        //        From = NodeService.Instance.PosWallet.AccountId,
        //        Block = svcBlock,
        //        MsgType = ChatMessageType.AuthorizerPrePrepare
        //    };

        //    var state = CreateAuthringState(msg);
        //    var localAuthResult = LocalAuthorizingAsync(msg);
        //    state.AddAuthResult(localAuthResult);

        //    if (!localAuthResult.IsSuccess)
        //    {
        //        _log.LogError($"Fatal Error: Consolidate block local authorization failed: {localAuthResult.Result}");
        //    }
        //    else
        //    {
        //        Send2P2pNetwork(msg);
        //        Send2P2pNetwork(localAuthResult);
        //    }

        //    return state;
        //}

        public static Props Props(IActorRef localNode, IPBFTNet pBFTNet)
        {
            return Akka.Actor.Props.Create(() => new ConsensusService(localNode, pBFTNet)).WithMailbox("consensus-service-mailbox");
        }

        public virtual void Send2P2pNetwork(SourceSignedMessage item)
        {
            item.Sign(NodeService.Instance.PosWallet.PrivateKey, item.From);

            if (item is ChatMsg)
                _localNode.Tell(item);
            else
                _pBFTNet.BroadCastMessage(item);
        }

        private ConsensusWorker GetWorker(string hash)
        {
            if (_cleanedConsensus.ContainsKey(hash))        // > 2min outdated.
            {
                _log.LogWarning($"GetWorker: no worker for expired hash: {hash.Shorten()}");
                return null;
            }

            if(_activeConsensus.ContainsKey(hash))
                return _activeConsensus[hash];
            else
            {
                var worker = new ConsensusWorker(this);
                if (_activeConsensus.TryAdd(hash, worker))
                    return worker;
                else
                    return _activeConsensus[hash];
            }
        }

        async Task OnNextConsensusMessageAsync(SourceSignedMessage item)
        {
            _log.LogInformation($"Consensus: OnNextConsensusMessageAsync Called: {item.MsgType} From: {item.From.Shorten()}");

            if(item.MsgType != ChatMessageType.NodeUp)
                OnNodeActive(item.From);

            _log.LogInformation($"Consensus: OnNextConsensusMessageAsync 2 {item.MsgType}");
            switch (item)
            {
                case AuthorizingMsg msg1:
                    var worker = GetWorker(msg1.Block.Hash);
                    if(worker != null)
                        await worker.OnPrePrepareAsync(msg1);
                    break;
                case AuthorizedMsg msg2:
                    _log.LogInformation($"Consensus: OnNextConsensusMessageAsync 3 {item.MsgType}");
                    var worker2 = GetWorker(msg2.BlockHash);
                    if (worker2 != null)
                        await worker2.OnPrepareAsync(msg2);
                    _log.LogInformation($"Consensus: OnNextConsensusMessageAsync 4 {item.MsgType}");
                    break;
                case AuthorizerCommitMsg msg3:
                    var worker3 = GetWorker(msg3.BlockHash);
                    if (worker3 != null)
                        worker3.OnCommit(msg3);
                    break;
                case ChatMsg chat when chat.MsgType == ChatMessageType.HeartBeat:
                    OnHeartBeat(chat);
                    break;
                case ChatMsg chat when chat.MsgType == ChatMessageType.NodeUp:
                    await OnNodeUpAsync(chat);
                    break;
                case ChatMsg bbb when bbb.MsgType == ChatMessageType.StakingChanges:
                    OnBillBoardBroadcast(bbb);
                    break;
                case ChatMsg bcc when bcc.MsgType == ChatMessageType.BlockConsolidation:
                    await OnBlockConsolicationAsync(bcc);
                    break;
                default:
                    // log msg unknown
                    break;
            }
        }

        private async Task OnBlockConsolicationAsync(ChatMsg msg)
        {
            if (!IsThisNodeSeed0)
            {
                var block = JsonConvert.DeserializeObject<ConsolidationBlock>(msg.Text);
                try
                {
                    await BlockChain.Singleton.AddBlockAsync(block);
                    _log.LogInformation($"Receive and store ConsolidateBlock of UIndex: {block.UIndex}");
                }
                catch(Exception e)
                {
                    _log.LogInformation($"OnBlockConsolication UIndex: {block.UIndex} Failed: {e.Message}");
                }                
            }
        }

        private void OnBillBoardBroadcast(ChatMsg msg)
        {
            if (!IsThisNodeSeed0) //TODO: only accept bbb from seeds
            {
                _board = JsonConvert.DeserializeObject<BillBoard>(msg.Text);
                _log.LogInformation("BillBoard updated!");
            }
        }

        private void BroadCastBillBoard()
        {
            if(_board != null)
            {
                var msg = new ChatMsg(NodeService.Instance.PosWallet.AccountId, ChatMessageType.StakingChanges, JsonConvert.SerializeObject(_board));
                Send2P2pNetwork(msg);
            }
        }

        public void OnNodeActive(string nodeAccountId)
        {
            if (_board != null)
                _board.RefreshAsync(nodeAccountId);
        }

        private void OnHeartBeat(ChatMsg chat)
        {
            if (_board != null)
            {
                _board.RefreshAsync(chat.From);

                if (_board.HasNode(chat.From))
                    _pBFTNet.PingNode(_board.GetNode(chat.From));
            }
        }

        private async Task OnNodeUpAsync(ChatMsg chat)
        {
            if (_board == null)
                return;

            var node = chat.Text.UnJson<PosNode>();
            _ = await _board.AddAsync(node);

            if (node.AccountID != NodeService.Instance.PosWallet.AccountId)
                _pBFTNet.AddPosNode(node);

            node.IP = JsonConvert.DeserializeObject<PosNode>(chat.Text).IP;

            if (IsThisNodeSeed0)
            {
                // broadcast billboard
                BroadCastBillBoard();
            }

            if (node.Balance < LyraGlobal.MinimalAuthorizerBalance)
            {
                _log.LogInformation("Node {0} has not enough balance: {1}.", node.AccountID, node.Balance);
            }
        }
    }

    internal class ConsensusServiceMailbox : PriorityMailbox
    {
        public ConsensusServiceMailbox(Akka.Actor.Settings settings, Config config)
            : base(settings, config)
        {
        }

        internal protected override bool IsHighPriority(object message)
        {
            switch (message)
            {
                //case ConsensusPayload _:
                //case ConsensusService.SetViewNumber _:
                //case ConsensusService.Timer _:
                //case Blockchain.PersistCompleted _:
                //                    return true;
                default:
                    return false;
            }
        }
    }
}
