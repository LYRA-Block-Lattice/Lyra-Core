﻿using Akka.Actor;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using Lyra.Core.Decentralize;
using Lyra.Core.Utils;
using Lyra.Shared;
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
    public class ConsensusWorker : ConsensusHandlerBase
    {
        private AuthorizersFactory _authorizers;

        AuthState _state;

        public string Hash { get; }
        public AuthState State { get => _state as AuthState; set => _state = value; }

        public ConsensusWorker(ConsensusService context, string hash) : base (context)
        {
            _authorizers = new AuthorizersFactory();
            TimeStarted = DateTime.Now;
            Hash = hash;
        }

        public async Task ProcessState(AuthState state)
        {
            _state = state;
            await ProcessMessage(state.InputMsg);
        }

        protected override bool IsStateCreated()
        {
            return _state != null;
        }
        protected override async Task InternalProcessMessage(ConsensusMessage msg)
        {
            bool sourceValid = true;
            if(msg is BlockConsensusMessage bmsg)
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
                else if(bmsg is AuthorizingMsg am && am.Block.BlockType == BlockTypes.Consolidation)
                {
                    if (_context.Board.CurrentLeader != bmsg.From)
                    {
                        _log.LogWarning($"Consolidation block not from current leader {_context.Board.CurrentLeader.Shorten()} but from {bmsg.From.Shorten()}");
                        sourceValid = false;
                    }
                }                
                else if (!(bmsg is AuthorizingMsg))     // allow authorizingmsg from anywhere
                {
                    if(!_context.Board.PrimaryAuthorizers.Contains(bmsg.From))
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

        private void OnPrePrepare(AuthorizingMsg msg, bool sourceValid)
        {
            _log.LogInformation($"Receive AuthorizingMsg: {msg.Block.Height}/{msg.Block.Hash} from {msg.From.Shorten()}");
            //_context.OnNodeActive(_context.GetDagSystem().PosWallet.AccountId);     // update billboard

            if (msg.Version != LyraGlobal.ProtocolVersion)
            {
                return;
            }

            // first try auth locally
            if (_state == null)
                _state = _context.CreateAuthringState(msg, sourceValid);

            _state.SetView(msg.IsServiceBlock ? _context.Board.AllVoters : _context.Board.PrimaryAuthorizers);

            // if source is invalid, we just listen to the network. 
            // we need to detect whether this node is out of sync now.
            if(sourceValid)
            {
                _ = Task.Run(async () =>
                {
                    //if (waitHandle != null)
                    //    await waitHandle.AsTask();

                    await AuthorizeAsync(msg);
                });
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

            _log.LogInformation($"OnCommit: For block {_state.InputMsg.Block.Height}/{_state.InputMsg.Block.Hash.Shorten()}. Commit {_state.CommitMsgs.Count}/{_state.WinNumber} From {item.From.Shorten()}");

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

            if(State is ServiceBlockAuthState sbas)
            {
                // if no leader elected, this will fail.
                int waited = 0;
                while (_context.Board.LeaderCandidate == null && waited < 5000)
                {
                    await Task.Delay(100);
                }

                _log.LogInformation($"After waiting, LeaderCandidate is {_context.Board.LeaderCandidate?.Shorten()}");
            }

            if(errCode != APIResultCodes.Success)
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
            var authorizer = _authorizers.Create(item.Block.BlockType);

            AuthorizedMsg result;
            try
            {
                var (localAuthResult, localAuthSign) = await authorizer.AuthorizeAsync(_context.GetDagSystem(), item.Block);

                // process service required send
                if(localAuthResult == APIResultCodes.Success
                    && item.Block is SendTransferBlock send
                    && send.Tags?.ContainsKey(Block.REQSERVICETAG) == true)
                {
                    localAuthResult = _context.AddSvcQueue(send);
                    if (localAuthResult != APIResultCodes.Success)
                        localAuthSign = null;       // destroy it
                }

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
                _log.LogError($"LocalAuthorizingAsync takes {stopwatch.ElapsedMilliseconds} ms with {result.Result}");
                _log.LogInformation($"LocalAuthorizingAsync state: {_state.InputMsg.Block.Height}/{_state.InputMsg.Block.Hash}");
            }

            return result;
        }

        protected virtual async Task AuthorizeAsync(AuthorizingMsg msg)
        {
            var localAuthResult = await LocalAuthorizingAsync(msg);
            State.LocalResult = localAuthResult;
            //_log.LogInformation($"AuthorizeAsync: done auth. _state is null? {_state == null}");

            // we still need local authorizer to make sure database is synced (via consolidation block)
            if (Neo.Settings.Default.LyraNode.Lyra.Mode == Data.Utils.NodeMode.Normal)
            {
                await _context.Send2P2pNetworkAsync(localAuthResult);

                var ok = _state.AddAuthResult(localAuthResult);
                if (!ok)
                    _log.LogError($"AuthorizeAsync: result not added ok.");
            }

            await CheckAuthorizedAllOkAsync(_context.GetDagSystem().PosWallet.AccountId);
        }

        private async Task CheckAuthorizedAllOkAsync(string from)
        {
            await ProcessQueueAsync();
            // check state
            // debug: show all states
            _log.LogInformation($"Consensus Result: {_state.OutputMsgs.Count}/{_state.WinNumber} from {from.Shorten()}");

            if (_state.OutputMsgs.Count < _state.WinNumber)
            {
                return;
            }
            await _state.Semaphore.WaitAsync();
            try
            {
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

                _log.LogInformation($"_state.Consensus is {_state.PrepareConsensus}");
                if (ConsensusResult.Uncertain != _state.PrepareConsensus)
                {

                    _log.LogInformation($"got Semaphore. is it saving? {_state.Saving}");

                    if (_state.Saving)
                        return;

                    _state.Saving = true;

                    _state.T4 = DateTime.Now;

                    _log.LogInformation($"Saving {_state.PrepareConsensus}: {_state.InputMsg.Block.Height}/{_state.InputMsg.Block.Hash}");

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

                    await _context.Send2P2pNetworkAsync(msg);
                    _state.AddCommitedResult(msg);
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"CheckAuthorizedAllOkAsync: {ex.ToString()}");
            }
            finally
            {
                _state.Semaphore.Release();
            }
        }

        private async Task CheckCommitedOKAsync()
        {
            var block = _state.InputMsg?.Block;
            if (block == null)
            // debug 
            {
                _log.LogWarning($"Block is null");
                return;
            }
            //

            if (_state.CommitConsensus == ConsensusResult.Yea)
            {
                if (!await _context.GetDagSystem().Storage.AddBlockAsync(block))
                    _log.LogWarning($"Block Save Failed Index: {block.Height}");
                else
                {
                    _log.LogInformation($"Block saved: {block.Height}/{block.Hash}");

                    // event hooks
                    var sys = _context.GetDagSystem();
                    sys.Consensus.Tell(new BlockChain.BlockAdded { NewBlock = block });
                }

                // if self result is Nay, need (re)send commited msg here
                var myResult = _state.OutputMsgs.FirstOrDefault(a => a.From == _context.GetDagSystem().PosWallet.AccountId);
                if(myResult == null || myResult.Result != APIResultCodes.Success)
                {
                    var msg = new AuthorizerCommitMsg
                    {
                        IsServiceBlock = State.InputMsg.IsServiceBlock,
                        From = _context.GetDagSystem().PosWallet.AccountId,
                        MsgType = ChatMessageType.AuthorizerCommit,
                        BlockHash = _state.InputMsg.Block.Hash,
                        Consensus = _state.PrepareConsensus
                    };

                    await _context.Send2P2pNetworkAsync(msg);
                }
            }
            else if (_state.CommitConsensus == ConsensusResult.Nay)
            {
                _log.LogWarning($"Block not saved because ConsensusResult is Nay: {block.Height}");
            }
            else
            {
                //_log.LogWarning($"Block not saved because ConsensusResult is Uncertain: {block.Height}");
                return;
            }

            _state.Close();
        }
    }
}
