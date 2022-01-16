using Akka.Actor;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using Lyra.Core.Decentralize;
using Lyra.Core.Utils;
using Lyra.Data.Shared;
using Microsoft.Extensions.Logging;
using Neo;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Lyra.Data.API;

namespace Lyra.Core.Decentralize
{
    public class ConsensusWorker : ConsensusHandlerBase, IDisposable
    {
        public enum ConsensusWorkerStatus
        {
            // init
            Idle, 
            /// <summary>
            /// in progress of consensus
            /// </summary>
            InAuthorizing,
            Commited,
            WaitForViewChanging
        }

        public ConsensusWorkerStatus Status; 

        private enum LocalAuthState { NotStarted, InProgress, Finished };
        private LocalAuthState _localAuthState = LocalAuthState.NotStarted;
        private AuthorizersFactory _authorizers;

        AuthState _state;
        public SemaphoreSlim _semaphore { get; }

        public string Hash { get; }
        public AuthState State { get => _state as AuthState; set => _state = value; }

        public ConsensusWorker(ConsensusService context, string hash) : base(context)
        {
            _authorizers = new AuthorizersFactory();
            Hash = hash;

            _semaphore = new SemaphoreSlim(1, 1);
        }

        public async Task ProcessStateAsync(AuthState state)
        {
            _state = state;
            await ProcessMessageAsync(state.InputMsg);
        }

        protected override bool IsStateCreated()
        {
            return _state != null;
        }
        protected override async Task InternalProcessMessageAsync(ConsensusMessage msg)
        {
            bool sourceValid = true;
            if (msg is BlockConsensusMessage bmsg)
            {
                if (bmsg is AuthorizingMsg svcB && svcB.Block.BlockType == BlockTypes.Service) // service block must come from the new elected leader
                {
                    if (_context.Board.LeaderCandidate != null && _context.Board.LeaderCandidate != bmsg.From)
                    {
                        _log.LogWarning($"Service block not from leader candidate {_context.Board.LeaderCandidate.Shorten()} but from {bmsg.From.Shorten()}");
                        sourceValid = false;
                    }
                }
                else if (bmsg.IsServiceBlock)
                {
                    if (!_context.Board.AllVoters.Contains(bmsg.From))
                    {
                        _log.LogWarning($"Service block auth msg not from AllVoters but from {bmsg.From.Shorten()}");
                        sourceValid = false;
                    }
                }
                else if (bmsg is AuthorizingMsg am && am.Block.BlockType == BlockTypes.Consolidation)
                {
                    if (_context.Board.CurrentLeader != bmsg.From)
                    {
                        _log.LogWarning($"Consolidation block not from current leader {_context.Board.CurrentLeader.Shorten()} but from {bmsg.From.Shorten()}");
                        sourceValid = false;
                    }
                }
                else if (!(bmsg is AuthorizingMsg))     // allow authorizingmsg from anywhere
                {
                    if (!_context.Board.PrimaryAuthorizers.Contains(bmsg.From))
                    {
                        return;
                    }
                }
            }

            switch (msg)
            {
                case AuthorizingMsg msg1:
                    OnPrePrepare(msg1, sourceValid);
                    break;
                case AuthorizedMsg msg2:
                    await OnPrepareAsync(msg2);
                    break;
                case AuthorizerCommitMsg msg3:
                    await OnCommitAsync(msg3);
                    break;
                default:
                    break;
            }
        }

        // for liveness when block failed we do view change,
        // after view change we need to redo the consensus process
        public void RedoBlockAuthorizing()
        {
            _log.LogInformation("In RedoBlockAuthorizing");
            ResetTimer();
            _state.Reset();
            _state.SetView(State.InputMsg.IsServiceBlock ? _context.Board.AllVoters : _context.Board.PrimaryAuthorizers);
            _localAuthState = LocalAuthState.NotStarted;
            OnPrePrepare(State.InputMsg, true);
        }

