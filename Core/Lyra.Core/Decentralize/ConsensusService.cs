﻿using Akka.Actor;
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
        public class BlockChainStatuChanged { public BlockChainState CurrentState {get; set;} }
        public class NodeInquiry { }

        public class ConsolidateFailed { public string consolidationBlockHash { get; set; } }
        public class Authorized { public bool IsSuccess { get; set; } }
        private readonly IActorRef _localNode;
        private readonly IActorRef _blockchain;
        private BlockChainState _currentBlockchainState { get; set; }

        ILogger _log;

        ConcurrentDictionary<string, ConsensusWorker> _activeConsensus;
        List<Vote> _lastVotes;
        private BillBoard _board;
        public bool IsViewChanging { get; private set; }
        private List<TransStats> _stats;

        public async Task<BlockChainState> GetBlockChainState() => await _blockchain.Ask<BlockChainState>(new BlockChain.QueryState());
        public bool IsThisNodeLeader => _sys.PosWallet.AccountId == Board.CurrentLeader;
        public bool IsMessageFromLeader(SourceSignedMessage msg)
        {
            return msg.From == Board.CurrentLeader;
        }
        public BillBoard Board { get => _board; }
        public List<TransStats> Stats { get => _stats; }

        private ViewChangeHandler _viewChangeHandler;

        // authorizer snapshot
        private DagSystem _sys;
        public DagSystem GetDagSystem() => _sys;
        public ConsensusService(DagSystem sys, IActorRef localNode, IActorRef blockchain)
        {
            _sys = sys;
            _localNode = localNode;
            _blockchain = blockchain;
            _log = new SimpleLogger("ConsensusService").Logger;

            _activeConsensus = new ConcurrentDictionary<string, ConsensusWorker>();
            _stats = new List<TransStats>();

            _board = new BillBoard();
            _board.CurrentLeader = ProtocolSettings.Default.StandbyValidators[0];          // default to seed0
            _board.PrimaryAuthorizers = ProtocolSettings.Default.StandbyValidators;        // default to seeds
            _board.AllVoters.AddRange(_board.PrimaryAuthorizers);                           // default to all seed nodes

            _viewChangeHandler = new ViewChangeHandler(this, (sender, leader, votes, voters) => {
                _log.LogInformation($"New leader selected: {leader} with votes {votes}");
                _board.CurrentLeader = leader;
                _board.CurrentLeadersVotes = votes;
                _board.AllVoters = voters;
                if(leader == _sys.PosWallet.AccountId)
                {
                    // its me!
                    _blockchain.Tell(new BlockChain.NewLeaderCreateView());
                }

                //_viewChangeHandler.Reset(); 
            });
            IsViewChanging = false;

            ReceiveAsync<Startup>(async state =>
            {
                // generate billboard from last service block
                var lastSvcBlk = await _sys.Storage.GetLastServiceBlockAsync();
                if (lastSvcBlk != null)
                {
                    _board.PrimaryAuthorizers = lastSvcBlk.Authorizers.Keys.ToArray();
                    if (!string.IsNullOrEmpty(lastSvcBlk.Leader))
                        _board.CurrentLeader = lastSvcBlk.Leader;
                    _board.AllVoters = _board.PrimaryAuthorizers.ToList();
                }

                await DeclareConsensusNodeAsync();
            });

            Receive<AskIfSeed0>((_) => Sender.Tell(new AskIfSeed0 { IsSeed0 = IsThisNodeLeader }));

            Receive<BlockChain.BlockAdded>(nb =>
            {
                //if (nb.NewBlock is ServiceBlock lastSvcBlk)
                //{
                //    _board.PrimaryAuthorizers = lastSvcBlk.Authorizers.Select(a => a.AccountID).ToArray();
                //    if (!string.IsNullOrEmpty(lastSvcBlk.Leader))
                //        _board.CurrentLeader = lastSvcBlk.Leader;
                //    _board.AllVoters = _board.PrimaryAuthorizers.ToList();

                //    IsViewChanging = false;
                //    _viewChangeHandler.Reset();
                //}
            });

            Receive<Consolidate>((_) =>
            {
                _log.LogInformation("Doing Consolidate");

                Task.Run(async () =>
                {
                    await CreateConsolidationBlock();
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

            ReceiveAsync<BlockChainStatuChanged>(async (state) =>
            {
                _currentBlockchainState = state.CurrentState;
            });

            ReceiveAsync<AuthState>(async state =>
            {
                if (_currentBlockchainState != BlockChainState.Almighty && _currentBlockchainState != BlockChainState.Genesis)
                {
                    state.Done?.Set();
                    return;
                }                    

                //TODO: check  || _context.Board == null || !_context.Board.CanDoConsensus
                if (state.InputMsg.Block is TransactionBlock)
                {
                    var acctId = (state.InputMsg.Block as TransactionBlock).AccountID;
                    if (FindActiveBlock(acctId, state.InputMsg.Block.Height))
                    {
                        _log.LogCritical($"Double spent detected for {acctId}, index {state.InputMsg.Block.Height}");
                        return;
                    }
                }

                await SubmitToConsensusAsync(state);
            });

            Receive<NodeInquiry>((_) => {
                var inq = new ChatMsg("", ChatMessageType.NodeStatusInquiry);
                inq.From = _sys.PosWallet.AccountId;
                Send2P2pNetwork(inq);
                //_log.LogInformation("Inquiry for node status.");
            });

            Receive<Idle>(o => { });

            ReceiveAny((o) => { _log.LogWarning($"consensus svc receive unknown msg: {o.GetType().Name}"); });

            var timr = new System.Timers.Timer(200);
            timr.Elapsed += async (s, o) =>
            {
                try
                {
                    if (_viewChangeHandler.CheckTimeout())
                    {
                        IsViewChanging = false;
                        _viewChangeHandler.Reset();

                        _log.LogWarning($"View Change Timeout. reset.");
                    }
                    foreach (var worker in _activeConsensus.Values.ToArray())
                    {
                        if (worker.CheckTimeout())
                        {
                            var result = worker.State?.CommitConsensus;

                            if (worker.State != null)
                            {
                                worker.State.Done?.Set();
                                worker.State.Close();
                            }

                            _activeConsensus.TryRemove(worker.Hash, out _);

                            if (result.HasValue && result.Value == ConsensusResult.Uncertain)
                            {
                                var blockchainStatus = await _blockchain.Ask<NodeStatus>(new BlockChain.QueryBlockchainStatus());

                                if (blockchainStatus.state == BlockChainState.Almighty && !IsViewChanging)
                                {
                                    // change view
                                    IsViewChanging = true;

                                    await _viewChangeHandler.BeginChangeViewAsync();
                                }
                            }
                        }
                    }
                }
                catch(Exception e)
                {
                    _log.LogError($"In Time keeper: {e}");
                }
            };
            timr.AutoReset = true;
            timr.Enabled = true;

            Task.Run(async () =>
            {
                int count = 0;
                while (true)
                {
                    try
                    {
                        //_log.LogWarning("starting maintaince loop... ");
                        if(_currentBlockchainState == BlockChainState.Almighty)
                            await CreateConsolidationBlock();

                        await Task.Delay(15000).ConfigureAwait(false);

                        await HeartBeatAsync();

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
            _localNode.Tell(item);
        }

        private async Task DeclareConsensusNodeAsync()
        {
            // declare to the network
            PosNode me = new PosNode(_sys.PosWallet.AccountId);
            me.IPAddress = $"{await GetPublicIPAddress.PublicIPAddressAsync(Settings.Default.LyraNode.Lyra.NetworkId)}";

            var lastSb = await _sys.Storage.GetLastServiceBlockAsync();
            var signAgainst = lastSb == null ? ProtocolSettings.Default.StandbyValidators[0] : lastSb.Hash;

            me.AuthorizerSignature = Signatures.GetSignature(_sys.PosWallet.PrivateKey,
                    signAgainst, _sys.PosWallet.AccountId);

            var msg = new ChatMsg(JsonConvert.SerializeObject(me), ChatMessageType.NodeUp);
            msg.From = _sys.PosWallet.AccountId;
            Send2P2pNetwork(msg);

            // add self to active nodes list
            await OnNodeActive(me.AccountID, me.AuthorizerSignature, BlockChainState.Startup);
        }

        private async Task OnHeartBeatAsync(HeartBeatMessage heartBeat)
        {
            await OnNodeActive(heartBeat.From, heartBeat.AuthorizerSignature, heartBeat.State);
        }
        private async Task OnNodeActive(string accountId, string authorizerSignature, BlockChainState state)
        {
            var lastSb = await _sys.Storage.GetLastServiceBlockAsync();
            var signAgainst = lastSb == null ? ProtocolSettings.Default.StandbyValidators[0] : lastSb.Hash;

            if (Signatures.VerifyAccountSignature(signAgainst, accountId, authorizerSignature))
            {
                if (_board.ActiveNodes.ToArray().Any(a => a.AccountID == accountId))
                {
                    var node = _board.ActiveNodes.First(a => a.AccountID == accountId);
                    node.LastActive = DateTime.Now;
                    node.AuthorizerSignature = authorizerSignature;
                    node.State = state;
                }
                else
                {
                    var node = new ActiveNode { 
                        AccountID = accountId, 
                        AuthorizerSignature = authorizerSignature, 
                        State = state,
                        LastActive = DateTime.Now 
                    };
                    _board.ActiveNodes.Add(node);
                }
            }
            else
            {
                // make sure ActiveNodes is clean and secured.
                _board.ActiveNodes.RemoveAll(a => a.AccountID == accountId);
            }

            _board.ActiveNodes.RemoveAll(a => a.LastActive < DateTime.Now.AddMinutes(-5));
        }

        private async Task HeartBeatAsync()
        {
            if(_board.ActiveNodes.ToArray().Any(a => a.AccountID == _sys.PosWallet.AccountId))
                _board.ActiveNodes.First(a => a.AccountID == _sys.PosWallet.AccountId).LastActive = DateTime.Now;

            var lastSb = await _sys.Storage.GetLastServiceBlockAsync();
            var signAgainst = lastSb == null ? ProtocolSettings.Default.StandbyValidators[0] : lastSb.Hash;

            // declare to the network
            var msg = new HeartBeatMessage
            {
                From = _sys.PosWallet.AccountId,
                Text = "I'm live",
                State = _currentBlockchainState,
                AuthorizerSignature = Signatures.GetSignature(_sys.PosWallet.PrivateKey, signAgainst, _sys.PosWallet.AccountId)
            };

            Send2P2pNetwork(msg);
        }

        private async Task OnNodeUpAsync(ChatMsg chat)
        {
            _log.LogInformation($"OnNodeUpAsync: Node is up: {chat.From.Shorten()}");

            try
            {
                if (_board == null)
                    throw new Exception("_board is null");

                var node = chat.Text.UnJson<PosNode>();

                // verify signature
                if (string.IsNullOrWhiteSpace(node.IPAddress))
                    throw new Exception("No public IP specified.");

                var lastSvcBlock = await _sys.Storage.GetLastServiceBlockAsync();
                var signAgainst = lastSvcBlock == null ? ProtocolSettings.Default.StandbyValidators.First() : lastSvcBlock.Hash;

                if (!Signatures.VerifyAccountSignature(signAgainst, node.AccountID, node.AuthorizerSignature))
                    throw new Exception("Signature verification failed.");

                await OnNodeActive(node.AccountID, node.AuthorizerSignature, BlockChainState.Startup);
                // add network/ip verifycation here
                // if(verifyIP)
                if (_board.NodeAddresses.ContainsKey(node.AccountID))
                    _board.NodeAddresses[node.AccountID] = node.IPAddress;
                else
                    _board.NodeAddresses.Add(node.AccountID, node.IPAddress);
                
                // if current leader is up, must resend up
                if(_board.CurrentLeader == node.AccountID)
                {
                    await DeclareConsensusNodeAsync();
                }

                //if (!IsViewChanging)
                //{
                //    var qualifiedCount = Board.AllNodes.Where(a => a.Votes >= LyraGlobal.MinimalAuthorizerBalance).Count();
                //    if (qualifiedCount > Board.PrimaryAuthorizers.Length && qualifiedCount <= LyraGlobal.MAXIMUM_AUTHORIZERS)
                //    {
                //        var blockchainStatus = await _blockchain.Ask<NodeStatus>(new BlockChain.QueryBlockchainStatus());

                //        if (blockchainStatus.state == BlockChainState.Almighty)
                //        {
                //            // change view
                //            IsViewChanging = true;

                //            await _viewChangeHandler.BeginChangeViewAsync();
                //        }
                //    }
                //}
            }
            catch (Exception ex)
            {
                _log.LogWarning($"OnNodeUpAsync: {ex.ToString()}");
            }
        }

        public bool FindActiveBlock(string accountId, long index)
        {
            return false;
            //return _activeConsensus.Values.Where(s => s.State != null)
            //    .Select(t => t.State.InputMsg.Block as TransactionBlock)
            //    .Where(x => x != null)
            //    .Any(a => a.AccountID == accountId && a.Index == index && a is SendTransferBlock);
        }

        private async Task CreateConsolidationBlock()
        {
            if (_currentBlockchainState != BlockChainState.Almighty)
                return;

            if (_activeConsensus.Values.Count > 0 && _activeConsensus.Values.Any(a => a.State?.InputMsg.Block is ConsolidationBlock))
                return;

            var lastCons = await _sys.Storage.GetLastConsolidationBlockAsync();
            if (lastCons == null)
                return;         // wait for genesis

            try
            {
                var blockchainStatus = await _blockchain.Ask<NodeStatus>(new BlockChain.QueryBlockchainStatus());
                if (IsThisNodeLeader && blockchainStatus.state == BlockChainState.Almighty)
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
                        // consolidate time from lastcons to now - 10s
                        var timeStamp = DateTime.UtcNow.AddSeconds(-10);
                        var unConsList = await _sys.Storage.GetBlockHashesByTimeRange(lastCons.TimeStamp, timeStamp);

                        if (unConsList.Count() > 10 || (unConsList.Count() > 1 && DateTime.UtcNow - lastCons.TimeStamp > TimeSpan.FromMinutes(10)))
                        {
                            try
                            {
                                await CreateConsolidateBlockAsync(lastCons, timeStamp, unConsList);
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

        //private async Task<bool> CheckPrimaryNodesStatus()
        //{
        //    if (Board.AllNodes.Count < 4)
        //        return false;

        //    var bag = new ConcurrentDictionary<string, GetSyncStateAPIResult>();
        //    var tasks = Board.AllNodes
        //        .Where(a => Board.PrimaryAuthorizers.Contains(a.AccountID))  // exclude self
        //        .Select(b => b)
        //        .Select(async node =>
        //        {
        //            var lcx = LyraRestClient.Create(Neo.Settings.Default.LyraNode.Lyra.NetworkId, Environment.OSVersion.ToString(), "Seed0", "1.0", $"http://{node.IPAddress}:4505/api/Node/");
        //            try
        //            {
        //                var syncState = await lcx.GetSyncState();
        //                bag.TryAdd(node.AccountID, syncState);
        //            }
        //            catch (Exception ex)
        //            {
        //                bag.TryAdd(node.AccountID, null);
        //            }
        //        });
        //    await Task.WhenAll(tasks);
        //    var mySyncState = bag[_sys.PosWallet.AccountId];
        //    var q = bag.Where(a => a.Key != _sys.PosWallet.AccountId)
        //        .Select(a => a.Value)
        //        .GroupBy(x => x.Status.lastUnSolidationHash)
        //        .Select(g => new { Hash = g.Key, Count = g.Count() })
        //        .OrderByDescending(x => x.Count)
        //        .First();

        //    if(mySyncState.Status.lastUnSolidationHash == q.Hash && q.Count > (int)Math.Ceiling((double)(Board.PrimaryAuthorizers.Length) * 3 / 2))
        //    {
        //        return true;
        //    }
        //    else
        //    {
        //        return false;
        //    }
        //}

        private async Task CreateConsolidateBlockAsync(ConsolidationBlock lastCons, DateTime timeStamp, IEnumerable<string> collection)
        {
            _log.LogInformation($"Creating ConsolidationBlock... ");

            var consBlock = new ConsolidationBlock
            {
                createdBy = _sys.PosWallet.AccountId,
                blockHashes = collection.ToList(),
                totalBlockCount = lastCons.totalBlockCount + collection.Count()
            };
            consBlock.TimeStamp = timeStamp;

            var mt = new MerkleTree();
            decimal feeAggregated = 0;
            foreach (var hash in consBlock.blockHashes)
            {
                mt.AppendLeaf(MerkleHash.Create(hash));

                // aggregate fees
                var transBlock = (await _sys.Storage.FindBlockByHashAsync(hash)) as TransactionBlock;
                if (transBlock != null)
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
                BlockHash = consBlock.Hash,
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

                    RefreshAllNodesVotes();
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
            if (state.InputMsg?.Block?.BlockType == BlockTypes.SendTransfer)
            {
                var tx = state.InputMsg.Block as TransactionBlock;
                var allSend = _activeConsensus.Values.Where(a => a.State?.InputMsg?.Block?.BlockType == BlockTypes.SendTransfer)
                    .Select(x => x.State.InputMsg.Block as TransactionBlock);

                var sameHeight = allSend.Any(y => y.AccountID == tx.AccountID && y.Height == tx.Height);
                var sameHash = _activeConsensus.Values.Any(a => a.State?.InputMsg.Hash == tx.Hash);
                if (sameHeight || sameHash)
                {
                    _log.LogCritical($"double spend detected: {tx.AccountID} Height: {tx.Height} Hash: {tx.Hash}");
                    return;
                }
            }

            var worker = await GetWorkerAsync(state.InputMsg.Block.Hash);
            if(worker != null)
            {
                await worker.ProcessState(state);
            }
            Send2P2pNetwork(state.InputMsg);
        }

        private async Task<ConsensusWorker> GetWorkerAsync(string hash, bool checkState = false)
        {
            // if a block is in database
            var aBlock = await _sys.Storage.FindBlockByHashAsync(hash);
            if (aBlock != null)
            {
                _log.LogWarning($"GetWorker: already in database! hash: {hash.Shorten()}");
                return null;
            }

            if(_activeConsensus.ContainsKey(hash))
                return _activeConsensus[hash];
            else
            {
                ConsensusWorker worker;
                if (checkState && _currentBlockchainState == BlockChainState.Almighty)
                    worker = new ConsensusWorker(this, hash);
                else if (checkState && _currentBlockchainState == BlockChainState.Engaging)
                    worker = new ConsensusEngagingWorker(this, hash);
                else
                    worker = new ConsensusWorker(this, hash);

                if (_activeConsensus.TryAdd(hash, worker))
                    return worker;
                else
                    return _activeConsensus[hash];
            }
        }

        async Task OnNextConsensusMessageAsync(SourceSignedMessage item)
        {
            //_log.LogInformation($"OnNextConsensusMessageAsync: {item.MsgType} From: {item.From.Shorten()}");
            if (item is ChatMsg chatMsg)
            {
                await OnRecvChatMsg(chatMsg);
                return;
            }

            if (_currentBlockchainState == BlockChainState.Genesis ||
                _currentBlockchainState == BlockChainState.Engaging ||
                _currentBlockchainState == BlockChainState.Almighty)
            {
                if (item is BlockConsensusMessage cm)
                {
                    var worker = await GetWorkerAsync(cm.BlockHash, true);
                    if (worker != null)
                        await worker.ProcessMessage(cm);
                    return;
                }

                if (_currentBlockchainState == BlockChainState.Almighty)
                {
                    if (item is ViewChangeMessage vcm)
                    {
                        //await _viewChangeHandler.ProcessMessage(vcm);
                        return;
                    }
                }
            }
        }

        private async Task OnRecvChatMsg(ChatMsg chat)
        {
            switch(chat.MsgType)
            {
                case ChatMessageType.HeartBeat:
                    await OnHeartBeatAsync(chat as HeartBeatMessage);
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

        private void RefreshAllNodesVotes()
        {
            var livingPosNodeIds = _board.ActiveNodes.Select(a => a.AccountID);
            _lastVotes = _sys.Storage.FindVotes(livingPosNodeIds);

            foreach (var node in _board.ActiveNodes.ToArray())
            {
                var vote = _lastVotes.FirstOrDefault(a => a.AccountId == node.AccountID);
                if (vote == null)
                    node.Votes = 0;
                else
                    node.Votes = vote.Amount;
            }
        }

        //public void OnNodeActive(string nodeAccountId)
        //{
        //    if (_board != null)
        //        _board.Refresh(nodeAccountId);
        //}
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
