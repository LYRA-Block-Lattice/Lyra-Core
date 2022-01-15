using Akka.Actor;
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
using static Neo.Network.P2P.LocalNode;
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

namespace Lyra.Core.Decentralize
{
    /// <summary>
    /// pBFT Consensus
    /// </summary>
    public partial class ConsensusService : ReceiveActor
    {
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

        readonly ILogger _log;
        IHostEnv _hostEnv;
        readonly ConcurrentDictionary<string, DateTime> _criticalMsgCache;
        readonly ConcurrentDictionary<string, ConsensusWorker> _activeConsensus;
        List<Vote> _lastVotes;
        private readonly BillBoard _board;
        private readonly List<TransStats> _stats;
        private System.Net.IPAddress _myIpAddress;

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
        ConcurrentDictionary<string, string> _workFlows;

        public static ConsensusService Singleton { get; private set; }

        public ConsensusService(DagSystem sys, IHostEnv hostEnv, IActorRef localNode, IActorRef blockchain)
        {
            _sys = sys;
            _currentView = sys.Storage.GetCurrentView();
            _hostEnv = hostEnv;
            _localNode = localNode;
            //_blockchain = blockchain;
            _log = new SimpleLogger("ConsensusService").Logger;
            _successBlockCount = 0;
            _lastConsolidateTry = DateTime.UtcNow;

            _criticalMsgCache = new ConcurrentDictionary<string, DateTime>();
            _activeConsensus = new ConcurrentDictionary<string, ConsensusWorker>();
            _stats = new List<TransStats>();

            _board = new BillBoard();

            _workFlows = new ConcurrentDictionary<string, string>();
            _failedLeaders = new ConcurrentDictionary<string, DateTime>();

            _af = new AuthorizersFactory();
            _af.Init();
            _bf = new BrokerFactory();
            _bf.Init(_af, _sys.Storage);

            if (localNode == null)
            {
                Board.CurrentLeader = _sys.PosWallet.AccountId;
                return;         // for unit test
            }                

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
                                await Task.Delay(1000);     // some nodes may not got this in time

                                try
                                {
                                    var svcBlock = await CreateNewViewAsNewLeaderAsync();

                                    _log.LogInformation($"New View was created. send to network...");
                                    await SendBlockToConsensusAndForgetAsync(svcBlock);
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
            Receive<AskForConsensusState>((askReq) =>
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

                    var state = CreateAuthringState(msg, true);
                    Sender.Tell(state);
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

            ReceiveAsync<SignedMessageRelay>(async relayMsg =>
            {
                try
                {
                    var signedMsg = relayMsg.signedMessage;

                    //_log.LogInformation($"ReceiveAsync SignedMessageRelay from {signedMsg.From.Shorten()} Hash {(signedMsg as BlockConsensusMessage)?.BlockHash}");

                    if(signedMsg.TimeStamp < DateTime.UtcNow.AddSeconds(3) &&
                        signedMsg.TimeStamp > DateTime.UtcNow.AddSeconds(-30))
                    {
                        if (signedMsg.VerifySignature(signedMsg.From))
                        {
                            await OnNextConsensusMessageAsync(signedMsg);
                            //await CriticalRelayAsync(signedMsg, async (msg) =>
                            //{
                            //    await OnNextConsensusMessageAsync(msg);
                            //});

                            // not needed anymore
                            // seeds take resp to forward heatbeat, once
                            if (IsThisNodeSeed && (
                                signedMsg.MsgType == ChatMessageType.HeartBeat
                                //|| (signedMsg is AuthorizingMsg au && (au.Block is ConsolidationBlock || au.Block is ServiceBlock))
                                //|| signedMsg.MsgType == ChatMessageType.ViewChangeRequest
                                //|| signedMsg.MsgType == ChatMessageType.ViewChangeReply
                                //|| signedMsg.MsgType == ChatMessageType.ViewChangeCommit
                                ))
                            {
                                await CriticalRelayAsync(signedMsg, null);
                            }
                        }
                        else
                        {
                            _log.LogWarning($"Receive Relay illegal type {signedMsg.MsgType} Delayed {(DateTime.UtcNow - signedMsg.TimeStamp).TotalSeconds}s Verify: {signedMsg.VerifySignature(signedMsg.From)} From: {signedMsg.From.Shorten()}");
                            //if (signedMsg.MsgType == ChatMessageType.AuthorizerPrePrepare)
                            //{
                            //    var json = JsonConvert.SerializeObject(signedMsg);
                            //    Console.WriteLine("===\n" + json + "\n===");

                            //    var jb = JsonConvert.SerializeObject(signedMsg);
                            //}
                        }
                    }

                }
                catch (Exception ex)
                {
                    _log.LogCritical("Error Receive Relay!!! " + ex.ToString());
                }
            });

            //ReceiveAsync<AuthState>(async state =>
            //{
            //    // not accepting new transaction from API
            //    // service block generate as usual.
            //    if (_viewChangeHandler.IsViewChanging)
            //        return;

            //    _log.LogInformation($"State told.");
            //    if (_stateMachine.State != BlockChainState.Almighty && _stateMachine.State != BlockChainState.Engaging && _stateMachine.State != BlockChainState.Genesis)
            //    {
            //        state.Close();
            //        return;
            //    }

            //    if (_viewChangeHandler.TimeStarted != DateTime.MinValue)
            //    {
            //        // view changing in progress. no block accepted
            //        state.Close();
            //        return;
            //    }

            //    //TODO: check  || _context.Board == null || !_context.Board.CanDoConsensus
            //    if (state.InputMsg.Block is TransactionBlock)
            //    {
            //        var acctId = (state.InputMsg.Block as TransactionBlock).AccountID;
            //        if (FindActiveBlock(acctId, state.InputMsg.Block.Height))
            //        {
            //            _log.LogCritical($"Double spent detected for {acctId}, index {state.InputMsg.Block.Height}");
            //            return;
            //        }
            //        state.SetView(Board.PrimaryAuthorizers);
            //    }
            //    else if (state.InputMsg.Block is ServiceBlock)
            //    {
            //        state.SetView(Board.AllVoters);
            //    }

            //    await SubmitToConsensusAsync(state);
            //});

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

            Receive<Idle>(o => { });

            ReceiveAny((o) => { _log.LogWarning($"consensus svc receive unknown msg: {o.GetType().Name}"); });

            _stateMachine = new StateMachine<BlockChainState, BlockChainTrigger>(BlockChainState.NULL);
            _engageTriggerStart = _stateMachine.SetTriggerParameters<long>(BlockChainTrigger.ConsensusNodesInitSynced);
            _engageTriggerConsolidateFailed = _stateMachine.SetTriggerParameters<string>(BlockChainTrigger.LocalNodeOutOfSync);
            if(_hostEnv != null)    // HACK: support unittest
                CreateStateMachine();

            var timr = new System.Timers.Timer(200);
            if(_hostEnv != null)
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
            // hack for unit test
            if (Settings.Default.LyraNode.Lyra.NetworkId == "xtest")
                _localNode = null;
        }

        private async Task BeginChangeViewAsync(string sender, ViewChangeReason reason)
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

        private bool InDBCC = false;
        private async Task<bool> DBCCAsync()
        {
            if (InDBCC)
                return true;

            InDBCC = true;
            try
            {
                _log.LogInformation($"Database consistent check... It may take a while.");

                var client = new LyraAggregatedClient(Settings.Default.LyraNode.Lyra.NetworkId, false, _sys.PosWallet.AccountId);
                await client.InitAsync();

                var lastCons = await _sys.Storage.GetLastConsolidationBlockAsync();
                var fixedHeight = lastCons?.Height ?? 0;
                var shouldReset = false;

                var localSafeCons = LocalDbSyncState.Load().lastVerifiedConsHeight;
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
                        var consSyncResult = await SyncAndVerifyConsolidationBlockAsync(client, lastCons);
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
                if (string.IsNullOrEmpty(localState.svcGenHash) && localState.lastVerifiedConsHeight > 0)
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
                    // remove sync state if db is empty
                    var count = await _sys.Storage.GetBlockCountAsync();
                    if (0 == count)
                    {
                        LocalDbSyncState.Remove();
                    }

                    if (Neo.Settings.Default.LyraNode.Lyra.Mode == Data.Utils.NodeMode.Normal)
                        await DeclareConsensusNodeAsync();

                    await InitJobSchedulerAsync();
                    BrokerFactory.Load(_sys.Storage);
                    BrokerFactory.OnFinished += (bp) =>
                    {
                        if(IsThisNodeLeader && bp.brokerAccount != null)
                        {
                            // queued tasks, which has same brokeraccount
                            var blueprints = BrokerFactory.GetAllBlueprints();
                            var bpx = blueprints
                                        .OrderBy(a => a.start)
                                        .Where(a => a.brokerAccount == bp.brokerAccount)
                                        .FirstOrDefault();

                            if (bpx != null)
                            {
                                _log.LogInformation($"BrokerFactory.OnFinished try next in queue {bpx.svcReqHash} for brk: {bp.brokerAccount}");
                                //ExecuteBlueprint(bpx, "OnFinished Leader");
                            }                            
                        }
                    };

                    do
                    {
                        try
                        {
                            _log.LogInformation($"Consensus Service Startup... ");
                            
                            await DeclareConsensusNodeAsync();  // important for cold start

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
                            }

                            // DBCC
                            if (!await DBCCAsync())
                                continue;

                            while (true)
                            {
                                try
                                {
                                    var client = new LyraAggregatedClient(Settings.Default.LyraNode.Lyra.NetworkId, false, _sys.PosWallet.AccountId);
                                    await client.InitAsync();

                                    var result = await client.GetLastServiceBlockAsync();
                                    if (result.ResultCode == APIResultCodes.Success)
                                    {
                                        lsb = result.GetBlock() as ServiceBlock;
                                    }
                                    else if (result.ResultCode == APIResultCodes.APIRouteFailed)
                                    {
                                        client.ReBase(true);
                                        await client.InitAsync();
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
                        try
                        {
                            var n = new Random().Next(1, 4).ToString();
                            var host = $"seed{n}.{Settings.Default.LyraNode.Lyra.NetworkId}.lyra.live";

                            if (await Shared.GetPublicIPAddress.IsThisHostMeAsync(host))
                            {
                                // self
                                await Task.Delay(1000);
                                continue;
                            }

                            ushort peerPort = 4504;
                            if (Settings.Default.LyraNode.Lyra.NetworkId == "mainnet")
                                peerPort = 5504;

                            var client = new LyraRestClient("", "", "", $"https://{host}:{peerPort}/api/Node/");
                            //var client = new LyraAggregatedClient(Settings.Default.LyraNode.Lyra.NetworkId, false);
                            //await client.InitAsync();

                            // when static sync, only query the seed nodes.
                            // three seeds are enough for database sync.
                            _log.LogInformation($"Querying Lyra Network Status... ");

                            var networkStatus = await client.GetSyncStateAsync();

                            if (networkStatus.ResultCode == APIResultCodes.APIRouteFailed)
                            {
                                //client.ReBase(true);
                                continue;
                            }

                            if (networkStatus.ResultCode != APIResultCodes.Success)
                            {
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
                                    while (!await SyncDatabaseAsync(client))
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
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                while (true)
                                {
                                    await EngagingSyncAsync();

                                    // check block count
                                    // if total block count is not consistant according to majority of nodes, there must be a damage.
                                    var client = new LyraAggregatedClient(Settings.Default.LyraNode.Lyra.NetworkId, false, _sys.PosWallet.AccountId);
                                    await client.InitAsync();
                                    var syncstate = await client.GetSyncStateAsync();
                                    var mysyncstate = await GetNodeStatusAsync();
                                    if(syncstate.Successful())
                                    {
                                        if (syncstate.Status.totalBlockCount == mysyncstate.totalBlockCount)
                                            break;
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
                                    localState.lastVerifiedConsHeight = 1;
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
                    var lsb = await _sys.Storage.GetLastServiceBlockAsync();
                    _viewChangeHandler.ShiftView(lsb.Height + 1);
                }).ConfigureAwait(false))
                .Permit(BlockChainTrigger.LocalNodeOutOfSync, BlockChainState.Engaging)         // make a quick recovery
                .Permit(BlockChainTrigger.LocalNodeMissingBlock, BlockChainState.Engaging);

            _stateMachine.OnTransitioned(t =>
            {
                _sys.UpdateConsensusState(t.Destination);
                _log.LogWarning($"OnTransitioned: {t.Source} -> {t.Destination} via {t.Trigger}({string.Join(", ", t.Parameters)})");
            });
        }

        public int GetQualifiedNodeCount(List<ActiveNode> allNodes)
        {
            var count = allNodes.Count();
            if (count > LyraGlobal.MAXIMUM_VOTER_NODES)
            {
                return LyraGlobal.MAXIMUM_VOTER_NODES;
            }
            else if (count < LyraGlobal.MINIMUM_AUTHORIZERS)
            {
                return LyraGlobal.MINIMUM_AUTHORIZERS;
            }
            else
            {
                return count;
            }
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

            //            var lastSb = await _sys.Storage.GetLastServiceBlockAsync();

            // TODO: filter auth signatures
            var list = Board.ActiveNodes.ToList()   // make sure it not changed any more
                                                    //.Where(x => Board.NodeAddresses.Keys.Contains(x.AccountID)) // filter bad ips
                //.Where(x => !_failedLeaders.Keys.Contains(x.AccountID))    // exclude failed leaders ! no, failed leader can still vote.
                .Where(a => a.Votes >= LyraGlobal.MinimalAuthorizerBalance && (a.State == BlockChainState.Engaging || a.State == BlockChainState.Almighty))
                //                .Where(s => Signatures.VerifyAccountSignature(lastSb.Hash, s.AccountID, s.AuthorizerSignature))
                .OrderByDescending(a => a.Votes)
                .ThenBy(a => a.AccountID)
                .ToList();

            var list2 = list.Take(GetQualifiedNodeCount(list))
                .Select(a => a.AccountID)
                .ToList();
            return list2;
        }

        public void UpdateVoters()
        {
            //_log.LogInformation("UpdateVoters begin...");
            RefreshAllNodesVotes();
            var list = GetQualifiedVoters();
            if (list.Count >= 4)        // simple check. but real condition is complex.
                Board.AllVoters = list;
            else
            {
                var s = "voters count < 4. network outtage happened. trying to resync";
                LocalConsolidationFailed(null);
                _log.LogError(s);
                //throw new InvalidOperationException(s);
            }
            //_log.LogInformation("UpdateVoters ended.");
        }

        internal async Task CheckCreateNewViewAsync()
        {
            //_log.LogInformation($"Checking new player(s)...");
            if (CurrentState != BlockChainState.Almighty)
            {
                return;
            }

            var cons = await _sys.Storage.GetLastConsolidationBlockAsync();
            var lsb = await _sys.Storage.GetLastServiceBlockAsync();

            var list1 = lsb.Authorizers.Keys.ToList();
            UpdateVoters();
            var list2 = Board.AllVoters;

            var firstNotSecond = list1.Except(list2).ToList();
            var secondNotFirst = list2.Except(list1).ToList();

            var reason = ViewChangeReason.None;

            if (firstNotSecond.Any() || secondNotFirst.Any())
            {
                //_log.LogInformation($"voter list is same as previous one.");
                reason = ViewChangeReason.PlayerJoinAndLeft;
            }
            else if(lsb.TimeStamp.AddHours(4) < DateTime.UtcNow)
            {
                reason = ViewChangeReason.ViewTimeout;
            }

            //_log.LogInformation($"We have new player(s). Change view...");
            // should change view for new member
            if(reason != ViewChangeReason.None)
                await BeginChangeViewAsync("View Monitor", reason);
        }

        internal void ServiceBlockCreated(ServiceBlock sb)
        {
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
                avgTime = g.Value.Sum(t => (decimal)t.MS) / g.Value.Count(),
                maxTime = g.Value.MaxBy(t => t.MS).MS,
                minTime = g.Value.MinBy(t => t.MS).MS
            })
             .OrderByDescending(b => b.times);
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
            //    Console.WriteLine("===\n" + json + "\n===");

            //    var jb = JsonConvert.SerializeObject(auth.Block);
            //    var blockx = JsonConvert.DeserializeObject<ProfitingGenesis>(jb);
            //    var v = blockx.VerifyHash();
            //    Console.WriteLine($"Test convert hash verify result: {v}");
            //    if(!v)
            //    {
            //        var jb2 = JsonConvert.SerializeObject(blockx);
            //        Console.WriteLine($"-----------\n{jb}\n\n{jb2}\n----------");
            //        Console.WriteLine($"+++++++++++\n{auth.Block.GetHashInput()}\n\n{blockx.GetHashInput()}\n+++++++++++");
            //    }
            //}
        }

        private async Task<ActiveNode> DeclareConsensusNodeAsync()
        {
            if (Settings.Default.LyraNode.Lyra.Mode == Data.Utils.NodeMode.App)
                return null;

            // declare to the network
            _myIpAddress = await GetPublicIPAddress.PublicIPAddressAsync();
            PosNode me = new PosNode(_sys.PosWallet.AccountId)
            {
                NodeVersion = LyraGlobal.NODE_VERSION.ToString(),
                ThumbPrint = _hostEnv?.GetThumbPrint(),
                IPAddress = $"{_myIpAddress}",
                //PftAccountID = Settings.Default.LyraNode.Lyra.ProfitAccountID
            };

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
            await OnNodeActiveAsync(me.AccountID, me.AuthorizerSignature, _stateMachine.State, _myIpAddress.ToString(), me.ThumbPrint);

            return _board.ActiveNodes.FirstOrDefault(a => a.AccountID == me.AccountID);
        }

        private async Task OnHeartBeatAsync(HeartBeatMessage heartBeat)
        {
            // dq any lower version
            var ver = new Version(heartBeat.NodeVersion);
            if (string.IsNullOrWhiteSpace(heartBeat.NodeVersion) || LyraGlobal.MINIMAL_COMPATIBLE_VERSION.CompareTo(ver) > 0)
            {
                //_log.LogInformation($"Node {heartBeat.From.Shorten()} ver {heartBeat.NodeVersion} is too old. Need at least {LyraGlobal.MINIMAL_COMPATIBLE_VERSION}");
                return;
            }

            await OnNodeActiveAsync(heartBeat.From, heartBeat.AuthorizerSignature, heartBeat.State, heartBeat.PublicIP, null);
        }

        private async Task OnNodeActiveAsync(string accountId, string authSign, BlockChainState state, string ip, string thumbPrint)
        {
            var lastSb = await _sys.Storage.GetLastServiceBlockAsync();
            var signAgainst = lastSb?.Hash ?? ProtocolSettings.Default.StandbyValidators[0];

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

                if(node.ProfitingAccountId == null)
                {
                    var pfts = await _sys.Storage.FindAllProfitingAccountForOwnerAsync(accountId);
                    var pft = pfts.Where(a => a.PType == Blocks.ProfitingType.Node)
                        .FirstOrDefault();

                    if (pft != null)
                        node.ProfitingAccountId = pft.AccountID;
                }
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

                var pfts = await _sys.Storage.FindAllProfitingAccountForOwnerAsync(accountId);
                var pft = pfts.Where(a => a.PType == Blocks.ProfitingType.Node)
                    .FirstOrDefault();

                if (pft != null)
                    node.ProfitingAccountId = pft.AccountID;

                _board.ActiveNodes.Add(node);
            }

            if (!string.IsNullOrWhiteSpace(ip))
            {
                if (System.Net.IPAddress.TryParse(ip, out System.Net.IPAddress addr))
                {
                    // one IP, one account id
                    var safeIp = addr.ToString();

                    // temp code. make it compatible.
                    if (true)//_verifiedIP.ContainsKey(safeIp))
                    {
                        var existingIP = _board.NodeAddresses.Where(x => x.Value == safeIp).ToList();
                        foreach (var exip in existingIP)
                        {
                            _board.NodeAddresses.TryRemove(exip.Key, out _);
                        }

                        _board.NodeAddresses.AddOrUpdate(accountId, ip, (key, oldValue) => ip);
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
                _log.LogWarning($"Hearbeat from {accountId.Shorten()} has no IP {ip}");
            }

            var deadList = _board.ActiveNodes.Where(a => a.LastActive < DateTime.Now.AddSeconds(-60)).ToList();
            foreach (var n in deadList)
                _board.NodeAddresses.TryRemove(n.AccountID, out _);
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

            if (_board.ActiveNodes.ToArray().Any(a => a.AccountID == _sys.PosWallet.AccountId))
                _board.ActiveNodes.First(a => a.AccountID == _sys.PosWallet.AccountId).LastActive = DateTime.Now;

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

                await OnNodeActiveAsync(node.AccountID, node.AuthorizerSignature, BlockChainState.Almighty, node.IPAddress, node.ThumbPrint);
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

        private async Task ConsolidateBlocksAsync()
        {
            if (_stateMachine.State != BlockChainState.Almighty
                && _stateMachine.State != BlockChainState.Engaging)
                return;

            // avoid hammer
            if (DateTime.UtcNow - _lastConsolidateTry < TimeSpan.FromSeconds(1))
                return;

            _lastConsolidateTry = DateTime.UtcNow;

            // check if there are pending consolidate blocks
            var pendingCons = _activeConsensus.Values
                .Where(a =>
                    a.State != null
                    && a.State.IsSourceValid
                    && a.State.InputMsg.Block is ConsolidationBlock
                    && a.Status != ConsensusWorker.ConsensusWorkerStatus.Commited);

            if (pendingCons.Any())
                return;

            // avoid racing condition (commited but not saved)
            if (pendingCons.Any(a => !a.State.IsSaved))
                return;

            try
            {
                var lastCons = await _sys.Storage.GetLastConsolidationBlockAsync();
                if(pendingCons.Any(x => x.State.IsSaved && x.State.InputMsg.Block.Height > lastCons.Height))
                {
                    _log.LogWarning($"Racing condition: pending cons height > last cons");
                    return;
                }


                // consolidate time from lastcons to now - 18s

                var timeShift = -18;
                var timeNow = IsThisNodeLeader ? DateTime.UtcNow : DateTime.UtcNow.AddSeconds(-1 * LyraGlobal.CONSENSUS_TIMEOUT);

                var timeStamp = timeNow.AddSeconds(timeShift);
                var unConsList = await _sys.Storage.GetBlockHashesByTimeRangeAsync(lastCons.TimeStamp, timeStamp);

                // if 1 it must be previous consolidation block.
                if (unConsList.Count() >= 10 || (unConsList.Count() > 1 && timeNow - lastCons.TimeStamp > TimeSpan.FromMinutes(10)))
                {
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
                                // leader may be faulty
                                var lsp = await _sys.Storage.GetLastServiceBlockAsync();
                                if(lsp.TimeStamp > DateTime.UtcNow.AddSeconds(-10))
                                    await BeginChangeViewAsync("cons blk monitor", ViewChangeReason.LeaderFailedConsolidating);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.LogError($"In creating consolidation block: {ex.Message}");
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

            var state = CreateAuthringState(msg, true);

            await SubmitToConsensusAsync(state);

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
                        x.State.IsCommited,
                        x.State.CommitConsensus,
                        tx = x.State.InputMsg.Block as TransactionBlock
                    })
                    .Where(a => !a.IsCommited && a.tx.AccountID == tx.AccountID && a.tx.Height == tx.Height)
                    .FirstOrDefault();

                if(doubleSpend != null)
                {
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

        private async Task SubmitToConsensusAsync(AuthState state)
        {
            // unit test support
            if(Settings.Default.LyraNode.Lyra.NetworkId == "xtest")
            {
                await OnNewBlock(state.InputMsg.Block);
                return;
            }
            if (IsBlockInQueue(state.InputMsg?.Block))
            {
                throw new Exception("Block is already in queue.");
            }

            Send2P2pNetwork(state.InputMsg);

            var worker = await GetWorkerAsync(state.InputMsg.Block.Hash);
            if (worker != null)
            {
                await worker.ProcessStateAsync(state);
            }            
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

        //public APIResultCodes AddSvcQueue(SendTransferBlock send)
        //{
        //    if (!_svcQueue.CanAdd(send.DestinationAccountId))
        //        return APIResultCodes.ReQuotaNeeded;

        //    _svcQueue.Add(send.DestinationAccountId, send.Hash);
        //    return APIResultCodes.Success;
        //}

        public async Task Worker_OnConsensusSuccess(Block block, ConsensusResult? result, bool localIsGood)
        {
            if(result != ConsensusResult.Uncertain)
                _successBlockCount++;

            _log.LogInformation($"Worker_OnConsensusSuccess {block.Hash.Shorten()} {block.BlockType} {block.Height} result: {result} local is good: {localIsGood}");

            // no, don't remove so quick. we will still receive message related to it.
            // should be better solution for high tps to avoid queue increase too big.
            // tps 100 * timeout 20s = 2k buffer, sounds we can handle it.
            //_activeConsensus.TryRemove(block.Hash, out _);
            _log.LogInformation($"Finished consensus: {_successBlockCount} Active Consensus: {_activeConsensus.Count}");

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

                    if(IsThisNodeLeader)
                    {
                        _ = Task.Run(() => {
                            var blueprints = BrokerFactory.GetAllBlueprints();

                            foreach (var x in blueprints
                                .OrderBy(a => a.start)
                                .GroupBy(a => a.brokerAccount)
                                .Select(g => new
                                {
                                    brk = g.Key,
                                    bp = g.OrderBy(d => d.start).FirstOrDefault()
                                })
                                .ToArray())
                            {
                                //ExecuteBlueprint(x.bp, "New Elected Leader");
                            }
                        }).ConfigureAwait(false);
                    }

                    /*
                    if (IsThisNodeLeader)
                    {
                        // new leader. clean all the unfinished swap operations
                        _ = Task.Run(async () =>
                        {
                            // get all unsettled send to pool factory
                            // get all unsettled send to pools

                            var allLeaderTasks = _svcQueue.AllTx.OrderBy(x => x.TimeStamp).ToList();
                            _log.LogInformation($"This new leader is processing {allLeaderTasks.Count} leader tasks.");
                            foreach (var entry in allLeaderTasks)
                            {
                                if (entry.ReqRecvHash == null)
                                {
                                    // do receive
                                    var send = await _sys.Storage.FindBlockByHashAsync(entry.ReqSendHash) as SendTransferBlock;

                                    if (send == null)
                                    {
                                        // not valid?
                                        continue;
                                    }

                                    var recv = await _sys.Storage.FindBlockBySourceHashAsync(entry.ReqSendHash) as ReceiveTransferBlock;
                                    if (recv == null)
                                    {
                                        _log.LogInformation($"One receive not finished for send {send.Hash}. processing...");
                                        ProcessSendBlock(send);
                                    }
                                    else
                                    {
                                        entry.FinishRecv(recv.Hash);

                                        //if ((entry is ServiceWithActionTx actx) && actx.ReplyActionHash == null)
                                        //{
                                        //    var block = await _sys.Storage.FindBlocksByRelatedTxAsync(recv.Hash);
                                        //    if (block == null)
                                        //    {
                                        //        _log.LogInformation($"One action not finished for recv {recv.Hash}. processing...");
                                        //        //ProcessManagedBlock(block, ConsensusResult.Yea);
                                        //    }
                                        //    else
                                        //    {
                                        //        entry.FinishAction(block.Hash);
                                        //    }
                                        //}
                                    }
                                }
                            }

                            _svcQueue.Clean();
                            allLeaderTasks = _svcQueue.AllTx.OrderBy(x => x.TimeStamp).ToList();
                            _log.LogInformation($"This new leader still have {allLeaderTasks.Count} leader tasks in queue.");
                        }).ConfigureAwait(false);
                    }*/
                }

                return;
            }
            else if (block is ConsolidationBlock cons)
            {
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

            //if (block is SendTransferBlock send &&
            //    send.Tags != null &&
            //    send.Tags.ContainsKey(Block.REQSERVICETAG))
            if (block is SendTransferBlock send)
                await ProcessServerReqBlockAsync(send, result);

            if (block.Tags != null && block.Tags.ContainsKey(Block.MANAGEDTAG))
                ProcessManagedBlock(block as TransactionBlock, result);
        }

        public async Task<object> GetBlockForRelatedTx(string reltx)
        {
            var wfhost = _hostEnv.GetWorkflowHost();

            var Id = _workFlows[reltx];
            var wf = await wfhost.PersistenceStore.GetWorkflowInstance(Id);
            var ctx = wf.Data as LyraContext;
            // wait for 2s for block
            int count = 200;
            while (count-- > 0 && ctx.LastBlock == null && ctx.State == WFState.Running)
                await Task.Delay(10);
            return ctx.LastBlock;
        }

        public string GetHashForWorkflow(string id)
        {
            return _workFlows.FirstOrDefault(a => a.Value == id).Key;
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
                var wfhost = _hostEnv.GetWorkflowHost();
                var ctx = new LyraContext
                {
                    SendBlock = send,
                    SubWorkflow = BrokerFactory.DynWorkFlows[svcreqtag],
                    State = WFState.Init,
                };
                var id = await wfhost.StartWorkflow(svcreqtag, ctx);
                _workFlows.AddOrUpdate(send.Hash, id, (key, oldid) => id);

                //// get broker account
                //var brkaccount = BrokerFactory.GetBrokerAccountID(send);

                //var bps = BrokerFactory.GetAllBlueprints();
                //var curbrks = bps.Where(a => a.brokerAccount == brkaccount).ToList();

                //// create a blueprint for workflow
                //var blueprint = new BrokerBlueprint
                //{
                //    view = _currentView,
                //    start = send.TimeStamp,
                //    initiatorAccount = send.AccountID,
                //    brokerAccount = brkaccount,
                //    svcReqHash = send.Hash,
                //    action = action,
                //    preDone = false,
                //    mainDone = false,
                //    extraDone = false
                //};
                //BrokerFactory.CreateBlueprint(blueprint);

                //if(IsThisNodeLeader)
                //{
                //    // if same broker account, then don't run, let it wait in queue.
                //    if (brkaccount != null && curbrks.Any())
                //    {
                //        _log.LogInformation($"Brk acct {brkaccount.Shorten()} exists in queue: {curbrks.Count}");
                //        return;
                //    }

                //    _log.LogInformation($"start process broker request {blueprint.svcReqHash}");
                //    //ExecuteBlueprint(blueprint, "Leader ProcessServerReqBlock");

                //    var wfhost = _hostEnv.GetWorkflowHost();
                //    var id = await wfhost.StartWorkflow("DebiMain", blueprint);
                //}
            }
        }

        public async Task<bool> CheckFinishedAsync(BrokerBlueprint bp)
        {
            //return await bp.ExecuteAsync(_sys, (b) => Task.CompletedTask, "CheckFinishedAsync");
            return false;
        }

        public async void ExecuteBlueprintx(BrokerBlueprint bp, string caller)
        {
            var wfhost = _hostEnv.GetWorkflowHost();

            wfhost.PublishEvent("", "", "");
            var id = await wfhost.StartWorkflow<BrokerBlueprint>("DebiMain", bp);
            var x = await wfhost.PersistenceStore.GetWorkflowInstance(id);
            

            if (_pfTaskMutex.Wait(1))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        _log.LogInformation($"Executing blueprints: process {bp.svcReqHash} ...");

                        bool success = false;
                        // hack for unit test
                        if (_hostEnv == null)
                        {
                            success = await bp.ExecuteAsync(_sys, (b) => OnNewBlock(b), caller);
                            _log.LogInformation($"broker request {bp.svcReqHash} result: {success}");
                            if (success)
                                BrokerFactory.RemoveBlueprint(bp.svcReqHash);
                        }
                        else
                        {
                            _log.LogInformation($"Begin executing blueprints...");
                            if (IsThisNodeLeader)
                            {
                                success = await bp.ExecuteAsync(_sys, async (b) => await SendBlockToConsensusAndForgetAsync(b), caller + " Leader");
                            }
                            else   // give normal nodes a chance to clear the queue
                                success = await bp.ExecuteAsync(_sys, async (b) => await Task.CompletedTask, caller + " normal node");
                            _log.LogInformation($"SVC request {bp.svcReqHash} executing result: {success}");
                        }

                        if (success)
                            BrokerFactory.RemoveBlueprint(bp.svcReqHash);
                        else
                        {
                            BrokerFactory.UpdateBlueprint(bp);
                        }
                    }
                    catch (Exception e)
                    {
                        var ms = "Error Executing blueprints: " + e.ToString();
                        Console.WriteLine(ms);
                        _log.LogError(ms);
                    }
                    finally
                    {
                        _log.LogInformation("Executing blueprints Done.");
                        _pfTaskMutex.Release();
                    }
                }).ConfigureAwait(false);
            }
        }

        public async Task ProcessManagedBlock(TransactionBlock block, ConsensusResult? result)
        {
            // find the key
            string key = null;
            
            if(block is IPool pool)
            {
                key = pool.RelatedTx;
            }
            else if(block is IBrokerAccount ib)
            {
                key = ib.RelatedTx;
            }
            else if(block is ReceiveTransferBlock recv)
            {
                key = recv.SourceHash;
                //if (block.AccountID == PoolFactoryBlock.FactoryAccount)
            }
            // add token gateway, merchant etc.

            if (key == null)
                return;

            var wfhost = _hostEnv.GetWorkflowHost();
            Console.WriteLine($"Key is {key} Publish Consensus event {result} ");
            await wfhost.PublishEvent("Consensus", key, result);
            return;

            var bp = BrokerFactory.GetBlueprint(key);
            if (bp == null)
                return;

            if(result == ConsensusResult.Nay)
            {
                // PD recv never fail, it just retry again
                if(block is ReceiveTransferBlock recv && recv.AccountID == PoolFactoryBlock.FactoryAccount)
                {
                    _log.LogWarning($"PoolFactory receive Nay. retry... RelatedTx: {key}");
                }
                else
                {
                    // process Nay
                    _log.LogCritical($"Fatal Error ProcessManagedBlock! RelatedTx: {key}");
                    BrokerFactory.RemoveBlueprint(key);
                }
            }

            if (!bp.FullDone)
            {
                _ = Task.Run(async () =>
                {
                    // hack for unit test
                    if (_hostEnv == null)
                    {
                        await Task.Delay(10);
                    }
                    // debug unit test. force execute when unit test debug
                    if (_hostEnv == null || _pfTaskMutex.Wait(1))
                    {
                        try
                        {
                            bool success = false;
                            if (IsThisNodeLeader)
                            {
                                if (_hostEnv == null)
                                {
                                    success = await bp.ExecuteAsync(_sys, (b) => OnNewBlock(b), "ProcessManagedBlock");
                                }
                                else
                                    success = await bp.ExecuteAsync(_sys, async (b) => await SendBlockToConsensusAndForgetAsync(b), "ProcessManagedBlock");
                            }
                            else   // give normal nodes a chance to clear the queue
                                success = await bp.ExecuteAsync(_sys, async (b) => await Task.CompletedTask, "ProcessManagedBlock");
                            _log.LogInformation($"broker request {bp.svcReqHash} result: {success}");
                            if (success)
                                BrokerFactory.RemoveBlueprint(bp.svcReqHash);
                            else
                            {
                                BrokerFactory.UpdateBlueprint(bp);
                            }
                        }
                        catch (Exception e)
                        {
                            var err = "Error Executing blueprints: " + e.ToString();
                            Console.WriteLine(err);
                            _log.LogError(err);
                        }
                        finally
                        {
                            _log.LogInformation("Executing blueprints Done.");
                            _pfTaskMutex.Release();
                        }
                    }
                }).ConfigureAwait(false);
            }
            else
            {
                BrokerFactory.RemoveBlueprint(key);
            }
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

            if (item is ViewChangeMessage vcm && _viewChangeHandler != null)
            {
                // need to listen to any view change event.
                if (/*_viewChangeHandler.IsViewChanging && */(CurrentState == BlockChainState.Engaging || CurrentState == BlockChainState.Almighty) && Board.ActiveNodes.Any(a => a.AccountID == vcm.From))
                {
                    await _viewChangeHandler.ProcessMessageAsync(vcm);
                }
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
                    await OnHeartBeatAsync(chat as HeartBeatMessage);
                    break;
                case ChatMessageType.NodeUp:
                    await Task.Run(async () => { await OnNodeUpAsync(chat); }).ConfigureAwait(false);
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

        private readonly Mutex _locker = new Mutex(false);
        public void RefreshAllNodesVotes()
        {
            try
            {
                _locker.WaitOne();
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

                    // TODO: new cal. remove old one after full upgrade
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
                _locker.ReleaseMutex();
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
