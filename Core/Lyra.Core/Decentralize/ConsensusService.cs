﻿using Akka.Actor;
using Akka.Configuration;
using Clifton.Blockchain;
using Lyra.Core.API;
using Lyra.Core.Blocks;
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
using Settings = Neo.Settings;
using Stateless;
using Org.BouncyCastle.Utilities.Net;
using System.Net;
using Lyra.Data.API;
using Lyra.Data.Crypto;
using System.Diagnostics;
using Lyra.Data.Blocks;
using Lyra.Data.Shared;
using WorkflowCore.Interface;
using WorkflowCore.Models;
using Lyra.Core.WorkFlow;
using System.Reflection;
using WorkflowCore.Services;
using Microsoft.AspNetCore.SignalR;
using Lyra.Core.Accounts;
using System.Security.Policy;
using Lyra.Core.WorkFlow.Shared;
using Humanizer;
using Lyra.Core.Authorizers;
using System.Threading.Tasks.Dataflow;
using Akka.Util;
using Loyc.Collections;
using Lyra.Data.API.WorkFlow;

namespace Lyra.Core.Decentralize
{
    public delegate void AuthorizeCompleteEventHandler(string reqHash, bool success);
    /// <summary>
    /// pBFT Consensus
    /// </summary>
    public partial class ConsensusService : ReceiveActor
    {
        public event AuthorizeCompleteEventHandler OnBlockFinished;
        public event AuthorizeCompleteEventHandler OnWorkflowFinished;

        public class Startup { }
        public class AskForBillboard { }
        public class AskForServiceBlock { }
        public class AskForState { }
        public class AskForStats { }
        public class AskForDbStats { }
        public class AskForConsensusState { public TransactionBlock ReqBlock { get; set; } }
        public class QueryBlockchainStatus { }
        public class ReqCreatePoolFactory { }

        public class Authorized { public bool IsSuccess { get; set; } }
        private readonly IActorRef _localNode;

        private readonly StateMachine<BlockChainState, BlockChainTrigger> _stateMachine;
        private readonly StateMachine<BlockChainState, BlockChainTrigger>.TriggerWithParameters<long> _engageTriggerStart;
        private readonly StateMachine<BlockChainState, BlockChainTrigger>.TriggerWithParameters<string> _engageTriggerConsolidateFailed;
        public BlockChainState CurrentState => _stateMachine.State;
        public bool CanDoConsense => CurrentState == BlockChainState.Almighty || CurrentState == BlockChainState.Engaging || CurrentState == BlockChainState.Genesis;

        readonly ILogger _log;
        IHostEnv _hostEnv;
        readonly ConcurrentDictionary<string, DateTime> _criticalMsgCache;
        readonly ConcurrentDictionary<string, ConsensusWorker> _activeConsensus;
        readonly ConcurrentDictionary<string, LockerDTO> _lockers;
        public int LockedCount => _lockers.Count;
        public IEnumerable<string> Lockedups => _lockers.Keys;

        private List<Vote> _lastVotes;
        private readonly BillBoard _board;
        private readonly List<TransStats> _stats;
        private string _myIpAddress;
        private string _lastServiceHash;
        private ConsolidationBlock _lastCons;

        public static int DefaultAPIPort => Settings.Default.LyraNode.Lyra.NetworkId == "mainnet" ? 5504 : 4504;
        public bool IsThisNodePrimary => Board.PrimaryAuthorizers.Contains(_sys.PosWallet.AccountId);
        public bool IsThisNodeLeader => _sys.PosWallet.AccountId == Board.CurrentLeader;
        public bool IsThisNodeSeed => ProtocolSettings.Default.StandbyValidators.Contains(_sys.PosWallet.AccountId);

        public BillBoard Board { get => _board; }
        //private ConcurrentDictionary<string, DateTime> _verifiedIP;   // ip verify by access public api port. valid for 24 hours.
        private readonly ConcurrentDictionary<string, DateTime> _failedLeaders; // when a leader fail, add it. expire after 1 hour.
        public List<TransStats> Stats { get => _stats; }

        private readonly ViewChangeHandler _viewChangeHandler;

        // how many suscess consensus did since started.
        private int _successBlockCount;

        // authorizer snapshot
        private readonly DagSystem _sys;
        public DagSystem GetDagSystem() => _sys;
        public AuthorizersFactory AF => _af;
        private AuthorizersFactory _af;
        private BrokerFactory _bf;
        private long _currentView;
        private SemaphoreSlim _pfTaskMutex = new SemaphoreSlim(1);

        private DateTime _lastConsolidateTry;

        public void SetHostEnv(IHostEnv env) { _hostEnv = env; }
        //ConcurrentDictionary<string, string> _workFlows;

        //private readonly IHubContext<LyraEventHub, ILyraEvent> _hubContext;

        public static ConsensusService Singleton { get; private set; }

        public bool CheckIfIdIsLocked(string id) => _activeConsensus.Values.Any(a => a.LocalAuthResult != null && (a.LocalAuthResult?.LockedIDs?.Contains(id) ?? false));

        ActionBlock<(SendTransferBlock, ConsensusResult?)> SendReqs;
        ActionBlock<(TransactionBlock, ConsensusResult?)> MgmtReqs;

