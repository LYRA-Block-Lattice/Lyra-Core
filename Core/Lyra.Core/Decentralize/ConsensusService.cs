using Akka.Actor;
using Akka.Configuration;
using Clifton.Blockchain;
using Core.Authorizers;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Cryptography;
using Lyra.Core.Utils;
using Lyra.Shared;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Neo;
using Neo.IO.Actors;
using Neo.Network.P2P;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Neo.Network.P2P.LocalNode;
using Settings = Neo.Settings;

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
        public class Startup { }
        public class Consolidate { }
        public class AskForBillboard { }
        public class AskForStats { }
        public class AskForDbStats { }
        public class AskForMaxActiveUID { }
        public class AskIfSeed0 { public bool IsSeed0 { get; set; } }
        public class ReplyForMaxActiveUID { public long? uid { get; set; } }
        public class BlockChainSynced { }
        public class NodeInquiry { }

        public class ConsolidateFailed { public string consolidationBlockHash { get; set; } }
        public class Authorized { public bool IsSuccess { get; set; } }
        private readonly IActorRef _localNode;
        private readonly IActorRef _blockchain;

        ILogger _log;
        Orphanage _orphange;

        ConcurrentDictionary<string, ConsensusWorker> _activeConsensus;
        ConcurrentDictionary<string, ConsensusWorker> _cleanedConsensus;
        List<Vote> _lastVotes;
        private BillBoard _board;
        private List<TransStats> _stats;

        public bool IsThisNodeSeed0 => _sys.PosWallet.AccountId == ProtocolSettings.Default.StandbyValidators[0];
        public bool IsMessageFromSeed0(SourceSignedMessage msg)
        {
            return msg.From == ProtocolSettings.Default.StandbyValidators[0];
        }
        public BillBoard Board { get => _board; }
        public List<TransStats> Stats { get => _stats; }

        // authorizer snapshot
        public HashSet<string> AuthorizerShapshot { get; private set; }

        private DagSystem _sys;
        public DagSystem GetDagSystem() => _sys;
        public ConsensusService(DagSystem sys, IActorRef localNode, IActorRef blockchain)
        {
            _sys = sys;
            _localNode = localNode;
            _blockchain = blockchain;
            _log = new SimpleLogger("ConsensusService").Logger;

            _activeConsensus = new ConcurrentDictionary<string, ConsensusWorker>();
            _cleanedConsensus = new ConcurrentDictionary<string, ConsensusWorker>();
            _stats = new List<TransStats>();

            _board = new BillBoard();
            _board.CurrentLeader = ProtocolSettings.Default.StandbyValidators[0];          // default to seed0
            _board.PrimaryAuthorizers = ProtocolSettings.Default.StandbyValidators;        // default to seeds

            _orphange = new Orphanage(
                    _sys,
                    async (state) => {
                        _log.LogInformation($"AuthState from Orphanage: {state.InputMsg.Block.Height}/{state.InputMsg.Block.Hash}");
                        var worker = await GetWorkerAsync(state.InputMsg.Block.Hash); 
                        worker.Create(state); 
                    },
                    async (msg1) => {
                        await OnNextConsensusMessageAsync(msg1);
                    },
                    async (msg2s) => {
                        foreach (var msg2 in msg2s)
                        {
                            var worker2 = await GetWorkerAsync(msg2.BlockHash);
                            if (worker2 != null)
                                await worker2.OnPrepareAsync(msg2);
                        }
                    },
                    async (msg3s) => {
                        foreach (var msg3 in msg3s)
                        {
                            var worker3 = await GetWorkerAsync(msg3.BlockHash);
                            if (worker3 != null)
                                await worker3.OnCommitAsync(msg3);
                        }
                    }
                );

            ReceiveAsync<Startup>(async state =>
            {
                // generate billboard from last service block
                var lastSvcBlk = await _sys.Storage.GetLastServiceBlockAsync();
                if (lastSvcBlk != null)
                {
                    _board.AllNodes.Clear();
                    foreach (var node in lastSvcBlk.Authorizers)
                        _board.AllNodes.Add(node);

                    _board.PrimaryAuthorizers = lastSvcBlk.Authorizers.Select(a => a.AccountID).ToArray();
                }
                await DeclareConsensusNodeAsync();
            });

            Receive<AskIfSeed0>((_) => Sender.Tell(new AskIfSeed0 { IsSeed0 = IsThisNodeSeed0 }));

            ReceiveAsync<BlockChain.BlockAdded>(async (ba) =>
            {
                await _orphange.BlockAddedAsync(ba.hash);
            });

            Receive<Consolidate>((_) =>
            {
                _log.LogInformation("Doing Consolidate");

                Task.Run(async () =>
                {
                    await CreateConsolidateBlockAsync();
                });
            });

            Receive<BillBoard>((bb) =>
            {
                //_board = bb;
                //foreach (var node in _board.AllNodes.Values)
                //    {
                //        if(node.AccountID != _sys.PosWallet.AccountId)
                //            _pBFTNet.AddPosNode(node);
                //    }                    
            });

            Receive<AskForBillboard>((_) => { Sender.Tell(_board); });
            Receive<AskForStats>((_) => Sender.Tell(_stats));
            Receive<AskForDbStats>((_) => Sender.Tell(PrintProfileInfo()));
            Receive<AskForMaxActiveUID>((_) => {
                var reply = new ReplyForMaxActiveUID();
                //if(_activeConsensus.Any())
                //{
                //    reply.uid = _activeConsensus.Values.Max(a => a.State?.InputMsg.Block.)
                //}
                reply.uid = 0;
                Sender.Tell(reply); });

            ReceiveAsync<SignedMessageRelay>(async relayMsg =>
            {
                if (relayMsg == null || relayMsg.signedMessage == null)
                    return;

                if (relayMsg.signedMessage.Version == LyraGlobal.ProtocolVersion)
                    try
                    {
                        await OnNextConsensusMessageAsync(relayMsg.signedMessage);
                    }
                    catch(Exception ex)
                    {
                        _log.LogCritical("OnNextConsensusMessageAsync!!! " + ex.ToString());
                    }
                else
                    _log.LogWarning("Protocol Version Mismatch. Do nothing.");
            });

            ReceiveAsync<BlockChainSynced>(async _ =>
            {
                int waitCount = 60;
                while (LocalNode.Singleton.RemoteNodes.Count < 1 && waitCount > 0)
                {
                    _log.LogInformation("Not connected to p2p network. Delay sending... ");
                    await Task.Delay(1000);
                    waitCount--;
                }

                await DeclareConsensusNodeAsync();
            });

            ReceiveAsync<AuthState>(async state =>
            {
                //TODO: check  || _context.Board == null || !_context.Board.CanDoConsensus
                if(state.InputMsg.Block is TransactionBlock)
                {
                    var acctId = (state.InputMsg.Block as TransactionBlock).AccountID;
                    if (FindActiveBlock(acctId, state.InputMsg.Block.Height))
                    {
                        _log.LogCritical($"Double spent detected for {acctId}, index {state.InputMsg.Block.Height}");
                        return;
                    }
                }

                if (await AddOrphanAsync(state))
                    return;

                await SubmitToConsensusAsync(state);
            });

            Receive<NodeInquiry>((_) => {
                var inq = new ChatMsg("", ChatMessageType.NodeStatusInquiry);
                inq.From = _sys.PosWallet.AccountId;
                Send2P2pNetwork(inq);
                _log.LogInformation("Inquiry for node status.");
            });

            Receive<Idle>(o => { });

            ReceiveAny((o) => { _log.LogWarning($"consensus svc receive unknown msg: {o.GetType().Name}"); });

            Task.Run(async () =>
            {
                int count = 0;
                while (true)
                {
                    try
                    {
                        //_log.LogWarning("starting maintaince loop... ");
                        //await StateMaintainceAsync();

                        await Task.Delay(15000).ConfigureAwait(false);

                        HeartBeat();

                        count++;

                        if (count > 4 * 5)     // 5 minutes
                        {

                            count = 0;
                        }
                    }
                    catch(Exception ex)
                    {
                        _log.LogWarning("In maintaince loop: " + ex.ToString());
                    }
                }
            });
        }

        public Task<bool> AddOrphanAsync(AuthState state)
        {
            return _orphange.TryAddOneAsync(state);
        }

        private string PrintProfileInfo()
        {
            // debug: measure time
            // debug only
            var dat = StopWatcher.Data;

            var sbLog = new StringBuilder();

            var q = dat.Select(g => new
             {
                 name = g.Key,
                 times = g.Value.Count(),
                 totalTime = g.Value.Sum(t => t.MS),
                 avgTime = g.Value.Sum(t => t.MS) / g.Value.Count()
            })
             .OrderByDescending(b => b.totalTime);
            foreach (var d in q)
            {
                sbLog.AppendLine($"Total time: {d.totalTime} times: {d.times} avg: {d.avgTime} ms. Method Name: {d.name}  ");
            }

            var info = sbLog.ToString();

            _log.LogInformation("\n------------------------\n" + sbLog.ToString() + "\n------------------------\\n");

            StopWatcher.Reset();
            return info;
        }

        public virtual void Send2P2pNetwork(SourceSignedMessage item)
        {
            item.Sign(_sys.PosWallet.PrivateKey, item.From);
            //item.Hash = "a";
            //item.Signature = "a";

            //if(LyraNodeConfig.GetNetworkId() != "xtest")
            //{
            //    while (LocalNode.Singleton.ConnectedCount < 1)
            //    {
            //        _log.LogInformation("p2p network not connected. delay sending message...");
            //        Task.Delay(1000).Wait();
            //    }
            //}

            //_log.LogInformation($"Send2P2pNetwork {item.MsgType}");
            _localNode.Tell(item);
        }

        private async Task DeclareConsensusNodeAsync()
        {
            // declare to the network
            PosNode me = new PosNode(_sys.PosWallet.AccountId);
            me.IPAddress = $"{await GetPublicIPAddress.PublicIPAddressAsync(Settings.Default.LyraNode.Lyra.NetworkId)}";
            me.Signature = Signatures.GetSignature(_sys.PosWallet.PrivateKey,
                    me.IPAddress, _sys.PosWallet.AccountId);

            var msg = new ChatMsg(JsonConvert.SerializeObject(me), ChatMessageType.NodeUp);
            msg.From = _sys.PosWallet.AccountId;
            _board.Add(me);
            Send2P2pNetwork(msg);
        }

        private void HeartBeat()
        {
            OnNodeActive(_sys.PosWallet.AccountId);     // update billboard

            // declare to the network
            var msg = new ChatMsg
            {
                From = _sys.PosWallet.AccountId,
                MsgType = ChatMessageType.HeartBeat,
                Text = "I'm live"
            };

            Send2P2pNetwork(msg);
        }

        public bool FindActiveBlock(string accountId, long index)
        {
            return false;
            //return _activeConsensus.Values.Where(s => s.State != null)
            //    .Select(t => t.State.InputMsg.Block as TransactionBlock)
            //    .Where(x => x != null)
            //    .Any(a => a.AccountID == accountId && a.Index == index && a is SendTransferBlock);
        }

        private async Task StateMaintainceAsync()
        {
            // expire partial transaction.
            // "patch" the exmpty UIndex
            // collec fees and do redistribute
            var lastCons = await _sys.Storage.GetLastConsolidationBlockAsync();
            if (lastCons == null)
                return;         // wait for genesis

            try
            {
                // first clean cleaned states
                var cleaned = _cleanedConsensus.Values.ToArray();
                for (int i = 0; i < cleaned.Length; i++)
                {
                    var state = cleaned[i].State;
                    if (DateTime.Now - state.Created > TimeSpan.FromMinutes(1)) // 2 mins
                    {
                        var finalResult = state.CommitConsensus;
                        if (finalResult == ConsensusResult.Uncertain)
                            _log.LogWarning($"Permanent remove timeouted Uncertain block: {state.InputMsg.Block.Hash}");
                        _cleanedConsensus.TryRemove(state.InputMsg.Block.Hash, out _);
                    }
                }

                var states = _activeConsensus.Values.ToArray();
                for (int i = 0; i < states.Length; i++)
                {
                    var state = states[i].State; 
                    if (state != null && DateTime.Now - state.Created > TimeSpan.FromSeconds(30)) // consensus timeout
                    {
                        var finalResult = state.CommitConsensus;
                        if(finalResult == ConsensusResult.Uncertain)
                            _log.LogWarning($"temporary remove timeouted Uncertain block: {state.InputMsg.Block.Hash}");

                        _activeConsensus.TryRemove(state.InputMsg.Block.Hash, out _);
                        state.Done?.Set();

                        _cleanedConsensus.TryAdd(state.InputMsg.Block.Hash, states[i]);
                    }
                }

                //if necessary, insert a new ConsolidateBlock
                var blockchainStatus = await _blockchain.Ask<NodeStatus>(new BlockChain.QueryBlockchainStatus());
                if (IsThisNodeSeed0 && blockchainStatus.state == BlockChainState.Almighty)
                {
                    //// test code
                    //var livingPosNodeIds = _board.AllNodes.Keys.ToArray();
                    //_lastVotes = _sys.Storage.FindVotes(livingPosNodeIds);
                    //// end test code
                    bool allNodeSyncd = false;
                    try
                    {
                        allNodeSyncd = true;// await CheckPrimaryNodesStatus();
                    }
                    catch(Exception ex)
                    {
                        _log.LogWarning("Exception in CheckPrimaryNodesStatus: " + ex.ToString());
                    }
                    if(allNodeSyncd)
                    {
                        var unConsList = await _sys.Storage.GetAllUnConsolidatedBlockHashesAsync();
                        var lastConsBlock = await _sys.Storage.GetLastConsolidationBlockAsync();

                        if (unConsList.Count() > 10 || (unConsList.Count() > 1 && DateTime.UtcNow - lastConsBlock.TimeStamp > TimeSpan.FromMinutes(10)))
                        {
                            try
                            {
                                await CreateConsolidateBlockAsync();
                            }
                            catch (Exception ex)
                            {
                                _log.LogError($"In creating consolidation block: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        // so there is inconsistant between seed0 and other nodes.

                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError("Error In GenerateConsolidateBlock: " + ex.Message);
            }
            finally
            {
                
            }
        }

        private async Task<bool> CheckPrimaryNodesStatus()
        {
            if (Board.AllNodes.Count < 4)
                return false;

            var bag = new ConcurrentDictionary<string, GetSyncStateAPIResult>();
            var tasks = Board.AllNodes
                .Where(a => Board.PrimaryAuthorizers.Contains(a.AccountID))  // exclude self
                .Select(b => b)
                .Select(async node =>
                {
                    var lcx = LyraRestClient.Create(Neo.Settings.Default.LyraNode.Lyra.NetworkId, Environment.OSVersion.ToString(), "Seed0", "1.0", $"http://{node.IPAddress}:4505/api/Node/");
                    try
                    {
                        var syncState = await lcx.GetSyncState();
                        bag.TryAdd(node.AccountID, syncState);
                    }
                    catch (Exception ex)
                    {
                        bag.TryAdd(node.AccountID, null);
                    }
                });
            await Task.WhenAll(tasks);
            var mySyncState = bag[_sys.PosWallet.AccountId];
            var q = bag.Where(a => a.Key != _sys.PosWallet.AccountId)
                .Select(a => a.Value)
                .GroupBy(x => x.Status.lastUnSolidationHash)
                .Select(g => new { Hash = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .First();

            if(mySyncState.Status.lastUnSolidationHash == q.Hash && q.Count > (int)Math.Ceiling((double)(Board.PrimaryAuthorizers.Length) * 3 / 2))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private async Task CreateConsolidateBlockAsync()
        {
            if (_activeConsensus.Values.Count > 0 && _activeConsensus.Values.Any(a => a.State?.InputMsg.Block is ConsolidationBlock))
                return;

            var lastCons = await _sys.Storage.GetLastConsolidationBlockAsync();
            var collection = await _sys.Storage.GetAllUnConsolidatedBlockHashesAsync();
            _log.LogInformation($"Creating ConsolidationBlock... ");

            var consBlock = new ConsolidationBlock
            {
                blockHashes = collection.ToList(),
                totalBlockCount = lastCons.totalBlockCount + collection.Count()
            };

            var mt = new MerkleTree();
            decimal feeAggregated = 0;
            foreach(var hash in consBlock.blockHashes)
            {
                mt.AppendLeaf(MerkleHash.Create(hash));

                // aggregate fees
                var transBlock = (await _sys.Storage.FindBlockByHashAsync(hash)) as TransactionBlock;
                if(transBlock != null)
                {
                    feeAggregated += transBlock.Fee;
                }
            }

            consBlock.totalFees = feeAggregated.ToBalanceLong();
            consBlock.MerkelTreeHash = mt.BuildTree().ToString();
            consBlock.ServiceHash = (await _sys.Storage.GetLastServiceBlockAsync()).Hash;

            consBlock.InitializeBlock(lastCons, _sys.PosWallet.PrivateKey,
                _sys.PosWallet.AccountId);

            AuthorizingMsg msg = new AuthorizingMsg
            {
                From = _sys.PosWallet.AccountId,
                Block = consBlock,
                MsgType = ChatMessageType.AuthorizerPrePrepare
            };

            var state = new AuthState(true);
            state.SetView(await _sys.Storage.GetLastServiceBlockAsync());
            state.InputMsg = msg;

            _ = Task.Run(async () =>
            {
                _log.LogInformation($"Waiting for ConsolidateBlock authorizing...");

                await state.Done.AsTask();
                state.Done.Close();
                state.Done = null;

                if (state.CommitConsensus == ConsensusResult.Yea)
                {
                    _log.LogInformation($"ConsolidateBlock is OK. update vote stats.");

                    var livingPosNodeIds = _board.AllNodes.Select(a => a.AccountID);
                    _lastVotes = _sys.Storage.FindVotes(livingPosNodeIds);
                }
                else
                {
                    _log.LogInformation($"ConsolidateBlock is Failed. vote stats not updated.");
                }
            });

            await SubmitToConsensusAsync(state);

            _log.LogInformation($"ConsolidationBlock was submited. ");
        }

        public static Props Props(DagSystem sys, IActorRef localNode, IActorRef blockchain)
        {
            return Akka.Actor.Props.Create(() => new ConsensusService(sys, localNode, blockchain)).WithMailbox("consensus-service-mailbox");
        }

        public void FinishBlock(string hash)
        {
            _activeConsensus.TryRemove(hash, out _);
            _log.LogInformation($"_activeConsensus: {_activeConsensus.Count}");
        }

        private async Task SubmitToConsensusAsync(AuthState state)
        {
            if(state.InputMsg?.Block?.BlockType == BlockTypes.SendTransfer)
            {
                var tx = state.InputMsg.Block as TransactionBlock;
                var allSend = _activeConsensus.Values.Where(a => a.State?.InputMsg?.Block?.BlockType == BlockTypes.SendTransfer)
                    .Select(x => x.State.InputMsg.Block as TransactionBlock);

                var sameHeight = allSend.Any(y => y.AccountID == tx.AccountID && y.Height == tx.Height);
                var sameHash = _activeConsensus.Values.Any(a => a.State?.InputMsg.Hash == tx.Hash);
                if(sameHeight || sameHash)
                {
                    _log.LogCritical($"double spend detected: {tx.AccountID} Height: {tx.Height} Hash: {tx.Hash}");
                    return;
                }
            }

            var worker = await GetWorkerAsync(state.InputMsg.Block.Hash);
            worker.Create(state);
        }

        private async Task<ConsensusWorker> GetWorkerAsync(string hash)
        {
            // if a block is in database
            var aBlock = await _sys.Storage.FindBlockByHashAsync(hash);
            if (aBlock != null)
                return null;

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
            //_log.LogInformation($"OnNextConsensusMessageAsync: {item.MsgType} From: {item.From.Shorten()}");
            if (item.MsgType != ChatMessageType.NodeUp)
                OnNodeActive(item.From);

            if (item is ChatMsg chatMsg)
            {
                await OnRecvChatMsg(chatMsg);
                return;
            }

            if(null == AuthorizerShapshot)
            {
                _log.LogWarning("AuthorizerShapshot is null.");
                return;
            }

            if (AuthorizerShapshot != null && !AuthorizerShapshot.Contains(item.From))
            {
                // only allow AuthorizingMsg and ChatMsg
                if(!(item is AuthorizingMsg))
                {
                    _log.LogWarning($"Voting message source {item.From.Shorten()} not in AuthorizerShapshot.");
                    return;
                }
            }

            switch (item)
            {
                case AuthorizingMsg msg1:
                    if(msg1.Block is TransactionBlock)
                    {
                        var acctId = (msg1.Block as TransactionBlock).AccountID;
                        if (FindActiveBlock(acctId, msg1.Block.Height))
                        {
                            _log.LogCritical($"Double spent detected for {acctId}, index {msg1.Block.Height}");
                            break;
                        }
                    }

                    if (msg1.Block is ServiceBlock && !IsMessageFromSeed0(item))
                    {
                        _log.LogError($"fake genesis block from node {item.From}");
                        return;
                    }                        

                    var worker = await GetWorkerAsync(msg1.Block.Hash);
                    if (worker != null)
                        await worker.OnPrePrepareAsync(msg1);
                    else
                        _log.LogError($"No worker1 for {msg1.Block.Hash}");
                    break;
                case AuthorizedMsg msg2:
                    //_log.LogInformation($"Consensus: OnNextConsensusMessageAsync 3 {item.MsgType}");

                    if (!AuthorizerShapshot.Contains(msg2.From))
                        return;

                    var worker2 = await GetWorkerAsync(msg2.BlockHash);
                    if (worker2 != null)
                        await worker2.OnPrepareAsync(msg2);
                    else
                        _log.LogInformation($"No worker2 from {msg2.From.Shorten()} for {msg2.BlockHash.Shorten()}");
                    //_log.LogInformation($"Consensus: OnNextConsensusMessageAsync 4 {item.MsgType}");
                    break;
                case AuthorizerCommitMsg msg3:
                    if (!AuthorizerShapshot.Contains(msg3.From))
                        return;

                    var worker3 = await GetWorkerAsync(msg3.BlockHash);
                    if (worker3 != null)
                        await worker3.OnCommitAsync(msg3);
                    else
                        _log.LogInformation($"No worker3 from {msg3.From.Shorten()} for {msg3.BlockHash.Shorten()}");
                    break;
                default:
                    // log msg unknown
                    _log.LogInformation($"Message unknown from {item.From} type {item.MsgType} not processed: ");
                    break;
            }
        }

        private async Task OnRecvChatMsg(ChatMsg chat)
        {
            switch(chat.MsgType)
            {
                case ChatMessageType.HeartBeat:
                    OnHeartBeat(chat);
                    break;
                case ChatMessageType.NodeUp:
                    await Task.Run(async () => { await OnNodeUpAsync(chat); });                    
                    break;
                //case ChatMessageType.BillBoardBroadcast:
                //    OnBillBoardBroadcast(chat);
                //    break;
                //case ChatMessageType.BlockConsolidation:
                //    await OnBlockConsolicationAsync(chat);
                //    break;
                case ChatMessageType.NodeStatusInquiry:
                    var status = await _sys.TheBlockchain.Ask<NodeStatus>(new BlockChain.QueryBlockchainStatus());
                    //var status = await _sys.Storage.GetNodeStatusAsync();
                    var resp = new ChatMsg(JsonConvert.SerializeObject(status), ChatMessageType.NodeStatusReply);
                    resp.From = _sys.PosWallet.AccountId;
                    Send2P2pNetwork(resp);
                    break;
                case ChatMessageType.NodeStatusReply:
                    var statusReply = JsonConvert.DeserializeObject<NodeStatus>(chat.Text);
                    _blockchain.Tell(statusReply);
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
                    await _sys.Storage.AddBlockAsync(block);
                    _log.LogInformation($"Receive and store ConsolidateBlock of UIndex: {block.Height}");
                }
                catch(Exception e)
                {
                    _log.LogInformation($"OnBlockConsolication UIndex: {block.Height} Failed: {e.Message}");
                }                
            }
        }

        //private void OnBillBoardBroadcast(ChatMsg msg)
        //{
        //    if (IsMessageFromSeed0(msg)) // only accept bbb from seeds
        //    {
        //        _board = JsonConvert.DeserializeObject<BillBoard>(msg.Text);
        //        AuthorizerShapshot = _board.PrimaryAuthorizers.ToHashSet();

        //        // switch to protect mode if necessary
        //        _sys.TheBlockchain.Tell(new BlockChain.AuthorizerCountChanged { IsSeed0 = false, count = _board.PrimaryAuthorizers.Length });

        //        // no me?
        //        if (!_board.AllNodes.ContainsKey(_sys.PosWallet.AccountId))
        //        {
        //            Task.Run(async () => { 
        //                await DeclareConsensusNodeAsync();
        //            });
        //        }

        //        _log.LogInformation("BillBoard updated!");
        //    }
        //}

        private void RefreshAllNodesVotesAsync()
        {
            var livingPosNodeIds = _board.AllNodes.Select(a => a.AccountID);
            _lastVotes = _sys.Storage.FindVotes(livingPosNodeIds);

            foreach (var node in _board.AllNodes)
            {
                var vote = _lastVotes.FirstOrDefault(a => a.AccountId == node.AccountID);
                if (vote == null)
                    node.Votes = 0;
                else
                    node.Votes = vote.Amount;
            }
        }

        //private void BroadCastBillBoard()
        //{
        //    if (_board != null)
        //    {
        //        RefreshAllNodesVotesAsync();
        //        OnNodeActive(_sys.PosWallet.AccountId);
        //        var deadNodes = _board.AllNodes.Values.Where(a => DateTime.Now - a.LastStaking > TimeSpan.FromHours(2)).ToList();
        //        foreach (var node in deadNodes)
        //        {
        //            _log.LogInformation("Remove un-active node from billboard: " + node.AccountID);
        //            _board.AllNodes.Remove(node.AccountID);
        //        }
        //        _board.SnapShot();      // primary node list updated.
        //        AuthorizerShapshot = _board.PrimaryAuthorizers.ToHashSet();
        //        var msg = new ChatMsg(JsonConvert.SerializeObject(_board), ChatMessageType.BillBoardBroadcast);
        //        msg.From = _sys.PosWallet.AccountId;
        //        Send2P2pNetwork(msg);

        //        // switch to protect mode if necessary
        //        _sys.TheBlockchain.Tell(new BlockChain.AuthorizerCountChanged { IsSeed0 = true, count = _board.PrimaryAuthorizers.Length });
        //    }
        //}

        public void OnNodeActive(string nodeAccountId)
        {
            if (_board != null)
                _board.Refresh(nodeAccountId);
        }

        private void OnHeartBeat(ChatMsg chat)
        {
            if (_board != null)
            {
                _board.Refresh(chat.From);
            }
        }

        private async Task OnNodeUpAsync(ChatMsg chat)
        {
            _log.LogInformation($"OnNodeUpAsync: Node is up: {chat.From}");

            try
            {
                if (_board == null)
                    throw new Exception("_board is null");

                var node = chat.Text.UnJson<PosNode>();

                // verify signature
                if (string.IsNullOrWhiteSpace(node.IPAddress))
                    throw new Exception("No public IP specified.");

                if (!Signatures.VerifyAccountSignature(node.IPAddress, node.AccountID, node.Signature))
                    throw new Exception("Signature verification failed.");

                // the same node up again, not properly.
                //if (_board.AllNodes.Values.Any(a => a.IPAddress == node.IPAddress))
                //{
                //    // only allow one node per ip
                //    throw new Exception("Only allow one authorizer per IP.");
                //}

                // add network/ip verifycation here

                _ = _board.Add(node);

                if (IsMessageFromSeed0(chat))    // seed0 up
                {
                    _log.LogInformation("Seed0 is UP. Declare node again.");
                    await DeclareConsensusNodeAsync();      // we need resend node up message to codinator.
                }

                //if (IsThisNodeSeed0)
                //{
                //    // broadcast billboard
                //    _log.LogInformation("Seed0 is broadcasting billboard.");
                //    BroadCastBillBoard();
                //}

                if (_board.AllNodes.First(a => a.AccountID == node.AccountID).Votes < LyraGlobal.MinimalAuthorizerBalance)
                {
                    _log.LogInformation("Node {0} has not enough votes: {1}.", node.AccountID, node.Votes);
                }
                else
                {
                    // verify signature
                }
            }
            catch(Exception ex)
            {
                _log.LogWarning($"OnNodeUpAsync: {ex.ToString()}");
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