        private void OnPrePrepare(AuthorizingMsg msg, bool sourceValid)
        {
            try
            {
                _semaphore.Wait();

                _log.LogInformation($"Receive AuthorizingMsg: {msg.Block.Height}/{msg.Block.Hash} from {msg.From.Shorten()}");
                //_context.OnNodeActive(_context.GetDagSystem().PosWallet.AccountId);     // update billboard

                if (msg.Version != LyraGlobal.ProtocolVersion)
                {
                    return;
                }

                // first try auth locally
                if (_state == null)
                {
                    _state = _context.CreateAuthringState(msg, sourceValid);
                }

                // if source is invalid, we just listen to the network. 
                // we need to detect whether this node is out of sync now.
                if (sourceValid)
                {
                    if (_localAuthState == LocalAuthState.NotStarted)
                    {
                        _localAuthState = LocalAuthState.InProgress;
                        _ = Task.Run(async () =>
                        {
                            //if (waitHandle != null)
                            //    await waitHandle.AsTask();

                            await AuthorizeAsync(msg);
                            _localAuthState = LocalAuthState.Finished;
                        }).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task OnPrepareAsync(AuthorizedMsg item)
        {
            if (_state.T3 == default)
                _state.T3 = DateTime.Now;

            if (_state.AddAuthResult(item))
                await CheckAuthorizedAllOkAsync(item.From);
        }

        private async Task OnCommitAsync(AuthorizerCommitMsg item)
        {
            if (_state.T5 == default)
                _state.T5 = DateTime.Now;

            //if (_activeConsensus.ContainsKey(item.BlockHash))
            //{
            //    var state = _activeConsensus[item.BlockHash];
            bool committed_result = _state.AddCommitedResult(item);
            if (committed_result)
                await CheckCommitedOKAsync();

            //_log.LogInformation($"committed_result: {committed_result}");

            //_log.LogInformation($"OnCommit: For block {_state.InputMsg.Block.Height}/{_state.InputMsg.Block.Hash.Shorten()}. Commit {_state.CommitMsgs.Count}/{_state.WinNumber} From {item.From.Shorten()}");

            //_context.OnNodeActive(item.From);        // track latest activities via billboard
            //}
            //else
            //{
            //    // maybe outof ordered message
            //    if (_cleanedConsensus.ContainsKey(item.BlockHash))
            //    {
            //        return;
            //    }

            //    List<SourceSignedMessage> msgs;
            //    if (_outOfOrderedMessages.ContainsKey(item.BlockHash))
            //        msgs = _outOfOrderedMessages[item.BlockHash];
            //    else
            //    {
            //        msgs = new List<SourceSignedMessage>();
            //        msgs.Add(item);
            //    }

            //    msgs.Add(item);
            //}
        }

        private async Task<AuthorizedMsg> LocalAuthorizingAsync(AuthorizingMsg item)
        {
            var errCode = APIResultCodes.Success;

            if (State is ServiceBlockAuthState sbas)
            {
                // if no leader elected, this will fail.
                int waited = 0;
                while (_context.Board.LeaderCandidate == null && waited < 5000)
                {
                    await Task.Delay(100);
                }

                _log.LogInformation($"After waiting, LeaderCandidate is {_context.Board.LeaderCandidate?.Shorten()}");
            }

            if (errCode != APIResultCodes.Success)
            {
                var result0 = new AuthorizedMsg
                {
                    IsServiceBlock = State.InputMsg.IsServiceBlock,
                    From = _context.GetDagSystem().PosWallet.AccountId,
                    MsgType = ChatMessageType.AuthorizerPrepare,
                    BlockHash = item.Block.Hash,
                    Result = errCode
                };
                return result0;
            }

            var stopwatch = Stopwatch.StartNew();
            var authorizer = _authorizers.Create(item.Block);

            AuthorizedMsg result;
            try
            {
                var (localAuthResult, localAuthSign) = await authorizer.AuthorizeAsync(_context.GetDagSystem(), item.Block);

                //// process service required send
                //if (localAuthResult == APIResultCodes.Success
                //    && item.Block is SendTransferBlock send
                //    && send.Tags?.ContainsKey(Block.REQSERVICETAG) == true)
                //{
                //    localAuthResult = _context.AddSvcQueue(send);
                //    if (localAuthResult != APIResultCodes.Success)
                //        localAuthSign = null;       // destroy it
                //}

                result = new AuthorizedMsg
                {
                    IsServiceBlock = State.InputMsg.IsServiceBlock,
                    From = _context.GetDagSystem().PosWallet.AccountId,
                    MsgType = ChatMessageType.AuthorizerPrepare,
                    BlockHash = item.Block.Hash,
                    Result = localAuthResult,
                    AuthSign = localAuthSign
                };

                _log.LogInformation($"Index {item.Block.Height} of block {item.Block.Hash.Shorten()} of Type {item.Block.BlockType}");
            }
            catch (Exception e)
            {
                _log.LogWarning($"Consensus: LocalAuthorizingAsync Exception: {e.Message} BlockIndex: {item.Block.Height}");

                result = new AuthorizedMsg
                {
                    IsServiceBlock = State.InputMsg.IsServiceBlock,
                    From = _context.GetDagSystem().PosWallet.AccountId,
                    MsgType = ChatMessageType.AuthorizerPrepare,
                    BlockHash = item.Block.Hash,
                    Result = APIResultCodes.UnknownError,
                    AuthSign = null
                };
            }

            stopwatch.Stop();
            if (result.Result == APIResultCodes.Success)
                _log.LogInformation($"LocalAuthorizingAsync {item.Block.BlockType} takes {stopwatch.ElapsedMilliseconds} ms with {result.Result}");
            else
            {
                if (result.Result == APIResultCodes.CouldNotFindLatestBlock)
                {
                    _log.LogInformation($"CouldNotFindLatestBlock!! state: {_state.InputMsg.Block.Height}/{_state.InputMsg.Block.Hash} Previous Block Hash: {_state.InputMsg.Block.PreviousHash}");
                }
                _log.LogError($"LocalAuthorizingAsync {item.Block.BlockType} takes {stopwatch.ElapsedMilliseconds} ms with {result.Result}");
                _log.LogInformation($"LocalAuthorizingAsync state: {_state.InputMsg.Block.Height}/{_state.InputMsg.Block.Hash}");
            }

            return result;
        }

        protected virtual async Task AuthorizeAsync(AuthorizingMsg msg)
        {
            var localAuthResult = await LocalAuthorizingAsync(msg);
            // check if state has already closed. 
            State.LocalResult = localAuthResult;
            //_log.LogInformation($"AuthorizeAsync: done auth. _state is null? {_state == null}");

            var myAccountId = _context.GetDagSystem().PosWallet.AccountId;
            // we still need local authorizer to make sure database is synced (via consolidation block)
            if (Neo.Settings.Default.LyraNode.Lyra.Mode == Data.Utils.NodeMode.Normal
                && State.CheckSenderValid(myAccountId)
                )
            {
                _context.Send2P2pNetwork(localAuthResult);
            }
            await CheckAuthorizedAllOkAsync(myAccountId);
            await CheckCommitedOKAsync();
        }

        private async Task CheckAuthorizedAllOkAsync(string from)
        {
            await ProcessQueueAsync();
            // check state
            // debug: show all states
            //_log.LogInformation($"Consensus Result: {_state.OutputMsgs.Count}/{_state.WinNumber} from {from.Shorten()}");

            if (_state.LocalResult == null)
                return;     // we always wait for local result.

            if (_state.OutputMsgs.Count < _state.WinNumber)
            {
                return;
            }
            try
            {
                await _semaphore.WaitAsync();

                //_log.LogInformation($"_state.Consensus is {_state.PrepareConsensus}");
                if (ConsensusResult.Uncertain != _state.PrepareConsensus)
                {
                    //_log.LogInformation($"got Semaphore. is it saving? {_state.Saving}");

                    if (_state.Saving)
                        return;

                    _state.Saving = true;

                    _state.T4 = DateTime.Now;

                    //_log.LogInformation($"Saving {_state.PrepareConsensus}: {_state.InputMsg.Block.Height}/{_state.InputMsg.Block.Hash}");

                    var ts = DateTime.Now - _state.Created;
                    if (_context.Stats.Count > 10000)
                        _context.Stats.RemoveRange(0, 2000);

                    _context.Stats.Add(new TransStats { ms = (long)ts.TotalMilliseconds, trans = _state.InputMsg.Block.BlockType });

                    var block = _state.InputMsg.Block;

                    // do commit
                    //block.Authorizations = _state.OutputMsgs.Select(a => a.AuthSign).ToList();

                    var msg = new AuthorizerCommitMsg
                    {
                        IsServiceBlock = State.InputMsg.IsServiceBlock,
                        From = _context.GetDagSystem().PosWallet.AccountId,
                        MsgType = ChatMessageType.AuthorizerCommit,
                        BlockHash = _state.InputMsg.Block.Hash,
                        Consensus = _state.PrepareConsensus
                    };

                    _context.Send2P2pNetwork(msg);
                    _state.AddCommitedResult(msg);
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"CheckAuthorizedAllOkAsync: {ex.ToString()}");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task CheckCommitedOKAsync()
        {
            try
            {
                await _semaphore.WaitAsync();

                if (_state.LocalResult == null)
                    return;

                var block = _state.InputMsg?.Block;
                if (block == null)
                // debug 
                {
                    _log.LogWarning($"Block is null");
                    return;
                }
                //

                if (Status == ConsensusWorkerStatus.Commited)
                    return;

                if (_state.CommitConsensus == ConsensusResult.Yea)
                {
                    Status = ConsensusWorkerStatus.Commited;
                    if (!_state.IsSaved)
                    {
                        if (!await _context.GetDagSystem().Storage.AddBlockAsync(block))
                            _log.LogWarning($"Block Save Failed Index: {block.Height}");
                        else
                        {
                            _state.SetSaved();
                            _log.LogInformation($"Block saved: {block.Height}/{block.Hash}");

                            // event hooks
                            var sys = _context.GetDagSystem();
                            sys.Consensus.Tell(new BlockChain.BlockAdded { NewBlock = block });

                            var sb = new StringBuilder();
                            sb.AppendLine();
                            sb.AppendLine($"* Transaction From Node {_state.InputMsg.From.Shorten()} Type: {_state.InputMsg.Block.BlockType} Index: {_state.InputMsg.Block.Height} Hash: {_state.InputMsg.Block.Hash.Shorten()}");
                            foreach (var msg in _state.OutputMsgs.ToList())
                            {
                                var seed0 = msg.From == _context.Board.CurrentLeader ? "[Leader]" : "";
                                string me = "";
                                if (msg.From == _context.GetDagSystem().PosWallet.AccountId)
                                    me = "[me]";
                                var voice = msg.IsSuccess ? "Yea" : "Nay";
                                var canAuth = _state.CheckSenderValid(msg.From);// _currentView.Authorizers.Any(a => a.AccountID == msg.From);
                                sb.AppendLine($"{voice} {msg.Result} By: {msg.From.Shorten()} CanAuth: {canAuth} {seed0}{me}");
                            }
                            _log.LogInformation(sb.ToString());
                        }
                    }
                }
                else if (_state.CommitConsensus == ConsensusResult.Nay)
                {
                    Status = ConsensusWorkerStatus.Commited;
                    _log.LogWarning($"Block not saved because ConsensusResult is Nay: {block.Height}");
                }
                else
                {
                    //_log.LogWarning($"Block not saved because ConsensusResult is Uncertain: {block.Height}");
                    return;
                }

                await _state.CommitAsync();
                _log.LogInformation($"consensus commited. {block.Height}/{block.Hash} state close.");
            }
            catch(Exception ex)
            {
                _log.LogError($"CheckCommitedOKAsync: {ex.ToString()}");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        // To detect redundant calls
        private bool _disposed = false;

        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose() => Dispose(true);

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // Dispose managed state (managed objects).
                _semaphore?.Dispose();
            }

            _disposed = true;
        }
    }
}