        public ConsensusService(DagSystem sys, IHostEnv hostEnv, /*IHubContext<LyraEventHub, ILyraEvent> hubContext, */IActorRef localNode, IActorRef blockchain)
        {
            _sys = sys;
            _currentView = sys.Storage.GetCurrentView();
            _hostEnv = hostEnv;
            //_hubContext = hubContext;
            _localNode = localNode;
            //_blockchain = blockchain;
            _log = new SimpleLogger("ConsensusService").Logger;
            _successBlockCount = 0;
            _lastConsolidateTry = DateTime.UtcNow;

            _criticalMsgCache = new ConcurrentDictionary<string, DateTime>();
            _activeConsensus = new ConcurrentDictionary<string, ConsensusWorker>();
            _lockers = new ConcurrentDictionary<string, LockerDTO>();
            _stats = new List<TransStats>();

            _board = new BillBoard();

            //_workFlows = new ConcurrentDictionary<string, string>();
            _failedLeaders = new ConcurrentDictionary<string, DateTime>();

            if (localNode == null)
            {
                Board.CurrentLeader = _sys.PosWallet.AccountId;
                return;         // for unit test
            }

            TestSharedInit();

            if (Neo.Settings.Default.LyraNode.Lyra.Mode == Data.Utils.NodeMode.Normal)
            {
                _viewChangeHandler = new ViewChangeHandler(_sys, this,
                    (leaderCandidate) =>
                    {
                        _board.LeaderCandidate = leaderCandidate;
                    },
                    (sender, viewId, leader, votes, voters) =>
                {
                    _log.LogInformation($"New leader selected: {leader} with votes {votes}");
                    _board.LeaderCandidate = leader;
                    _board.LeaderCandidateVotes = votes;

                    if (leader == _sys.PosWallet.AccountId)
                    {
                        // its me!
                        if (!_creatingSvcBlock)
                        {
                            _creatingSvcBlock = true;

                            _log.LogInformation($"Me was elected new leader. Creating New View...");

                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    var svcBlock = await CreateNewViewAsNewLeaderAsync();

                                    _log.LogInformation($"New View was created. send to network...");
                                    await LeaderSendBlockToConsensusAndForgetAsync(svcBlock);
                                    //var result = await SendBlockToConsensusAndWaitResultAsync(svcBlock);
                                }
                                catch (Exception e)
                                {
                                    _log.LogCritical($"CreateNewViewAsNewLeader: {e}");
                                }
                                finally
                                {
                                    // for unit test only, save 10 seconds
                                    if (_hostEnv != null)
                                        await Task.Delay(10000);
                                    _creatingSvcBlock = false;
                                }
                            }).ConfigureAwait(false);
                        }
                    }

                    //_ = Task.Run(async () =>
                    //{
                    //    await Task.Delay(10000);

                    //    _board.LeaderCandidate = null;

                    //    var sb = await _sys.Storage.GetLastServiceBlockAsync();
                    //    if (sb.Height < viewId)
                    //    {
                    //        _log.LogCritical($"The new leader {leader.Shorten()} failed to generate service block. {sb.Height} vs {viewId} redo election.");
                    //        // the new leader failed.

                    //        // limit the count of failed leader to 4.
                    //        // so we can avoid fatal error like blockchain fork.

                    //        if (_failedLeaders.Count >= 4)
                    //        {
                    //            var kvp = _failedLeaders.OrderBy(x => x.Value).First();
                    //            _failedLeaders.TryRemove(kvp.Key, out _);
                    //        }

                    //        // never add seeds
                    //        if (!ProtocolSettings.Default.StandbyValidators.Contains(leader))
                    //            _failedLeaders.AddOrUpdate(leader, DateTime.UtcNow, (k, v) => v = DateTime.UtcNow);

                    //        if (CurrentState == BlockChainState.Almighty || CurrentState == BlockChainState.Engaging)
                    //        {
                    //            // redo view change
                    //            _viewChangeHandler.BeginChangeView(false, $"The new leader {leader.Shorten()} failed to generate service block.");
                    //        }
                    //    }
                    //});
                    //_viewChangeHandler.Reset(); // wait for svc block generated
                });
            }

            Receive<Startup>(state =>
            {
                if(_hostEnv != null) // unit test
                    _stateMachine.Fire(BlockChainTrigger.LocalNodeStartup);
            });

            ReceiveAsync<QueryBlockchainStatus>(async _ =>
            {
                var status = await GetNodeStatusAsync();
                Sender.Tell(status);
            });

            Receive<AskForState>((_) => Sender.Tell(_stateMachine.State));
            Receive<AskForBillboard>((_) => { Sender.Tell(_board); });
            ReceiveAsync<AskForServiceBlock>(async (_) => { Sender.Tell(await CreateNewViewAsNewLeaderAsync()); });
            Receive<AskForStats>((_) => Sender.Tell(_stats));
            Receive<AskForDbStats>((_) => Sender.Tell(PrintProfileInfo()));

            // consensus, replace authstate
            ReceiveAsync<AuthState>(async (state) =>
            {
                await SubmitToConsensusAsync(state);
            });
            ReceiveAsync<AskForConsensusState>(async (askReq) =>
            {
                try
                {
                    AuthorizingMsg msg = new AuthorizingMsg
                    {
                        From = _sys.PosWallet.AccountId,
                        Block = askReq.ReqBlock,
                        BlockHash = askReq.ReqBlock?.Hash,
                        MsgType = ChatMessageType.AuthorizerPrePrepare
                    };

                    var statex = await CreateAuthringStateAsync(msg, true);
                    Sender.Tell(statex);
                }
                catch (Exception ex)
                {
                    _log.LogError("When reply AskForConsensusState: " + ex.ToString());
                }
            });

            ReceiveAsync<ReqCreatePoolFactory>(async (_) =>
                {
                    try
                    {
                        await CreatePoolFactoryAsync();
                    }
                    catch (Exception ex)
                    {
                        _log.LogError("Error in CreatePoolFactoryAsync: " + ex.ToString());
                    }
                }
            );

            ReceiveAsync<LocalNode.SignedMessageRelay>(async relayMsg =>
            {
                try
                {
                    var signedMsg = relayMsg.signedMessage;

                    //_log.LogInformation($"SignedMessageRelay {signedMsg.Signature.Shorten()} {signedMsg.MsgType} from {signedMsg.From.Shorten()} Hash {(signedMsg as BlockConsensusMessage)?.BlockHash}");
                    
                    if(signedMsg.TimeStamp < DateTime.UtcNow.AddSeconds(3) &&
                        signedMsg.TimeStamp > DateTime.UtcNow.AddSeconds(-30))
                    {
                        if (signedMsg.VerifySignature(signedMsg.From))
                        {
                            // test if this is really needed.
                            //if(IsThisNodeSeed)
                            //{
                            //    await CriticalRelayAsync(signedMsg, null);
                            //}                            

                            await OnNextConsensusMessageAsync(signedMsg);

                            //await CriticalRelayAsync(signedMsg, async (msg) =>
                            //{
                            //    await OnNextConsensusMessageAsync(msg);
                            //});

                            /*                            BlockTypes bt = BlockTypes.Null;
                                                        if (signedMsg is AuthorizingMsg au)
                                                        {
                                                            bt = au.Block.BlockType;
                                                        }
                                                        else if (signedMsg is BlockConsensusMessage bcm)
                                                        {
                                                            if (_activeConsensus.ContainsKey(bcm.BlockHash))
                                                            {
                                                                var bx = _activeConsensus[bcm.BlockHash];
                                                                if (bx.State != null && bx.State.InputMsg != null)
                                                                    bt = bx.State.InputMsg.Block.BlockType;
                                                            }
                                                        }*/

                            //// not needed anymore
                            //// seeds take resp to forward heatbeat, once
                            //if ((IsThisNodeSeed && (
                            //    signedMsg.MsgType == ChatMessageType.HeartBeat
                            //    //|| bt == BlockTypes.Consolidation
                            //    //|| bt == BlockTypes.Service
                            //    //|| (signedMsg is AuthorizingMsg au && (au.Block is ConsolidationBlock || au.Block is ServiceBlock))
                            //    //|| (signedMsg is AuthorizingMsg au && (au.Block is ConsolidationBlock || au.Block is ServiceBlock))
                            //    //|| signedMsg.MsgType == ChatMessageType.ViewChangeRequest
                            //    //|| signedMsg.MsgType == ChatMessageType.ViewChangeReply
                            //    //|| signedMsg.MsgType == ChatMessageType.ViewChangeCommit
                            //    )) || CurrentState == BlockChainState.Genesis)
                            //{
                            //    await CriticalRelayAsync(signedMsg, null);
                            //}
                        }
                        else
                        {
                            _log.LogWarning($"Receive Relay illegal type {signedMsg.MsgType} Delayed {(DateTime.UtcNow - signedMsg.TimeStamp).TotalSeconds}s Verify: {signedMsg.VerifySignature(signedMsg.From)} From: {signedMsg.From.Shorten()}");
                            //if (signedMsg.MsgType == ChatMessageType.AuthorizerPrePrepare)
                            //{
                            //    var json = JsonConvert.SerializeObject(signedMsg);
                            //    _log.LogInformation("===\n" + json + "\n===");

                            //    var jb = JsonConvert.SerializeObject(signedMsg);
                            //}
                        }
                    }
                    else
                        _log.LogInformation($"SignedMessageRelay {signedMsg.Signature.Shorten()} exited.");
                }
                catch (Exception ex)
                {
                    _log.LogCritical("Error Receive Relay!!! " + ex.ToString());
                }
            });

            Receive<Idle>(o => { });

            ReceiveAny((o) => { _log.LogWarning($"consensus svc receive unknown msg: {o.GetType().Name}"); });

            _stateMachine = new StateMachine<BlockChainState, BlockChainTrigger>(BlockChainState.NULL);
            _engageTriggerStart = _stateMachine.SetTriggerParameters<long>(BlockChainTrigger.ConsensusNodesInitSynced);
            _engageTriggerConsolidateFailed = _stateMachine.SetTriggerParameters<string>(BlockChainTrigger.LocalNodeOutOfSync);
            if(_hostEnv != null)    // to support unittest
                CreateStateMachine();

            var timr = new System.Timers.Timer(200);
            if (_hostEnv != null)
            {
                timr.Elapsed += (s, o) =>
                {
                    try
                    {
                        // clean critical msg forward table
                        var oldList = _criticalMsgCache.Where(a => a.Value < DateTime.Now.AddSeconds(-60))
                                .Select(b => b.Key);

                        foreach (var hb in oldList)
                        {
                            _criticalMsgCache.TryRemove(hb, out _);
                        }
                    }
                    catch (Exception e)
                    {
                        _log.LogError($"In Time keeper: {e}");
                    }
                };
                timr.AutoReset = true;
                timr.Enabled = true;
            }

            Singleton = this;
            // for unit test
            if (Settings.Default.LyraNode.Lyra.NetworkId == "xtest")
                _localNode = null;
        }

        public void TestSharedInit()
        {
            _af = new AuthorizersFactory();
            _af.Init();
            _bf = new BrokerFactory();
            _bf.Init(_af, _sys.Storage);

            SendReqs = new ActionBlock<(SendTransferBlock, ConsensusResult?)>(
                async ConsensusResult =>
                {
                    await ProcessServerReqBlockAsync(ConsensusResult.Item1, ConsensusResult.Item2);
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 8
                }
                );

            MgmtReqs = new ActionBlock<(TransactionBlock, ConsensusResult?)>(
                async ConsensusResult =>
                {
                    await ProcessManagedBlockAsync(ConsensusResult.Item1, ConsensusResult.Item2);
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 8
                }
                );
        }

        public async Task BeginChangeViewAsync(string sender, ViewChangeReason reason)
        {
            _log.LogWarning($"Get View Change Request from {sender} for Reason: {reason}");

            if (_viewChangeHandler == null)
            {
                _log.LogInformation("Can't change view because _viewChangeHandler is null");
                return;
            }

            if (CurrentState == BlockChainState.Engaging || CurrentState == BlockChainState.Almighty)
            {
                await DeclareConsensusNodeAsync();
                await _viewChangeHandler.BeginChangeViewAsync(reason);
            }
            else
                _log.LogWarning($"GotViewChangeRequest for CurrentState: {CurrentState}");
        }

        internal async Task GotViewChangeRequestAsync(long viewId, int requestCount)
        {
            // temp disable
            // stop endless view change
            // let view change request only from consensus service.

            _log.LogWarning($"GotViewChangeRequest from other nodes for {viewId} count {requestCount}");
            await BeginChangeViewAsync("consensus network", ViewChangeReason.TooManyViewChangeRquests);
        }

        private ILyraAPI CreateSafeClient()
        {
            //// temp speedup all network. will test sync by all nodes later.
            //if ("testnet" == Settings.Default.LyraNode.Lyra.NetworkId)
            //{
            //    var client1 = new LyraRestClient("", "", "", $"https:///api/Node/");
            //    return client1;
            //}

            var useSeedOnly = true;
            var client = new LyraAggregatedClient(Settings.Default.LyraNode.Lyra.NetworkId, useSeedOnly, _sys.PosWallet.AccountId, Board);
            return client;
        }

        private async Task<ILyraAPI> CreateFastClientAsync()
        {
            // use random primary node to get block data.
            // because block can be verified, so it's safe.
            var rand = new Random();

            while(true)
            {
                var ep = _board.NodeAddresses.Where(a => _board.PrimaryAuthorizers.Contains(a.Key))
                    .Select(a => a.Value)
                    .Where(a => a != _myIpAddress)
                    .OrderBy(a => rand.Next())
                    .FirstOrDefault();

                if(ep == null)
                {
                    _log.LogInformation($"Can't find a primary node from billboard. retrying... ");
                    await Task.Delay(5000);
                    continue;
                }

                int port = 4504;
                if (Settings.Default.LyraNode.Lyra.NetworkId.Equals("mainnet", StringComparison.InvariantCultureIgnoreCase))
                    port = 5504;
                var addr = ep.Contains(':') ? ep : $"{ep}:{port}";

                _log.LogInformation($"CreateFastClient uses {addr}");
                var client1 = new LyraRestClient("", "", "", $"https://{addr}/api/Node/");
                return client1;
            }

        }

        private bool InDBCC = false;
        private async Task<bool> DBCCAsync()
        {
            if (InDBCC)
                return true;

            InDBCC = true;
            try
            {
                //await ((_sys.Storage as TracedStorage).Store as MongoAccountCollection).FixDbRecordAsync();

                var blcokcount = await _sys.Storage.GetBlockCountAsync();
                if(blcokcount == 0) //genesis
                {
                    return true;
                }

                _log.LogInformation($"Database consistent check... It may take a while.");

                var client = CreateSafeClient();
                var fastClient = await CreateFastClientAsync();

                var lastCons = await _sys.Storage.GetLastConsolidationBlockAsync();
                var fixedHeight = lastCons?.Height ?? 0;
                var shouldReset = false;

                // roll back a little to make sure 
                var localSafeCons = LocalDbSyncState.Load().lastVerifiedConsHeight - 30;
                for (long i = lastCons == null ? 0 : lastCons.Height; i >= localSafeCons; i--)
                {
                    bool missingBlock = false;

                    if (lastCons != null)
                    {
                        for (int k = i == 1 ? 0 : 1; k < lastCons.blockHashes.Count; k++)
                        {
                            var b = await _sys.Storage.FindBlockByHashAsync(lastCons.blockHashes[k]);
                            if (b == null)
                            {
                                _log.LogCritical($"DBCC: missing block: {lastCons.blockHashes[k]}");
                                missingBlock = true;

                                var vState = LocalDbSyncState.Load();
                                vState.lastVerifiedConsHeight = lastCons.Height - 1;
                                LocalDbSyncState.Save(vState);
                            }
                        }
                    }

                    ConsolidationBlock nextCons = null;
                    if (i > 1)
                    {
                        nextCons = await _sys.Storage.FindBlockByHashAsync(lastCons.blockHashes[0]) as ConsolidationBlock;
                        if (nextCons == null)
                        {
                            _log.LogCritical($"DBCC: missing consolidation block: {lastCons.Height - 1} {lastCons.blockHashes[0]}");
                            missingBlock = true;
                        }
                        else
                        {
                            var allBlocksInTimeRange = await _sys.Storage.GetBlockHashesByTimeRangeAsync(nextCons.TimeStamp, lastCons.TimeStamp);
                            var extras = allBlocksInTimeRange.Where(a => !lastCons.blockHashes.Contains(a));
                            if (extras.Any())
                            {
                                _log.LogCritical($"Found extra blocks in cons range {nextCons.Height} to {lastCons.Height}");

                                foreach (var extraHash in extras)
                                {
                                    _log.LogCritical($"Found extra block {extraHash} in cons range {nextCons.Height} to {lastCons.Height}");
                                    await _sys.Storage.RemoveBlockAsync(extraHash);
                                    _log.LogInformation("Extra block removed.");
                                }
                                i++;
                                continue;
                            }
                        }
                    }

                    if (missingBlock)
                    {
                        _log.LogInformation($"DBCC: Fixing database...");
                        //var consSyncResult = await SyncAndVerifyConsolidationBlockAsync(client, fastClient, lastCons);
                        var consSyncResult = await SyncDatabaseAsync(client, 3);
                        if (consSyncResult)
                            i++;
                        else
                        {
                            // reset aggregated client
                            shouldReset = true;
                            break;
                        }
                    }
                    else
                    {
                        if (i == 1)
                            break;

                        lastCons = nextCons;
                    }
                }

                if (shouldReset)
                {
                    _log.LogInformation($"Database consistent check has problem syncing database. Redo... ");
                    return false;
                }

                _log.LogInformation($"Database consistent check is done.");
                var localState = LocalDbSyncState.Load();
                if (string.IsNullOrEmpty(localState.svcGenHash) || localState.lastVerifiedConsHeight == 0)
                {
                    localState.databaseVersion = LyraGlobal.DatabaseVersion;
                    var svcGen = await _sys.Storage.GetServiceGenesisBlockAsync();
                    localState.svcGenHash = svcGen.Hash;
                }
                localState.lastVerifiedConsHeight = fixedHeight;
                LocalDbSyncState.Save(localState);

                return true;
            }
            finally
            {
                InDBCC = false;
            }
        }

        private void CreateStateMachine()
        {
            _stateMachine.Configure(BlockChainState.NULL)
                .Permit(BlockChainTrigger.LocalNodeStartup, BlockChainState.Initializing);

            _ = _stateMachine.Configure(BlockChainState.Initializing)
#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
                .OnEntry(async () =>
                {
                    _log.LogInformation("while true in BlockChainState.Initializing OnEntry");
                    // remove sync state if db is empty
                    var count = await _sys.Storage.GetBlockCountAsync();
                    if (0 == count)
                    {
                        LocalDbSyncState.Remove();
                    }

                    if (Neo.Settings.Default.LyraNode.Lyra.Mode == Data.Utils.NodeMode.Normal)
                        await DeclareConsensusNodeAsync();

                    await InitJobSchedulerAsync();

                    do
                    {
                        try
                        {
                            _log.LogInformation($"Consensus Service Startup... ");
                            
                            await DeclareConsensusNodeAsync();  // important for cold start

                            _lastCons = await _sys.Storage.GetLastConsolidationBlockAsync();
                            var lsb = await _sys.Storage.GetLastServiceBlockAsync();
                            if (lsb == null)
                            {
                                _board.CurrentLeader = ProtocolSettings.Default.StandbyValidators[0];          // default to seed0
                                _board.UpdatePrimary(ProtocolSettings.Default.StandbyValidators.ToList());        // default to seeds
                                _board.AllVoters = _board.PrimaryAuthorizers;                           // default to all seed nodes
                            }
                            else
                            {
                                _board.CurrentLeader = lsb.Leader;
                                _board.UpdatePrimary(lsb.Authorizers.Keys.ToList());
                                _board.AllVoters = _board.PrimaryAuthorizers;

                                _lastServiceHash = lsb.Hash;
                            }

                            // DBCC
                            if (!await DBCCAsync())
                                continue;

                            while (true)
                            {
                                _log.LogInformation("while true after dbcc");
                                try
                                {
                                    var client = CreateSafeClient();

                                    var result = await client.GetLastServiceBlockAsync();
                                    if (!result.Successful())
                                        _log.LogWarning($"Can't get service block for dbcc: {result.ResultCode}");

                                    if (result.ResultCode == APIResultCodes.Success)
                                    {
                                        lsb = result.GetBlock() as ServiceBlock;
                                    }
                                    else if (result.ResultCode == APIResultCodes.APIRouteFailed)
                                    {
                                        //client.ReBase(true);
                                        //await client.InitAsync();
                                        break;
                                    }
                                    else if (result.ResultCode != APIResultCodes.ServiceBlockNotFound)
                                    {
                                        await Task.Delay(2000);
                                        continue;
                                    }

                                    if (lsb == null)
                                    {
                                        _board.CurrentLeader = ProtocolSettings.Default.StandbyValidators[0];          // default to seed0
                                        _board.UpdatePrimary(ProtocolSettings.Default.StandbyValidators.ToList());        // default to seeds
                                        _board.AllVoters = _board.PrimaryAuthorizers;                           // default to all seed nodes
                                    }
                                    else
                                    {
                                        _board.CurrentLeader = lsb.Leader;
                                        _board.UpdatePrimary(lsb.Authorizers.Keys.ToList());
                                        _board.AllVoters = _board.PrimaryAuthorizers;
                                    }

                                    break;
                                }
                                catch (Exception ex)
                                {
                                    _log.LogInformation($"Consensus Service Startup Exception: {ex}");
                                }
                                finally
                                {
                                    _log.LogInformation("In finally init sync");
                                    await Task.Delay(10000);
                                }
                            }

                            // swith mode
                            _stateMachine.Fire(BlockChainTrigger.DatabaseSync);
                            break;
                        }
                        catch (Exception ex)
                        {
                            _log.LogError("Error In startup: " + ex.ToString());
                            await Task.Delay(10000);
                        }
                        finally
                        {
                            _log.LogInformation("In finally state machine init");
                            await Task.Delay(2000);
                        }
                    } while (true);
                })
#pragma warning restore VSTHRD101 // Avoid unsupported async delegates
                .Permit(BlockChainTrigger.DatabaseSync, BlockChainState.StaticSync);

            _stateMachine.Configure(BlockChainState.StaticSync)
                .OnEntry(() => Task.Run(async () =>
                {
                    while (true)
                    {
                        _log.LogInformation("While true in BlockChainState.StaticSync Entry");
                        try
                        {
                            var n = new Random().Next(1, 4).ToString();
                            var host = $"seed{n}.{Settings.Default.LyraNode.Lyra.NetworkId}.lyra.live";
                            var seedhost = $"{host}:{DefaultAPIPort}";

                            // remember to set endpoint to seeds.

                            if (seedhost == _myIpAddress)
                            {
                                // self
                                await Task.Delay(1000);
                                continue;
                            }

                            //var client = new LyraRestClient("", "", "", $"https://{seedhost}/api/Node/");
                            var client = CreateSafeClient();

                            // when static sync, only query the seed nodes.
                            // three seeds are enough for database sync.
                            _log.LogInformation($"Querying Lyra Network Status... ");

                            var networkStatus = await client.GetSyncStateAsync();

                            if (networkStatus.ResultCode == APIResultCodes.APIRouteFailed)
                            {
                                _log.LogInformation($"Invalid Sync State: {networkStatus.ResultMessage}");
                                //if(client is LyraAggregatedClient agg)
                                //{
                                //    agg.ReBase(true);   // look for seeds
                                //    networkStatus = await client.GetSyncStateAsync();
                                //}
                            }

                            if (networkStatus.ResultCode != APIResultCodes.Success)
                            {
                                _log.LogInformation($"Unexpected network state: {networkStatus.ResultCode}");
                                await Task.Delay(2000);
                                continue;
                            }

                            var majorHeight = networkStatus.Status.totalBlockCount;
                            _log.LogInformation($"Consensus network: Major Height = {majorHeight}");

                            var myStatus = await GetNodeStatusAsync();

                            if (myStatus.totalBlockCount == 0 && majorHeight == 0)
                            {
                                //_stateMachine.Fire(_engageTriggerStartupSync, majorHeight.Height);
                                await _stateMachine.FireAsync(BlockChainTrigger.ConsensusBlockChainEmpty);
                                break;
                            }
                            else if (majorHeight >= 2)
                            {
                                // if local == remote then no need for database sync
                                if (majorHeight != myStatus.totalBlockCount)
                                {
                                    _log.LogInformation($"local height {myStatus.totalBlockCount} not equal to majority {majorHeight}, do database sync.");
                                    // verify local database
                                    while (!await SyncDatabaseAsync(client, 3))
                                    {
                                        //fatal error. should not run program
                                        _log.LogCritical($"Unable to sync blockchain database. Will retry in 1 minute.");
                                        await Task.Delay(60000);
                                    }
                                }

                                _stateMachine.Fire(_engageTriggerStart, majorHeight);
                                break;
                            }
                            else
                            {
                                _log.LogInformation($"Unable to get Lyra network status.");
                            }
                        }
                        catch (Exception ex)
                        {
                            _log.LogCritical("In BlockChainState.Startup: " + ex.ToString());
                            await Task.Delay(5000);
                        }
                        finally
                        {
                            _log.LogInformation("In finally static sync");
                            await Task.Delay(2000);
                        }
                    }
                }).ConfigureAwait(false))
                .PermitReentry(BlockChainTrigger.QueryingConsensusNode)
                .Permit(BlockChainTrigger.ConsensusBlockChainEmpty, BlockChainState.Genesis)
                .Permit(BlockChainTrigger.ConsensusNodesInitSynced, BlockChainState.Engaging);

            _stateMachine.Configure(BlockChainState.Genesis)
                .OnEntry(() => Task.Run(async () =>
                {
                    LocalDbSyncState.Remove();

                    StartWorkflowEngine();  // because genesis goes directly into almighty, so no chance to init workflow engine.

                    // reset bill board contents related to leader
                    Board.LeaderCandidate = ProtocolSettings.Default.StandbyValidators[0];
                    Board.LeaderCandidateVotes = 4;

                    var IsSeed0 = _sys.PosWallet.AccountId == ProtocolSettings.Default.StandbyValidators[0];
                    if (await _sys.Storage.FindLatestBlockAsync() == null && IsSeed0)
                    {
                        // check if other seeds is ready
                        do
                        {
                            _log.LogInformation("Check if other nodes are in genesis mode.");
                            await Task.Delay(3000);
                        } while (_board.ActiveNodes
                            .Where(a => _board.PrimaryAuthorizers.Contains(a.AccountID))
                            .Where(a => a.State == BlockChainState.Genesis)
                            .Count() < 4);

                        await GenesisAsync();
                    }
                    else
                    {
                        // wait for genesis to finished.
                        await Task.Delay(360000);
                    }
                }).ConfigureAwait(false))
                .Permit(BlockChainTrigger.GenesisDone, BlockChainState.Almighty);

            _stateMachine.Configure(BlockChainState.Engaging)
                .OnEntry(() =>
                    {
                        StartWorkflowEngine();

                        _ = Task.Run(async () =>
                        {
                            try
                            {                               
                                while (true)
                                {
                                    _log.LogInformation("While true in BlockChainState.Engaging Task.Run");

                                    await SetupCurrentView();

                                    await EngagingSyncAsync();

                                    // check block count
                                    // if total block count is not consistant according to majority of nodes, there must be a damage.
                                    var client = CreateSafeClient();

                                    var syncstate = await client.GetSyncStateAsync();
                                    var mysyncstate = await GetNodeStatusAsync();
                                    if(syncstate.Successful())
                                    {
                                        if (syncstate.Status.lastConsolidationHash == mysyncstate.lastConsolidationHash
                                                && syncstate.Status.lastUnSolidationHash == mysyncstate.lastUnSolidationHash)
                                        {
                                            _log.LogInformation("Fully synced.");
                                            break;
                                        }                                            
                                        else
                                            _log.LogWarning($"Count from network: {syncstate.Status.totalBlockCount}, count of mine: {mysyncstate.totalBlockCount}");
                                    }
                                    else
                                    {
                                        _log.LogWarning($"Can't get status from network: {syncstate.ResultCode}");
                                    }

                                    _log.LogWarning("Can't make sure database consistence. forced to do a full DBCC.");
                                    // reset the dbcc counter
                                    var localState = LocalDbSyncState.Load();
                                    localState.lastVerifiedConsHeight -= 15;
                                    LocalDbSyncState.Save(localState);

                                    await DBCCAsync();
                                    await Task.Delay(10000);
                                }
                            }
                            catch (Exception e)
                            {
                                _log.LogError($"In Engaging: {e}");
                            }
                            finally
                            {
                                await _stateMachine.FireAsync(BlockChainTrigger.LocalNodeFullySynced);
                            }
                        }).ConfigureAwait(false);
                    })
                .Permit(BlockChainTrigger.LocalNodeFullySynced, BlockChainState.Almighty);

            _stateMachine.Configure(BlockChainState.Almighty)
                .OnEntry(() => Task.Run(async () =>
                {
                    await SetupCurrentView();
                }).ConfigureAwait(false))
                .Permit(BlockChainTrigger.LocalNodeOutOfSync, BlockChainState.Engaging)         // make a quick recovery
                .Permit(BlockChainTrigger.LocalNodeMissingBlock, BlockChainState.Engaging);

            _stateMachine.OnTransitioned(t =>
            {
                _sys.UpdateConsensusState(t.Destination);
                _log.LogWarning($"OnTransitioned: {t.Source} -> {t.Destination} via {t.Trigger}({string.Join(", ", t.Parameters)})");
            });
        }

        private async Task SetupCurrentView()
        {
            _lastCons = await _sys.Storage.GetLastConsolidationBlockAsync();
            var lsb = await _sys.Storage.GetLastServiceBlockAsync();
            _lastServiceHash = lsb.Hash;
            _viewChangeHandler.ShiftView(lsb.Height + 1);
        }

        public IWorkflowHost StartWorkflowEngine()
        {
            var host = _hostEnv.GetWorkflowHost();

            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo finfo = typeof(WorkflowHost).GetField("_shutdown", bindingFlags);
            bool shutdown = (bool)finfo.GetValue(host);
            if (shutdown)
            {
                if(Settings.Default.LyraNode.Lyra.NetworkId != "xtest")
                {
                    foreach (var type in BrokerFactory.DynWorkFlows.Values.Select(a => a.GetType()))
                    {
                        var methodInfo = typeof(WorkflowHost).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                            .Where(a => a.Name == "RegisterWorkflow")
                            .Last();

                        var genericMethodInfo = methodInfo.MakeGenericMethod(type, typeof(LyraContext));

                        genericMethodInfo.Invoke(host, new object[] { });
                    }
                }

                _log.LogInformation("Start workflow host.Start()");
                host.OnStepError += Host_OnStepError;
                host.OnLifeCycleEvent += Host_OnLifeCycleEvent;
                host.Start();
            }

            return host;
        }



        //var producer = new ActionBlock<string>(async s =>
        //{
        //    foreach (char c in s)
        //    {
        //        await consumer.SendAsync(c);

        //        Debug.Print($"Yielded {c}");
        //    }
        //});

        // set to public is for unit test. better solution later.
        public void Host_OnLifeCycleEvent(WorkflowCore.Models.LifeCycleEvents.LifeCycleEvent evt)
        {
            //_log.LogInformation($"Workflow Event: {evt.WorkflowDefinitionId} Reference {evt.Reference}");

            // evt: instant id is guid, defination id is req tag
            var lkdto = _lockers.Values.FirstOrDefault(a => a.workflowid == evt.WorkflowInstanceId);
            if(lkdto != null )
            {
                if (evt.Reference == "Exited")
                {
                    _log.LogInformation($"Workflow {lkdto.reqhash} created {lkdto.seqhashes.Count} blocks state {evt.Reference} is terminated. ");
                    RemoveLockerDTO(lkdto.reqhash);

                    OnWorkflowFinished?.Invoke(lkdto.reqhash, true);
                }

                //await FireSignalrWorkflowEventAsync(new WorkflowEvent
                //{
                //    Owner = ctx.OwnerAccountId,
                //    State = Message == "Workflow is done." ? "Exited" : ctx.State.ToString(),
                //    Name = ctx.SvcRequest,
                //    Key = ctx.SendHash,
                //    Action = ctx.LastBlockType.ToString(),
                //    Result = ctx.LastResult.ToString(),
                //    Message = Message,
                //});
            }

            //lock (lifeo)
            //{
            //    //_log.LogInformation($"Life: {evt.WorkflowInstanceId}: {evt.Reference}");
            //    if (evt.Reference == "end")
            //    {
            //        if (!_endedWorkflows.Contains(evt.WorkflowInstanceId))
            //        {
            //            _endedWorkflows.Add(evt.WorkflowInstanceId);
            //            var hash = GetHashForWorkflow(evt.WorkflowInstanceId);
            //            _log.LogInformation($"Key is {hash} terminated. Set it.");
            //        }
            //    }
            //}
        }

        public void Host_OnStepError(WorkflowInstance workflow, WorkflowStep step, Exception exception)
        {
            _log.LogError($"Workflow Host Error: {workflow.Id} {step.Name} {exception}");
            Console.WriteLine($"Workflow Host Error: {workflow.Id} {step.Name} {exception}");

            var lkdto = _lockers.Values.FirstOrDefault(a => a.workflowid == workflow.Id);
            if (lkdto != null)
            {
                RemoveLockerDTO(lkdto.reqhash);
                OnWorkflowFinished?.Invoke(lkdto.reqhash, false);                
            }
        }

        public static int GetQualifiedNodeCount()
        {
            return Settings.Default.LyraNode.Lyra.NetworkId switch
            {
                "mainnet" => 19,
                "testnet" => 19,
                "devnet" => 4,
                _ => int.MaxValue,
            };

            //var count = allNodes.Count();
            //if (count > LyraGlobal.MAXIMUM_VOTER_NODES)
            //{
            //    return LyraGlobal.MAXIMUM_VOTER_NODES;
            //}
            //else if (count < LyraGlobal.MINIMUM_AUTHORIZERS)
            //{
            //    return LyraGlobal.MINIMUM_AUTHORIZERS;
            //}
            //else
            //{
            //    return count;
            //}
        }

        public bool AddFailedLeader(string leaderPosWallet)
        {
            // limit failed count to f/2
            var count = 1;// (Board.AllVoters.Count - 1) / 6;
            if(_failedLeaders.Count >= count)
            {
                _log.LogWarning($"too many failed leader: {_failedLeaders.Count}");
                var oldest = _failedLeaders.OrderBy(a => a.Value).First();
                _failedLeaders.Remove(oldest.Key, out _);
            }
            return _failedLeaders.TryAdd(leaderPosWallet, DateTime.Now);
        }

        public bool IsLeaderInFailureList(string accountId)
        {
            return _failedLeaders.ContainsKey(accountId);
        }

        public List<string> GetQualifiedVoters()
        {
            // debug only
            foreach (var x in _failedLeaders)
            {
                _log.LogInformation($"LookforVoters: failed leaders: {x.Key.Shorten()} {x.Value}");
            }
            // end debug

            var list = Board.ActiveNodes.ToList()   // make sure it not changed any more
                                                    //.Where(x => Board.NodeAddresses.Keys.Contains(x.AccountID)) // filter bad ips
                //.Where(x => !_failedLeaders.Keys.Contains(x.AccountID))    // exclude failed leaders ! no, failed leader can still vote.
                .Where(a => a.Votes >= LyraGlobal.MinimalAuthorizerBalance && (a.State == BlockChainState.Engaging || a.State == BlockChainState.Almighty))
                //                .Where(s => Signatures.VerifyAccountSignature(lastSb.Hash, s.AccountID, s.AuthorizerSignature))
                .OrderByDescending(a => a.Votes)
                .ThenBy(a => a.AccountID)
                .ToList();

            var list2 = list.Take(GetQualifiedNodeCount())
                .Select(a => a.AccountID)
                .ToList();
            return list2;
        }

        public void UpdateVoters()
        {
            //_log.LogInformation("UpdateVoters begin...");
            RefreshAllNodesVotes();
            var list = GetQualifiedVoters();
            var minVoterNumber = GetQualifiedNodeCount();
            if (list.Count >= minVoterNumber)        // we only update this when there is enough voters.
                Board.AllVoters = list;

            // note: a resync can't help. only wait for p2p network to reconnect.
            //else
            //{
            //    var s = $"voters count < {minVoterNumber}. network outtage happened. trying to resync";
            //    LocalConsolidationFailed(null);
            //    _log.LogError(s);
            //    //throw new InvalidOperationException(s);
            //}
            //_log.LogInformation("UpdateVoters ended.");
        }

        internal async Task CheckCreateNewViewAsync()
        {
            //_log.LogInformation($"Checking new player(s)...");
            if (CurrentState != BlockChainState.Almighty)
            {
                return;
            }

            //var cons = await _sys.Storage.GetLastConsolidationBlockAsync();
            var lsb = await _sys.Storage.GetLastServiceBlockAsync();

            var list1 = lsb.Authorizers.Keys.ToList();
            UpdateVoters();
            var list2 = Board.AllVoters;

            var firstNotSecond = list1.Except(list2).ToList();
            var secondNotFirst = list2.Except(list1).ToList();

            var reason = ViewChangeReason.None;

            if (firstNotSecond.Any() || secondNotFirst.Any())
            {
                _log.LogInformation($"voter list is not the same as previous one.");
                reason = ViewChangeReason.PlayerJoinAndLeft;
            }
            else if(lsb.TimeStamp.AddHours(4) < DateTime.UtcNow)
            {
                _log.LogInformation($"view 4 hours time out.");
                reason = ViewChangeReason.ViewTimeout;
            }
            else
            {
                //_log.LogInformation($"no reason to change view");
            }

            //_log.LogInformation($"We have new player(s). Change view...");
            // should change view for new member
            if(reason != ViewChangeReason.None)
                await BeginChangeViewAsync("View Monitor", reason);
        }

        internal void ServiceBlockCreated(ServiceBlock sb)
        {
            _lastServiceHash = sb.Hash;
            //var lsb = _sys.Storage.GetLastServiceBlock();
            //if(sb.Height > lsb.Height)
            //{
                Board.UpdatePrimary(sb.Authorizers.Keys.ToList());
                Board.CurrentLeader = sb.Leader;
                _currentView = sb.Height;
                if (_viewChangeHandler?.ViewId == sb.Height)
                {
                    _viewChangeHandler.FinishViewChange(sb.Height);
                    //_log.LogInformation($"Shift View Id to {sb.Height + 1}");
                    _viewChangeHandler.ShiftView(sb.Height + 1);
                }
            //}
        }

        internal void LocalConsolidationFailed(string hash)
        {
            _log.LogInformation($"LocalConsolidationFailed: failed hash {hash} Current: {CurrentState}");
            if (CurrentState == BlockChainState.Almighty)
                _stateMachine.Fire(_engageTriggerConsolidateFailed, hash);
        }

        //internal void LocalServiceBlockFailed(string hash)
        //{
        //    if (CurrentState == BlockChainState.Almighty)
        //        _stateMachine.Fire(BlockChainTrigger.LocalNodeMissingBlock);
        //}

        //internal void LocalTransactionBlockFailed(string hash)
        //{
        //    if (CurrentState == BlockChainState.Almighty)
        //        _stateMachine.Fire(BlockChainTrigger.LocalNodeMissingBlock);
        //}

        public string PrintProfileInfo()
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
                avgTime = g.Value.Sum(t => (decimal)t.MS) / g.Value.Count(),
                maxTime = g.Value.MaxBy(t => t.MS).MS,
                minTime = g.Value.MinBy(t => t.MS).MS
            })
             .OrderByDescending(b => b.totalTime);
            foreach (var d in q)
            {
                sbLog.AppendLine($"Total time: {d.totalTime} times: {d.times} avg: {d.avgTime:N2} ms max: {d.maxTime} ms min: {d.minTime} ms. Method Name: {d.name}  ");
            }

            sbLog.AppendLine($"Tatal time all call: {q.Sum(a => a.totalTime)}");
            var info = sbLog.ToString();

            _log.LogInformation("\n------------------------\n" + sbLog.ToString() + "\n------------------------\\n");

            StopWatcher.Reset();
            return info;
        }

        public virtual void Send2P2pNetwork(SourceSignedMessage item)
        {
            item.Sign(_sys.PosWallet.PrivateKey, item.From);
            //_log.LogInformation($"Sending message type {item.MsgType} Hash {(item as BlockConsensusMessage)?.BlockHash}");
            //if (item.MsgType == ChatMessageType.HeartBeat || item.MsgType == ChatMessageType.NodeUp)
            //    Debugger.Break();
            _localNode.Tell(item);

            //if(item is AuthorizingMsg auth && auth.Block is ProfitingGenesis gen)
            //{
            //    var json = JsonConvert.SerializeObject(item);
            //    _log.LogInformation("===\n" + json + "\n===");

            //    var jb = JsonConvert.SerializeObject(auth.Block);
            //    var blockx = JsonConvert.DeserializeObject<ProfitingGenesis>(jb);
            //    var v = blockx.VerifyHash();
            //    _log.LogInformation($"Test convert hash verify result: {v}");
            //    if(!v)
            //    {
            //        var jb2 = JsonConvert.SerializeObject(blockx);
            //        _log.LogInformation($"-----------\n{jb}\n\n{jb2}\n----------");
            //        _log.LogInformation($"+++++++++++\n{auth.Block.GetHashInput()}\n\n{blockx.GetHashInput()}\n+++++++++++");
            //    }
            //}
        }

        private async Task<ActiveNode?> DeclareConsensusNodeAsync()
        {
            if (Settings.Default.LyraNode.Lyra.Mode == Data.Utils.NodeMode.App)
                return null;

            // declare to the network
            if(_myIpAddress == null)
            {
                // (empty), port, or host:port, or ip:port, or ip, or host
                int epport = DefaultAPIPort;
                string host = "";
                (var addr, _) = await GetPublicIPAddress.PublicIPAddressAsync();
                if (addr == null)
                    return null;

                if(string.IsNullOrEmpty(Neo.Settings.Default.P2P.Endpoint))
                {
                    host = addr.ToString();
                }
                else if (int.TryParse(Neo.Settings.Default.P2P.Endpoint, out epport))
                {
                    host = addr.ToString();
                }
                else if(Neo.Settings.Default.P2P.Endpoint.Contains(':'))
                {
                    var secs = Neo.Settings.Default.P2P.Endpoint.Split(':');
                    if (secs.Length != 2 || !int.TryParse(secs[1], out _))
                        return null;

                    host = secs[0];
                    epport = int.Parse(secs[1]);
                }
                else // pure host
                {
                    host = Neo.Settings.Default.P2P.Endpoint;
                    epport = DefaultAPIPort;
                }

                if (epport == DefaultAPIPort)
                    _myIpAddress = host;
                else
                    _myIpAddress = $"{host}:{epport}";

                _log.LogInformation($"My IP, Using API endpoint: {_myIpAddress}");
            }

            if (_myIpAddress == null)
                return null;

            PosNode me = new PosNode(_sys.PosWallet.AccountId)
            {
                NodeVersion = LyraGlobal.NODE_VERSION.ToString(),
                ThumbPrint = _hostEnv?.GetThumbPrint(),
                IPAddress = _myIpAddress,
            };

            // p2p address can use ipv6 freely. not all node need to connect to it.
            // api address must use ipv4 to allow all to query data. it can be dstnat'ed one.

            //_log.LogInformation($"Declare node up to network. my IP is {_myIpAddress}");

            var lastSb = await _sys.Storage.GetLastServiceBlockAsync();
            var signAgainst = lastSb?.Hash ?? ProtocolSettings.Default.StandbyValidators[0];

            me.AuthorizerSignature = Signatures.GetSignature(_sys.PosWallet.PrivateKey,
                    signAgainst, _sys.PosWallet.AccountId);

            var msg = new ChatMsg(JsonConvert.SerializeObject(me), ChatMessageType.NodeUp)
            {
                From = _sys.PosWallet.AccountId
            };
            Send2P2pNetwork(msg);

            // add self to active nodes list
            if (_board.NodeAddresses.ContainsKey(me.AccountID))
            {
                _board.NodeAddresses[me.AccountID] = me.IPAddress.ToString();
            }
            else
                _board.NodeAddresses.TryAdd(me.AccountID, me.IPAddress.ToString());
            OnNodeActive(me.AccountID, me.AuthorizerSignature, _stateMachine.State, _myIpAddress, me.ThumbPrint);

            return _board.ActiveNodes.FirstOrDefault(a => a.AccountID == me.AccountID);
        }

        private void OnHeartBeat(HeartBeatMessage heartBeat)
        {
            // dq any lower version
            var ver = new Version(heartBeat.NodeVersion);
            if (string.IsNullOrWhiteSpace(heartBeat.NodeVersion) || LyraGlobal.MINIMAL_COMPATIBLE_VERSION.CompareTo(ver) > 0)
            {
                //_log.LogInformation($"Node {heartBeat.From.Shorten()} ver {heartBeat.NodeVersion} is too old. Need at least {LyraGlobal.MINIMAL_COMPATIBLE_VERSION}");
                return;
            }

            OnNodeActive(heartBeat.From, heartBeat.AuthorizerSignature, heartBeat.State, heartBeat.PublicIP, null);
        }

        private void OnNodeActive(string accountId, string authSign, BlockChainState state, string endpoint, string thumbPrint)
        {
            try
            {
                var signAgainst = _lastServiceHash ?? ProtocolSettings.Default.StandbyValidators[0];

                // verify pftaccount !! genesis mode!!
                //if (!string.IsNullOrWhiteSpace(pftAccount))
                //{
                //    var pfts = await _sys.Storage.FindAllProfitingAccountForOwnerAsync(accountId);
                //    var pft = pfts.FirstOrDefault();
                //    if (pft == null || pft.AccountID != pftAccount)
                //    {
                //        var exists = _board.ActiveNodes.FirstOrDefault(a => a.AccountID == accountId);
                //        if (exists != null)
                //            _board.ActiveNodes.Remove(exists);
                //        return;
                //    }
                //}

                ActiveNode node;
                if (_board.ActiveNodes.ToArray().Any(a => a.AccountID == accountId))
                {
                    node = _board.ActiveNodes.First(a => a.AccountID == accountId);
                    node.LastActive = DateTime.Now;
                    node.State = state;
                    node.AuthorizerSignature = authSign;

                    if (thumbPrint != null)
                        node.ThumbPrint = thumbPrint;
                }
                else
                {
                    node = new ActiveNode
                    {
                        AccountID = accountId,
                        State = state,
                        LastActive = DateTime.Now,
                        AuthorizerSignature = authSign
                    };

                    if (thumbPrint != null)
                        node.ThumbPrint = thumbPrint;

                    _board.ActiveNodes.Add(node);
                }

                if (!string.IsNullOrWhiteSpace(endpoint))
                {
                    string ip = endpoint;
                    var port = DefaultAPIPort;

                    if (endpoint.Contains(":"))
                    {
                        var secs = endpoint.Split(":");
                        ip = secs[0];
                        port = int.Parse(secs[1]);
                    }
                    if (System.Net.IPAddress.TryParse(ip, out System.Net.IPAddress? addr) || Dns.GetHostEntry(ip) != null)
                    {
                        // temp code. make it compatible.
                        if (true)//_verifiedIP.ContainsKey(safeIp))
                        {
                            //var existingIP = _board.NodeAddresses.Where(x => x.Value.StartsWith(ip)).ToList();
                            //foreach (var exip in existingIP)
                            //{
                            //    _board.NodeAddresses.TryRemove(exip.Key, out _);
                            //}

                            _board.NodeAddresses.AddOrUpdate(accountId, endpoint, (key, oldValue) => endpoint);
                        }

                        //if(thumbPrint != null)// || !_verifiedIP.ContainsKey(safeIp))
                        //{
                        //    // if thumbPrint != null means its a node up signal.
                        //    // this will help make the voters list consistent across all nodes.
                        //    _ = Task.Run(async () =>
                        //    {
                        //        try
                        //        {
                        //            var outDated = _verifiedIP.Where(x => x.Value < DateTime.UtcNow.AddDays(-1))
                        //                .Select(x => x.Key)
                        //                .ToList();

                        //            foreach (var od in outDated)
                        //                _verifiedIP.TryRemove(od, out _);

                        //            // just send it to the leader
                        //            var platform = Environment.OSVersion.Platform.ToString();
                        //            var appName = "LyraNode";
                        //            var appVer = "1.0";
                        //            var networkId = Settings.Default.LyraNode.Lyra.NetworkId;
                        //            ushort peerPort = 4504;
                        //            if (networkId == "mainnet")
                        //                peerPort = 5504;

                        //            var client = LyraRestClient.Create(networkId, platform, appName, appVer, $"https://{safeIp}:{peerPort}/api/Node/");

                        //            var ver = await client.GetVersion(1, appName, appVer);
                        //            if (ver.PosAccountId == node.AccountID 
                        //                && client.ServerThumbPrint != null
                        //                && client.ServerThumbPrint == node.ThumbPrint)
                        //                _verifiedIP.AddOrUpdate(safeIp, DateTime.UtcNow, (key, oldValue) => DateTime.UtcNow);                                
                        //        }
                        //        catch (Exception ex)
                        //        {
                        //            _log.LogInformation($"Failure in get node thumbprint: {ex.Message} for {safeIp}");
                        //        }
                        //    });
                        //}

                        // backslash. will do this later
                        //var platform = Environment.OSVersion.Platform.ToString();
                        //var appName = "LyraAggregatedClient";
                        //var appVer = "1.0";
                        //ushort peerPort = 4504;
                        //var networkdId = Neo.Settings.Default.LyraNode.Lyra.NetworkId;
                        //if (networkdId == "mainnet")
                        //    peerPort = 5504;
                        //var client = LyraRestClient.Create(networkdId, platform, appName, appVer, $"https://{ip}:{peerPort}/api/Node/");
                        //var lsb = await client.GetLastServiceBlock();
                        //if(lsb.ResultCode == APIResultCodes.Success)
                        //{
                        //    var block = lsb.GetBlock();
                        //    if(block?.Hash == lastSb.Hash)
                        //    {
                        //        _board.NodeAddresses.AddOrUpdate(accountId, ip, (key, oldValue) => ip);
                        //    }
                        //}                    
                    }
                }
                else
                {
                    _log.LogWarning($"Hearbeat from {accountId.Shorten()} has no endpoint: ({endpoint})");
                }

                var deadList = _board.ActiveNodes.Where(a => a.LastActive < DateTime.Now.AddMinutes(-60)).ToList();
                foreach (var n in deadList)
                    _board.NodeAddresses.TryRemove(n.AccountID, out _);
            }
            catch(Exception ex)
            {
                _log.LogError($"In OnNodeActive: {ex}");
            }
        }

        private async Task HeartBeatAsync()
        {
            // this keep the node up pace
            //var lsb = await _sys.Storage.GetLastServiceBlockAsync();
            //if(lsb == null)
            //{
            //    _board.CurrentLeader = ProtocolSettings.Default.StandbyValidators[0];
            //    _board.LeaderCandidate = ProtocolSettings.Default.StandbyValidators[0];
            //    _board.UpdatePrimary(ProtocolSettings.Default.StandbyValidators.ToList());                
            //}
            //else
            //{
            //    _board.CurrentLeader = lsb.Leader;
            //    _board.UpdatePrimary(lsb.Authorizers.Keys.ToList());
            //}

            var me = _board.ActiveNodes.FirstOrDefault(a => a.AccountID == _sys.PosWallet.AccountId);
            if (me == null && Neo.Settings.Default.LyraNode.Lyra.Mode == Data.Utils.NodeMode.Normal)
                me = await DeclareConsensusNodeAsync();

            me = _board.ActiveNodes.FirstOrDefault(a => a.AccountID == _sys.PosWallet.AccountId);
            if (me == null)
            {
                return;     // app mode null
                //_log.LogError("No me in billboard!!!");
            }
            else
            {
                me.State = _stateMachine.State;
                me.LastActive = DateTime.Now;
            }

            var node = _board.ActiveNodes.FirstOrDefault(a => a.AccountID == _sys.PosWallet.AccountId);
            if (node != null)
                node.LastActive = DateTime.Now;

            // declare to the network
            var lastSb = await _sys.Storage.GetLastServiceBlockAsync();
            var signAgainst = lastSb?.Hash ?? ProtocolSettings.Default.StandbyValidators[0];
            var msg = new HeartBeatMessage
            {
                From = _sys.PosWallet.AccountId,
                NodeVersion = LyraGlobal.NODE_VERSION.ToString(),
                Text = "I'm live",
                State = _stateMachine.State,
                PublicIP = _myIpAddress?.ToString() ?? "",
                AuthorizerSignature = Signatures.GetSignature(_sys.PosWallet.PrivateKey, signAgainst, _sys.PosWallet.AccountId)
            };

            Send2P2pNetwork(msg);
        }

        private async Task OnNodeUpAsync(ChatMsg chat)
        {
            // must throttle on this msg

            //_log.LogInformation($"OnNodeUpAsync: Node is up: {chat.From.Shorten()}");

            try
            {
                if (_board == null)
                    return;

                var node = chat.Text.UnJson<PosNode>();

                // dq any lower version
                if (string.IsNullOrWhiteSpace(node.NodeVersion))
                    return;

                var ver = new Version(node.NodeVersion);
                if (LyraGlobal.MINIMAL_COMPATIBLE_VERSION.CompareTo(ver) > 0)
                {
                    //_log.LogInformation($"Node {chat.From.Shorten()} ver {node.NodeVersion} is too old. Need at least {LyraGlobal.MINIMAL_COMPATIBLE_VERSION}");
                    return;
                }

                // verify signature
                if (string.IsNullOrWhiteSpace(node.IPAddress))
                    return;

                OnNodeActive(node.AccountID, node.AuthorizerSignature, BlockChainState.Almighty, node.IPAddress, node.ThumbPrint);
            }
            catch (Exception ex)
            {
                _log.LogWarning($"OnNodeUpAsync: {ex}");
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

        // TODO: check conditions when block not generated
        private async Task ConsolidateBlocksAsync()
        {
            if (_stateMachine.State != BlockChainState.Almighty
                && _stateMachine.State != BlockChainState.Engaging)
                return;

            // avoid hammer
            if (DateTime.UtcNow - _lastConsolidateTry < TimeSpan.FromSeconds(1))
                return;

            _lastConsolidateTry = DateTime.UtcNow;

            //_log.LogInformation("ConsolidateBlocksAsync: Begin prepare a consolidation block.");

            // check if there are pending consolidate blocks
            var pendingCons = _activeConsensus.Values
                .Where(a =>
                    a.State != null
                    && a.State.IsSourceValid
                    && a.State.InputMsg.Block is ConsolidationBlock
                    && a.Status != ConsensusWorker.ConsensusWorkerStatus.Commited);

            if (pendingCons.Any())
            {
                var ts = DateTime.UtcNow - pendingCons.First().TimeStarted;
                //_log.LogInformation($"ConsolidateBlocksAsync: A consolidation block is pending, {ts.TotalSeconds}s ago.");
                return;
            }

            // avoid racing condition (commited but not saved)
            if (pendingCons.Any(a => !a.State.IsSaved))
                return;

            if (_lastCons == null)
                return;

            try
            {
                var lastCons = _lastCons;
                if(pendingCons.Any(x => x.State.IsSaved && x.State.InputMsg.Block.Height > lastCons.Height))
                {
                    _log.LogWarning($"ConsolidateBlocksAsync: Racing condition: pending cons height > last cons");
                    return;
                }

                //_log.LogInformation("ConsolidateBlocksAsync: Finding all floating blocks...");
                var timeStamp = DateTime.UtcNow.AddSeconds(LyraGlobal.CONSOLIDATIONDELAY); // delay one minute
                var unConsList = await _sys.Storage.GetBlockHashesByTimeRangeAsync(lastCons.TimeStamp, timeStamp);

                // if 1 it must be previous consolidation block.
                if (unConsList.Count() >= 10 || (unConsList.Count() > 1 && timeStamp - lastCons.TimeStamp > TimeSpan.FromMinutes(10)))
                {
                    //_log.LogInformation("ConsolidateBlocksAsync: time to create new one...");
                    try
                    {
                        var InCons = _activeConsensus.Any(a => a.Value.Status == ConsensusWorker.ConsensusWorkerStatus.InAuthorizing
                                && a.Value?.State?.InputMsg?.Block?.BlockType == BlockTypes.Consolidation);
                        
                        if(!InCons)
                        {
                            if (IsThisNodeLeader)
                            {
                                await LeaderCreateConsolidateBlockAsync(lastCons, timeStamp, unConsList);
                            }
                            else
                            {
                                //_log.LogInformation("ConsolidateBlocksAsync: Not the leader.");
                                //// leader may be faulty
                                //var lsp = await _sys.Storage.GetLastServiceBlockAsync();
                                //// give new leader enough time to consolidate blocks
                                //if(lsp.TimeStamp < DateTime.UtcNow.AddSeconds(-45 + LyraGlobal.CONSOLIDATIONDELAY))
                                //    await BeginChangeViewAsync("cons blk monitor", ViewChangeReason.LeaderFailedConsolidating);
                            }
                        }
                        else
                        {
                            _log.LogInformation("ConsolidateBlocksAsync: consolidation block in queue.");
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.LogError($"In creating consolidation block: {ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"Error In GenerateConsolidateBlock: {ex}");
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
        //            var lcx = LyraRestClient.Create(Neo.Settings.Default.LyraNode.Lyra.NetworkId, Environment.OSVersion.ToString(), "Seed0", "1.0", $"http://{node.IPAddress}:{Neo.Settings.Default.P2P.WebAPI}/api/Node/");
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

        public async Task LeaderCreateConsolidateBlockAsync(ConsolidationBlock lastCons, DateTime timeStamp, IEnumerable<string> collection)
        {
            _log.LogInformation($"Leader is creating ConsolidationBlock... ");

            var consBlock = new ConsolidationBlock
            {
                createdBy = _board.CurrentLeader,
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
                IsServiceBlock = false,
                From = _sys.PosWallet.AccountId,
                BlockHash = consBlock.Hash,
                Block = consBlock,
                MsgType = ChatMessageType.AuthorizerPrePrepare
            };

            var state = await CreateAuthringStateAsync(msg, true);

            await SubmitToConsensusAsync(state.state);

            _log.LogInformation($"ConsolidationBlock was submited. ");
        }

        public static Props Props(DagSystem sys, IHostEnv hostEnv, IActorRef localNode, IActorRef blockchain)
        {
            return Akka.Actor.Props.Create(() => new ConsensusService(sys, hostEnv, localNode, blockchain)).WithMailbox("consensus-service-mailbox");
        }

        public bool IsBlockInQueue(Block block)
        {
            if (block == null)
                return false;

            var sameHash = _activeConsensus.Values.Any(a => a.State?.InputMsg.Hash == block.Hash);
            if (sameHash)
                return true;

            if (block is TransactionBlock tx)
            {
                var doubleSpend = _activeConsensus.Values
                    .Where(x => x.State != null && x.State.InputMsg?.Block is TransactionBlock)
                    .Select(x => new
                    {
                        x.IsTimeout,
                        x.State.IsCommited,
                        x.State.CommitConsensus,
                        tx = x.State.InputMsg.Block as TransactionBlock
                    })
                    .Where(a => !a.IsTimeout && !a.IsCommited && a.tx.AccountID == tx.AccountID && a.tx.Height == tx.Height)
                    .FirstOrDefault();

                if(doubleSpend != null)
                {
                    if(doubleSpend.tx.Hash != tx.Hash)      // seed nodes will forward it
                        _log.LogWarning($"Double spend dup: {tx.Height} on {tx.AccountID} hash in queue: {doubleSpend.tx.Hash} hash new: {tx.Hash}");
                    return true;
                }
                //// make strict check to ensure all account operations are in serial.
                //if (sameChainBlocks.Any())
                //{
                //    _log.LogInformation("Force single account ops in serial");
                //    return true;
                //}

                // bellow not necessary
                //var sameHeight = sameChainBlocks.Any(y => y.Height == tx.Height);
                //var samePrevHash = sameChainBlocks.Any(a => a.PreviousHash == tx.PreviousHash);

                //if (sameHeight || samePrevHash)
                //{
                //    //_log.LogCritical($"double spend detected: {tx.AccountID} Height: {tx.Height} Hash: {tx.Hash}");
                //    return true;
                //}
            }

            return false;
        }

        private async Task<bool> SubmitToConsensusAsync(AuthState? state)
        {
            if(state == null) return false;

            // unit test support
            if(Settings.Default.LyraNode.Lyra.NetworkId == "xtest")
            {
                await OnNewBlock(state.InputMsg.Block);
                return true;
            }
            if (IsBlockInQueue(state.InputMsg?.Block))
            {
                return false;
                //throw new Exception("Block is already in queue.");
            }

            Send2P2pNetwork(state.InputMsg);

            var worker = await GetWorkerAsync(state.InputMsg.Block.Hash);
            if (worker != null)
            {
                await worker.ProcessStateAsync(state);
            }

            return true;
        }

        private async Task<ConsensusWorker> GetWorkerAsync(string hash, bool checkState = false)
        {
            // if a block is in database
            var aBlock = await _sys.Storage.FindBlockByHashAsync(hash);
            if (aBlock != null)
            {
                //_log.LogWarning($"GetWorker: already in database! hash: {hash.Shorten()}");
                return null;
            }

            if (_activeConsensus.ContainsKey(hash))
                return _activeConsensus[hash];
            else
            {
                ConsensusWorker worker;
                if (checkState && _stateMachine.State == BlockChainState.Almighty)
                    worker = new ConsensusWorker(this, hash);
                else if (checkState && _stateMachine.State == BlockChainState.Engaging)
                    worker = new ConsensusEngagingWorker(this, hash);
                else
                    worker = new ConsensusWorker(this, hash);

                if (_activeConsensus.TryAdd(hash, worker))
                    return worker;
                else
                    return _activeConsensus[hash];
            }
        }

        public async Task FireSignalrWorkflowEventAsync(WorkflowEvent wfevent)
        {
            await _hostEnv.FireEventAsync(new EventContainer(wfevent));
            //if (_hubContext != null)
            //    await _hubContext.Clients.All.OnEvent();
        }

        public void OnWorkflowTerminated(string key, bool authSuccess, bool hasError)
        {
            // we assume 
            Console.WriteLine($"{DateTime.Now:mm:ss.ff} OnWorkflowTerminated: {key} Auth: {authSuccess} Error: {hasError}");

            OnWorkflowFinished?.Invoke(key, authSuccess);
        }

        public async Task Worker_OnConsensusSuccessAsync(Block block, ConsensusResult? result, bool localIsGood)
        {
            // first unlock 
            if (_lockers.ContainsKey(block.Hash))
            {
                var dto = _lockers[block.Hash];

                if (!dto.haswf && dto.workflowid != null)
                    _log.LogCritical($"Fatal!!! locker dto workflow wrong logic 1 for {block.Hash}");

                if (!dto.haswf)
                {
                    // no workflow related. just release the lock.
                    RemoveLockerDTO(dto.reqhash);

                    OnBlockFinished?.Invoke(block.Hash, result == ConsensusResult.Yea);
                }
                else if(dto.haswf && dto.workflowid == null && result != ConsensusResult.Yea)
                {
                    // block auth failed. no workflow
                    RemoveLockerDTO(dto.reqhash);

                    OnBlockFinished?.Invoke(block.Hash, result == ConsensusResult.Yea);
                }
            }

            // consequence procedures
            if (_hostEnv != null)     // for unit test
            {
                await _hostEnv.FireEventAsync(new EventContainer(
                    new ConsensusEvent
                    {
                        BlockAPIResult = BlockAPIResult.Create(block),
                        Consensus = result,
                    }));
            }

            if (result != ConsensusResult.Uncertain)
                _successBlockCount++;

            //_log.LogInformation($"Worker_OnConsensusSuccess {block.Hash.Shorten()} {block.BlockType} {block.Height} result: {result} local is good: {localIsGood}");

            // no, don't remove so quick. we will still receive message related to it.
            // should be better solution for high tps to avoid queue increase too big.
            // tps 100 * timeout 20s = 2k buffer, sounds we can handle it.
            //_activeConsensus.TryRemove(block.Hash, out _);
            //_log.LogInformation($"Finished consensus: {_successBlockCount} Active Consensus: {_activeConsensus.Count} Locked: {LockedCount}");

            if (result == ConsensusResult.Yea)
                _sys.NewBlockGenerated(block);

            // just save the block in queue
            // non-leader will wait. if leader failed, the block can be send immediatelly
            // the saved block can be used to verify/authenticate
            // pool events
            // only current leader deals with managed blocks
            //if (Board.CurrentLeader == _sys.PosWallet.AccountId && block.ContainsTag(Block.REQSERVICETAG))

            if (!localIsGood)
            {
                LocalConsolidationFailed(block.Hash);
            }

            if (block is ServiceBlock serviceBlock)
            {
                if (result == ConsensusResult.Yea)
                {
                    ServiceBlockCreated(serviceBlock);

                    var wfhost = _hostEnv.GetWorkflowHost();
                    _log.LogInformation($"View changed to {serviceBlock.Height} ");
                    await wfhost.PublishEvent("ViewChanged", $"{serviceBlock.PreviousHash}", result);
                }

                return;
            }
            else if (block is ConsolidationBlock cons && result == ConsensusResult.Yea)
            {
                _lastCons = cons;
                if(CurrentState == BlockChainState.Genesis)
                    _ = Task.Run(async () => { await _stateMachine.FireAsync(BlockChainTrigger.GenesisDone); }).ConfigureAwait(false);
                else
                {
                    // make sure database is healthy
                    var dbblks = await _sys.Storage.GetBlockCountAsync();
                    if(cons.totalBlockCount + 1 > dbblks)
                    {
                        _ = Task.Run(async () => { await DBCCAsync(); }).ConfigureAwait(false);
                    }
                }
            }

            // tmp code
            if (block is GuildGenesisBlock)
                return;

            // let any existing workflow continue execute
            if (block is TransactionBlock trans && block.Tags != null && block.Tags.ContainsKey(Block.MANAGEDTAG))
            {
                MgmtReqs.Post((trans, result));
            }
            
            // process any service request from normal user or workflow
            if (block is SendTransferBlock send)
            {
                SendReqs.Post((send, result));
            }
        }


        private async Task<LyraContext?> GetWfContextByReqHashAsync(string reltx)
        {
            var wfhost = _hostEnv.GetWorkflowHost();

            if (_lockers.ContainsKey(reltx))
            {
                var dto = _lockers[reltx];
                var wf = await wfhost.PersistenceStore.GetWorkflowInstance(dto.workflowid);
                var ctx = wf.Data as LyraContext;
                return ctx;
            }
            else
                return null;
        }

        public async Task<TransactionBlock?> GetBlockByRelatedTxForCompareAuthAsync(string reltx)
        {
            var ctx = await GetWfContextByReqHashAsync(reltx);
            return ctx?.GetLastBlock();

            //if (_workFlows.ContainsKey(reltx))
            //{
            //    var Id = _workFlows[reltx];
            //    var wf = await wfhost.PersistenceStore.GetWorkflowInstance(Id);
            //    var ctx = wf.Data as LyraContext;
            //    return ctx.GetLastBlock();

            //    //var SubWorkflow = BrokerFactory.DynWorkFlows[ctx.SvcRequest];

            //    //var sendBlock = await DagSystem.Singleton.Storage.FindBlockByHashAsync(ctx.SendHash)
            //    //    as SendTransferBlock;
            //    //var block =
            //    //    await BrokerOperations.ReceiveViaCallback[SubWorkflow.GetDescription().RecvVia](DagSystem.Singleton, sendBlock)
            //    //        ??
            //    //    await SubWorkflow.BrokerOpsAsync(DagSystem.Singleton, sendBlock)
            //    //        ??
            //    //    await SubWorkflow.ExtraOpsAsync(DagSystem.Singleton, ctx.SendHash);
            //    //_log.LogInformation($"BrokerOpsAsync for {ctx.SendHash} called and generated {block}");

            //    //ctx.SetLastBlock(block);
            //    //return block;
            //}
        }

        //public string GetHashForWorkflow(string id)
        //{
        //    return _workFlows.FirstOrDefault(a => a.Value == id).Key;
        //}

        private bool AddLockerDTO(LockerDTO dto)
        {
            foreach(var id in dto.lockedups)
            {
                if (_lockers.Values.Any(a => a.lockedups.Contains(id)))
                    return false;
            }

            return _lockers.TryAdd(dto.reqhash, dto);            
        }

        private bool RemoveLockerDTO(string reqHash)
        {
            //_log.LogInformation($"Removing locker DTO req: {reqHash} has req: {_lockers.ContainsKey(reqHash)}");
            LockerDTO dto;
            var ret = _lockers.TryRemove(reqHash, out dto);

            if(!ret)
            {
                //_log.LogCritical($"Can't RemoveLockerDTO!!! req: {reqHash} has req: {_lockers.ContainsKey(reqHash)}");
            }

            return ret;
        }

        public bool IsAccountLocked(string accountId)
        {
            return _lockers.Any(a => a.Value.lockedups.Contains(accountId));
        }

        public bool IsRequestLocked(string reqHash)
        {
            return _lockers.ContainsKey(reqHash);
        }

        public LockerDTO GetLockerDTOFromReq(string reqHash)
        {
            return _lockers[reqHash];
        }

        public async Task ProcessServerReqBlockAsync(SendTransferBlock send, ConsensusResult? result)
        {
            if (result != ConsensusResult.Yea)
                return;

            string svcreqtag = null;
            if (send.Tags != null && send.Tags.ContainsKey(Block.REQSERVICETAG))
                svcreqtag = send.Tags[Block.REQSERVICETAG];

            if (svcreqtag != null)
            {
                // try lock some resources
                var lkrdto = await WorkFlowBase.GetLocketDTOAsync(_sys, send);
                var lockedok = AddLockerDTO(lkrdto);
                if(lockedok)
                {
                    // resource locked. then we try to authorize the block.
                    if (BrokerFactory.DynWorkFlows.ContainsKey(send.Tags[Block.REQSERVICETAG]))
                    {
                        //var wf = BrokerFactory.DynWorkFlows[send.Tags[Block.REQSERVICETAG]];

                        //var rl = await wf.PreAuthAsync(_sys, send);

                        //if (rl.Result == APIResultCodes.Success)
                        //{
                            // block is leagle. we start a workflow to process it.
                            var wfhost = _hostEnv.GetWorkflowHost();
                            var ctx = new LyraContext
                            {
                                Send = send
                            };

                            // id: a guid; 1st argument -> defination id: svcreq
                            lkrdto.workflowid = await wfhost.StartWorkflow(svcreqtag, ctx);
                        //}
                        //else
                        //{
                        //    // unable to authorize. the send is wrong. do a refund.
                        //}
                    }
                }
            }
        }

        public async Task ProcessManagedBlockAsync(TransactionBlock block, ConsensusResult? result)
        {
            // find the key
            string? key = null;
            
            if(block is IPool pool)
            {
                key = pool.RelatedTx;
            }
            else if(block is IBrokerAccount ib)
            {
                key = ib.RelatedTx;
            }
            else if (block is ReceiveTransferBlock recv)
            {
                key = recv.SourceHash;
                //if (block.AccountID == PoolFactoryBlock.FactoryAccount)
            }

            // add token gateway, merchant etc.

            if (string.IsNullOrEmpty(key) && block.BlockType != BlockTypes.PoolFactory && block.BlockType != BlockTypes.GuildGenesis)
            {
                _log.LogError($"Should not happen: unknown mgmt block {block.BlockType} {block.Hash}");
                return;
            }

            var wfhost = _hostEnv.GetWorkflowHost();
            if(result != ConsensusResult.Yea)
                _log.LogInformation($"Key is {key} Publish Consensus event {result} ");
            await wfhost.PublishEvent("MgBlkDone", key, result);
            return;
        }

        private async Task<bool> CriticalRelayAsync<T>(T message, Func<T, Task> localAction)
            where T : SourceSignedMessage, new()
        {
            //_log.LogInformation($"OnRelay: {message.MsgType} From: {message.From.Shorten()} Hash: {(message as BlockConsensusMessage)?.BlockHash} My state: {CurrentState}");

            // seed node relay heartbeat, only once
            // this keep the whole network one consist view of active nodes.
            // this is important to make election.
            if (_criticalMsgCache.TryAdd(message.Hash, DateTime.Now))
            {
                // try ever node forward.
                // monitor network traffic closely.

                _localNode.Tell(message);     // no sign again!!!

                if (localAction != null)
                {
                    await localAction(message);
                }

                return true;
            }
            else
                return false;
        }

        async Task OnNextConsensusMessageAsync(SourceSignedMessage item)
        {
            //_log.LogInformation($"OnMessage: {item.MsgType} From: {item.From.Shorten()} Hash: {(item as BlockConsensusMessage)?.BlockHash} My state: {CurrentState}");
            if (item is ChatMsg chatMsg)
            {
                await OnRecvChatMsgAsync(chatMsg);
                return;
            }

            if (!CanDoConsense)   // only do consensus when can
            {
                return;
            }

            if (item is ViewChangeMessage vcm)
            {
                // need to listen to any view change event.
                //_log.LogInformation($"View change request from {vcm.From.Shorten()}, is voter? {Board.AllVoters.Contains(vcm.From)}");
                await _viewChangeHandler.ProcessMessageAsync(vcm);
                return;
            }
            else if (_stateMachine.State == BlockChainState.Genesis ||
                _stateMachine.State == BlockChainState.Engaging ||
                _stateMachine.State == BlockChainState.Almighty)
            {
                if (item is AuthorizingMsg ppMsg)
                {
                    if (IsBlockInQueue(ppMsg.Block))
                    {
                        // need to send message indicate that a requota is needed

                        return;
                    }
                }

                if (item is BlockConsensusMessage cm)
                {
                    var worker = await GetWorkerAsync(cm.BlockHash, true);
                    if (worker != null)
                    {
                        await worker.ProcessMessageAsync(cm);
                    }
                    return;
                }
            }
        }

        private async Task OnRecvChatMsgAsync(ChatMsg chat)
        {
            switch (chat.MsgType)
            {
                case ChatMessageType.HeartBeat:
                    OnHeartBeat(chat as HeartBeatMessage);
                    break;
                case ChatMessageType.NodeUp:
                    await OnNodeUpAsync(chat);
                    break;
                //case ChatMessageType.NodeStatusInquiry:
                //    var status = await GetNodeStatusAsync();
                //    var resp = new ChatMsg(JsonConvert.SerializeObject(status), ChatMessageType.NodeStatusReply)
                //    {
                //        From = _sys.PosWallet.AccountId
                //    };
                //    Send2P2pNetwork(resp);
                //    break;
                default:
                    break;
            }
        }

        private readonly ManualResetEvent _refreshNodesLocker = new ManualResetEvent(true);
        public void RefreshAllNodesVotes()
        {
            if (!_refreshNodesLocker.WaitOne(1))
                return;

            try
            {
                // remove stalled nodes
                // debug only
                foreach (var x in _board.ActiveNodes.Where(a => a.LastActive < DateTime.Now.AddSeconds(-60)))
                {
                    _log.LogInformation($"RefreshAllNodesVotes is removing {x.AccountID}");
                }
                // end debug
                _board.ActiveNodes.RemoveAll(a => a.LastActive < DateTime.Now.AddSeconds(-60));

                var livingPosNodeIds = _board.ActiveNodes.Select(a => a.AccountID).ToList();
                _lastVotes = _sys.Storage.FindVotes(livingPosNodeIds, DateTime.UtcNow);

                foreach (var node in _board.ActiveNodes.ToArray())
                {
                    var vote = _lastVotes.FirstOrDefault(a => a.AccountId == node.AccountID);
                    if (vote == null)
                    {
                        //_log.LogInformation($"No (zero) vote found for {node.AccountID}");
                        node.Votes = 0;
                    }
                    else
                        node.Votes = vote.Amount;

                    if (node.ProfitingAccountId != null)
                    {
                        var stks = _sys.Storage.FindAllStakings(node.ProfitingAccountId, DateTime.UtcNow);
                        node.Votes += stks.Sum(a => a.Amount);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"In RefreshAllNodesVotes: {ex}");
            }
            finally
            {
                _refreshNodesLocker.Set();
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
