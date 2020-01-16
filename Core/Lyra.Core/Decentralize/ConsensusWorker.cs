using Akka.Actor;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Core.Utils;
using Microsoft.Extensions.Logging;
using Neo;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize
{
    public class ConsensusWorker : ReceiveActor
    {
        ConsensusService _context;
        ILogger _log;
        private AuthorizersFactory _authorizers;

        ConcurrentQueue<SourceSignedMessage> _outOfOrderedMessages;

        AuthState _state;

        public ConsensusWorker(ConsensusService context)
        {
            _context = context;
            _log = new SimpleLogger("ConsensusWorker").Logger;
            _authorizers = new AuthorizersFactory();
            _outOfOrderedMessages = new ConcurrentQueue<SourceSignedMessage>();

            Receive<AuthorizingMsg>(msg =>
            {
                _context.OnNodeActive(NodeService.Instance.PosWallet.AccountId);     // update billboard

                if (msg.Version != LyraGlobal.ProtocolVersion)
                {
                    return;
                }

                // first try auth locally
                //if(_state == null)
                _state = CreateAuthringState(msg);

                if(_outOfOrderedMessages.Count > 0)
                {
                    SourceSignedMessage queuedMsg;
                    while (_outOfOrderedMessages.TryDequeue(out queuedMsg))
                    {
                        switch(queuedMsg)
                        {
                            case AuthorizedMsg msg1:
                                OnPrepare(msg1);
                                break;
                            case AuthorizerCommitMsg msg2:
                                OnCommit(msg2);
                                break;
                        }
                    }
                }

                //_context.Send2P2pNetwork(msg);

                var localAuthResult = LocalAuthorizingAsync(msg);
                _state.AddAuthResult(localAuthResult);

                _context.Send2P2pNetwork(localAuthResult);
            });

            Receive<AuthState>(state =>
            {
                _state = state;
                _context.Send2P2pNetwork(_state.InputMsg);

                var localAuthResult = LocalAuthorizingAsync(_state.InputMsg);
                _state.AddAuthResult(localAuthResult);

                _context.Send2P2pNetwork(localAuthResult);
            });

            Receive<AuthorizedMsg>(msg =>
            {
                if (_state == null)
                    _outOfOrderedMessages.Enqueue(msg);
                else
                    OnPrepare(msg);
            });

            Receive<AuthorizerCommitMsg>(msg =>
            {
                if (_state == null)
                    _outOfOrderedMessages.Enqueue(msg);
                else
                    OnCommit(msg);
            });
        }

        private AuthState CreateAuthringState(AuthorizingMsg item)
        {
            _log.LogInformation($"Consensus: CreateAuthringState Called: BlockUIndex: {item.Block.UIndex}");

            var ukey = item.Block.Hash;
            //if (_activeConsensus.ContainsKey(ukey))
            //{
            //    return _activeConsensus[ukey];
            //}

            var state = new AuthState
            {
                HashOfFirstBlock = ukey,
                InputMsg = item,
            };

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
            //if (null != BlockChain.Singleton.FindBlockByHash(item.Block.Hash))
            //{
            //    _log.LogInformation("CreateAuthringState: Block is already in database.");
            //    return null;
            //}

            // check if block was replaced by nulltrans
            //if (null != BlockChain.Singleton.FindNullTransBlockByHash(item.Block.Hash))
            //{
            //    _log.LogInformation("CreateAuthringState: Block is already consolidated by nulltrans.");
            //    return null;
            //}

            //_activeConsensus.Add(ukey, state);
            return state;
        }

        private AuthorizedMsg LocalAuthorizingAsync(AuthorizingMsg item)
        {
            var stopwatch = Stopwatch.StartNew();
            var authorizer = _authorizers[item.Block.BlockType];

            AuthorizedMsg result;
            try
            {
                var localAuthResult = authorizer.Authorize(item.Block);
                result = new AuthorizedMsg
                {
                    From = NodeService.Instance.PosWallet.AccountId,
                    MsgType = ChatMessageType.AuthorizerPrepare,
                    BlockHash = item.Block.Hash,
                    Result = localAuthResult.Item1,
                    AuthSign = localAuthResult.Item2
                };

                if (item.Block.BlockType == BlockTypes.Consolidation || item.Block.BlockType == BlockTypes.NullTransaction || item.Block.BlockType == BlockTypes.Service)
                {
                    // do nothing. the UIndex has already been take cared of.
                }
                else
                {
                    _log.LogWarning($"Give UIndex {_context.USeed} to block {item.Block.Hash.Shorten()} of Type {item.Block.BlockType}");
                    result.BlockUIndex = _context.USeed++;
                }
            }
            catch (Exception e)
            {
                _log.LogWarning($"Consensus: LocalAuthorizingAsync Exception: {e.Message} BlockUIndex: {item.Block.UIndex}");

                result = new AuthorizedMsg
                {
                    From = NodeService.Instance.PosWallet.AccountId,
                    MsgType = ChatMessageType.AuthorizerPrepare,
                    BlockHash = item.Block.Hash,
                    Result = APIResultCodes.UnknownError,
                    AuthSign = null
                };
            }
            result.Sign(NodeService.Instance.PosWallet.PrivateKey, result.From);

            stopwatch.Stop();
            _log.LogInformation($"LocalAuthorizingAsync takes {stopwatch.ElapsedMilliseconds} ms with {result.Result}");
            return result;
        }

        private void OnPrePrepare(AuthorizingMsg item)
        {
            //_log.LogInformation($"Consensus: OnPrePrepare Called: BlockUIndex: {item.Block.UIndex}");

            var state = CreateAuthringState(item);
            if (state == null)
                return;

            var result = LocalAuthorizingAsync(item);

            _context.Send2P2pNetwork(result);
            state.AddAuthResult(result);
            CheckAuthorizedAllOk(state);
            if(!result.IsSuccess)
                _log.LogInformation($"Consensus: OnPrePrepare LocalAuthorized: {item.Block.UIndex}: {result.Result}");
        }

        private void OnPrepare(AuthorizedMsg item)
        {
            //_log.LogInformation($"Consensus: OnPrepare Called: Block Hash: {item.BlockHash}");

            //if (_activeConsensus.ContainsKey(item.BlockHash))
            //{
            //    var state = _activeConsensus[item.BlockHash];
            _state.AddAuthResult(item);

            CheckAuthorizedAllOk(_state);
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

        private void CheckAuthorizedAllOk(AuthState state)
        {
            // check state
            // debug: show all states
            if(state.OutputMsgs.Count > 2)
            {

            }
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine($"* Transaction From Node {state.InputMsg.Block.AccountID.Shorten()} Type: {state.InputMsg.Block.BlockType} Index: {state.InputMsg.Block.Index} Hash: {state.InputMsg.Block.Hash.Shorten()}");
            foreach (var msg in state.OutputMsgs)
            {
                var seed0 = msg.From == ProtocolSettings.Default.StandbyValidators[0] ? "[seed0]" : "";
                string me = "";
                if (msg.From == NodeService.Instance.PosWallet.AccountId)
                    me = "[me]";
                var voice = msg.IsSuccess ? "Yay" : "Nay";
                sb.AppendLine($"{voice} {msg.Result} By: {msg.From.Shorten()} CanAuth: {_context.Board.AllNodes[msg.From].AbleToAuthorize} {seed0}{me}");
            }
            _log.LogInformation(sb.ToString());

            if (state.GetIsAuthoringSuccess(_context.Board))
            {
                if (state.Saving)
                    return;

                state.Saving = true;

                var ts = DateTime.Now - state.Created;
                if (_context.Stats.Count > 10000)
                    _context.Stats.RemoveRange(0, 2000);

                _context.Stats.Add(new TransStats { ms = (long)ts.TotalMilliseconds, trans = state.InputMsg.Block.BlockType });

                // do commit
                var block = state.InputMsg.Block;
                block.Authorizations = state.OutputMsgs.Select(a => a.AuthSign).ToList();

                if (block.BlockType != BlockTypes.Consolidation && block.BlockType != BlockTypes.NullTransaction && block.BlockType != BlockTypes.Service)
                {
                    // pickup UIndex
                    try
                    {
                        block.UIndex = state.ConsensusUIndex;
                    }
                    catch (Exception ex)
                    {
                        _log.LogError("Can't get UIndex. System fail: " + ex.Message);
                        return;
                    }

                    //if (!IsThisNodeSeed0 && block.UIndex != USeed - 1)
                    //{
                    //    // local node out of sync
                    //    Mode = ConsensusWorkingMode.OutofSyncWaiting;
                    //    LyraSystem.Singleton.TheBlockchain.Tell(new BlockChain.NeedSync { ToUIndex = block.UIndex });
                    //}
                }

                block.UHash = SignableObject.CalculateHash($"{block.UIndex}|{block.Index}|{block.Hash}");

                if(!BlockChain.Singleton.AddBlock(block))
                    _log.LogWarning($"Block Save Failed UIndex: {block.UIndex}");

                var msg = new AuthorizerCommitMsg
                {
                    From = NodeService.Instance.PosWallet.AccountId,
                    MsgType = ChatMessageType.AuthorizerCommit,
                    BlockHash = state.InputMsg.Block.Hash,
                    BlockIndex = block.UIndex,
                    Commited = true
                };
                msg.Sign(NodeService.Instance.PosWallet.PrivateKey, msg.From);

                state.AddCommitedResult(msg);
                _context.Send2P2pNetwork(msg);
            }
        }

        private void OnCommit(AuthorizerCommitMsg item)
        {
            //_log.LogInformation($"Consensus: OnCommit Called: BlockUIndex: {item.BlockIndex}");

            //if (_activeConsensus.ContainsKey(item.BlockHash))
            //{
            //    var state = _activeConsensus[item.BlockHash];
            _state.AddCommitedResult(item);

            _context.OnNodeActive(item.From);        // track latest activities via billboard
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
    }
}
