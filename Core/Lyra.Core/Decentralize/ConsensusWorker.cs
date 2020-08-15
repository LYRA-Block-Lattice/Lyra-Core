using Akka.Actor;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Cryptography;
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

namespace Lyra.Core.Decentralize
{
    public class ConsensusWorker : ConsensusHandlerBase
    {
        private AuthorizersFactory _authorizers;

        AuthState _state;
        ServiceBlock _currentView;

        public string Hash { get; }
        public AuthState State { get => _state as AuthState; set => _state = value; }

        public ConsensusWorker(ConsensusService context, string hash) : base (context)
        {
            _authorizers = new AuthorizersFactory();            

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
            if(_currentView != null)
            {
                if(!_currentView.Authorizers.Any(a => a.AccountID == msg.From))
                {
                    return;     // msg not from the view
                }
            }
            switch (msg)
            {
                case AuthorizingMsg msg1:
                    await OnPrePrepareAsync(msg1);
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

        private async Task OnPrePrepareAsync(AuthorizingMsg msg)
        {
            _log.LogInformation($"Receive AuthorizingMsg: {msg.Block.Height}/{msg.Block.Hash}");
            //_context.OnNodeActive(_context.GetDagSystem().PosWallet.AccountId);     // update billboard

            if (msg.Version != LyraGlobal.ProtocolVersion)
            {
                return;
            }

            // first try auth locally
            if (_state == null)
                _state = CreateAuthringState(msg);

            _currentView = await _context.GetDagSystem().Storage.FindBlockByHashAsync(_state.InputMsg.Block.ServiceHash) as ServiceBlock;
            _state.SetView(_currentView);

            //_context.Send2P2pNetwork(msg);
            _ = Task.Run(async () =>
            {
                //if (waitHandle != null)
                //    await waitHandle.AsTask();

                await AuthorizeAsync(msg);
            });
        }

        private async Task OnPrepareAsync(AuthorizedMsg item)
        {
            if (_state.T3 == default)
                _state.T3 = DateTime.Now;

            if (_state.AddAuthResult(item))
                await CheckAuthorizedAllOkAsync(_state);
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

            _log.LogInformation($"OnCommit: {_state.CommitMsgs.Count}/{_state.WinNumber} From {item.From.Shorten()}, {_state.InputMsg.Block.Height}/{_state.InputMsg.Block.Hash.Shorten()}");

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

        private AuthState CreateAuthringState(AuthorizingMsg item)
        {
            _log.LogInformation($"Consensus: CreateAuthringState Called: BlockIndex: {item.Block.Height}");

            var ukey = item.Block.Hash;
            //if (_activeConsensus.ContainsKey(ukey))
            //{
            //    return _activeConsensus[ukey];
            //}

            var state = new AuthState();
            state.InputMsg = item;

            //// add possible out of ordered messages belong to the block
            //if (_outOfOrderedMessages.ContainsKey(item.Block.Hash))
            //{
            //    var msgs = _outOfOrderedMessages[item.Block.Hash];
            //    _outOfOrderedMessages.Remove(item.Block.Hash);

            //    foreach (var msg in msgs)
            //    {
            //        switch (msg)
            //        {
            //            case AuthorizedMsg authorized:
            //                state.AddAuthResult(authorized);
            //                break;
            //            case AuthorizerCommitMsg committed:
            //                state.AddCommitedResult(committed);
            //                break;
            //        }
            //    }
            //}

            // check if block existing
            //if (null != _context.GetDagSystem().Storage.FindBlockByHash(item.Block.Hash))
            //{
            //    _log.LogInformation("CreateAuthringState: Block is already in database.");
            //    return null;
            //}

            // check if block was replaced by nulltrans
            //if (null != _context.GetDagSystem().Storage.FindNullTransBlockByHash(item.Block.Hash))
            //{
            //    _log.LogInformation("CreateAuthringState: Block is already consolidated by nulltrans.");
            //    return null;
            //}

            //_activeConsensus.Add(ukey, state);
            return state;
        }

        private async Task<AuthorizedMsg> LocalAuthorizingAsync(AuthorizingMsg item)
        {
            ////_log.LogInformation($"LocalAuthorizingAsync: {item.Block.BlockType} {item.Block.UIndex}/{item.Block.Index}/{item.Block.Hash}");
            var errCode = APIResultCodes.Success;
            //if (!ConsensusService.Board.CanDoConsensus)
            //{
            //    errCode = APIResultCodes.PBFTNetworkNotReadyForConsensus;
            //}
            //else if(!ConsensusService.AuthorizerShapshot.Contains(_context.GetDagSystem().PosWallet.AccountId))
            //{
            //    errCode = APIResultCodes.NotListedAsQualifiedAuthorizer;
            //}

            if(errCode != APIResultCodes.Success)
            {
                var result0 = new AuthorizedMsg
                {
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
                var localAuthResult = await authorizer.AuthorizeAsync(_context.GetDagSystem(), item.Block);
                result = new AuthorizedMsg
                {
                    From = _context.GetDagSystem().PosWallet.AccountId,
                    MsgType = ChatMessageType.AuthorizerPrepare,
                    BlockHash = item.Block.Hash,
                    Result = localAuthResult.Item1,
                    AuthSign = localAuthResult.Item2
                };

                _log.LogInformation($"Index {item.Block.Height} of block {item.Block.Hash.Shorten()} of Type {item.Block.BlockType}");
            }
            catch (Exception e)
            {
                _log.LogWarning($"Consensus: LocalAuthorizingAsync Exception: {e.Message} BlockIndex: {item.Block.Height}");

                result = new AuthorizedMsg
                {
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

        private async Task AuthorizeAsync(AuthorizingMsg msg)
        {
            var localAuthResult = await LocalAuthorizingAsync(msg);
            _log.LogInformation($"AuthorizeAsync: done auth. _state is null? {_state == null}");
            if(_state.AddAuthResult(localAuthResult))
            {
                _context.Send2P2pNetwork(localAuthResult);
                await CheckAuthorizedAllOkAsync(_state);
            }
        }

        private async Task CheckAuthorizedAllOkAsync(AuthState state)
        {
            await ProcessQueueAsync();
            // check state
            // debug: show all states
            _log.LogInformation($"Consensus Result: {state.OutputMsgs.Count}/{state.WinNumber} {state.OutputMsgs.Select(a => a.From.Shorten()).Aggregate((x, y) => x + "," + y)}");

            if (state.OutputMsgs.Count < state.WinNumber)
            {
                return;
            }
            await state.Semaphore.WaitAsync();
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine();
                var acctId = state.InputMsg.Block is TransactionBlock ? (state.InputMsg.Block as TransactionBlock).AccountID.Shorten() : "";
                sb.AppendLine($"* Transaction From Node {acctId} Type: {state.InputMsg.Block.BlockType} Index: {state.InputMsg.Block.Height} Hash: {state.InputMsg.Block.Hash.Shorten()}");
                foreach (var msg in state.OutputMsgs.ToList())
                {
                    var seed0 = msg.From == ProtocolSettings.Default.StandbyValidators[0] ? "[seed0]" : "";
                    string me = "";
                    if (msg.From == _context.GetDagSystem().PosWallet.AccountId)
                        me = "[me]";
                    var voice = msg.IsSuccess ? "Yea" : "Nay";
                    var canAuth = _currentView.Authorizers.Any(a => a.AccountID == msg.From);
                    sb.AppendLine($"{voice} {msg.Result} By: {msg.From.Shorten()} CanAuth: {canAuth} {seed0}{me}");
                }
                _log.LogInformation(sb.ToString());

                _log.LogInformation($"state.Consensus is {state.PrepareConsensus}");
                if (ConsensusResult.Uncertain != state.PrepareConsensus)
                {

                    _log.LogInformation($"got Semaphore. is it saving? {state.Saving}");

                    if (state.Saving)
                        return;

                    state.Saving = true;

                    state.T4 = DateTime.Now;

                    _log.LogInformation($"Saving {state.PrepareConsensus}: {_state.InputMsg.Block.Height}/{_state.InputMsg.Block.Hash}");

                    var ts = DateTime.Now - state.Created;
                    if (_context.Stats.Count > 10000)
                        _context.Stats.RemoveRange(0, 2000);

                    _context.Stats.Add(new TransStats { ms = (long)ts.TotalMilliseconds, trans = state.InputMsg.Block.BlockType });

                    var block = state.InputMsg.Block;

                    // do commit
                    //block.Authorizations = state.OutputMsgs.Select(a => a.AuthSign).ToList();

                    var msg = new AuthorizerCommitMsg
                    {
                        From = _context.GetDagSystem().PosWallet.AccountId,
                        MsgType = ChatMessageType.AuthorizerCommit,
                        BlockHash = state.InputMsg.Block.Hash,
                        Consensus = state.PrepareConsensus
                    };

                    _context.Send2P2pNetwork(msg);
                    state.AddCommitedResult(msg);
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"CheckAuthorizedAllOkAsync: {ex.ToString()}");
            }
            finally
            {
                state.Semaphore.Release();
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
                    sys.Consensus.Tell(new BlockChain.BlockAdded { hash = block.Hash });

                    if (block is ConsolidationBlock consBlock)
                    {
                        var status = await sys.TheBlockchain.Ask<NodeStatus>(new BlockChain.QueryBlockchainStatus());
                        // we need to update the consolidation flag
                        foreach (var hash in consBlock.blockHashes)
                        {
                            if (!await sys.Storage.ConsolidateBlock(hash) && status.state != BlockChainState.Engaging)
                                _log.LogCritical($"BlockChain Not consolidate block properly: {hash}");
                        }

                        // debug
                        var blockCountInDb = await sys.Storage.GetBlockCountAsync();
                        if (consBlock.totalBlockCount + 1 > blockCountInDb)
                            _log.LogCritical($"Consolidation block miscalculate!! total: {blockCountInDb} calculated: {consBlock.totalBlockCount}");
                    }

                    //else if(block is ServiceBlock sb)
                    //{
                    //    // if this authorizer is in the previous service block's authorizers' list, it should receive fee.
                    //    if(sb.Height > 0)
                    //    {
                    //        var sbPrev = await sys.Storage.FindBlockByHashAsync(sb.PreviousHash) as ServiceBlock;
                    //        if(sbPrev != null && sbPrev.Authorizers.Any(a => a.AccountID == sys.PosWallet.AccountId))
                    //        {
                    //            var recvFeeBlock = new ReceiveAuthorizerFeeBlock
                    //            {
                    //                Height = 
                    //            };
                    //        }
                    //    }
                    //}
                }
                    

                // if self result is Nay, need (re)send commited msg here
                var myResult = _state.OutputMsgs.FirstOrDefault(a => a.From == _context.GetDagSystem().PosWallet.AccountId);
                if(myResult == null || myResult.Result != APIResultCodes.Success)
                {
                    var msg = new AuthorizerCommitMsg
                    {
                        From = _context.GetDagSystem().PosWallet.AccountId,
                        MsgType = ChatMessageType.AuthorizerCommit,
                        BlockHash = _state.InputMsg.Block.Hash,
                        Consensus = _state.PrepareConsensus
                    };

                    _context.Send2P2pNetwork(msg);
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

            _state.Done?.Set();
            _context.FinishBlock(block.Hash);

            if (block is ConsolidationBlock)
            {
                // get my authorize result
                var myResult = _state.OutputMsgs.FirstOrDefault(a => a.From == _context.GetDagSystem().PosWallet.AccountId);
                if (myResult != null && myResult.Result == APIResultCodes.Success && myResult.IsSuccess == (_state.CommitConsensus == ConsensusResult.Yea))
                    return;

                // crap! this node is out of sync.
                _context.GetDagSystem().TheBlockchain.Tell(new ConsensusService.ConsolidateFailed { consolidationBlockHash = block.Hash });
            }
        }

    }
}
