using Akka.Actor;
using Akka.Configuration;
using Lyra.Core.Accounts;
using Lyra.Core.Authorizers;
using Lyra.Core.Blocks;
using Lyra.Core.Utils;
using Microsoft.Extensions.Logging;
using Neo;
using Neo.IO.Actors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Neo.Network.P2P.LocalNode;

namespace Lyra.Core.Decentralize
{
    /// <summary>
    /// about seed generation: the seed0 seed will generate UIndex whild sending authorization message.
    /// </summary>
    public class ConsensusService : ReceiveActor
    {
        public class Authorized { public bool IsSuccess { get; set; } }
        private readonly IActorRef _localNode;

        ILogger _log;

        // hash, authState
        Dictionary<string, AuthState> _activeConsensus;

        private AuthorizersFactory _authorizers;
        private long _UIndexSeed;

        public ConsensusService(IActorRef localNode)
        {
            _localNode = localNode;
            _log = new SimpleLogger("ConsensusService").Logger;

            _activeConsensus = new Dictionary<string, AuthState>();

            _authorizers = new AuthorizersFactory();
            _UIndexSeed = BlockChain.Singleton.GetBlockCount() + 1;

            Receive<AuthorizingMsg>(async msg => {
                // first try auth locally
                var state = CreateAuthringState(msg);
                var localAuthResult = LocalAuthorizingAsync(msg);
                state.AddAuthResult(localAuthResult);
                
                if(!localAuthResult.IsSuccess)
                {
                    state.Done.Set();
                    Sender.Tell(state);
                }
                else
                {
                    Send2P2pNetwork(msg);
                    Send2P2pNetwork(localAuthResult);

                    var sender = Context.Sender;

                    await Task.Run(() =>
                    {
                        state.Done.WaitOne();
                    });

                    sender.Tell(state);                    
                }
            });

            Receive<SignedMessageRelay>(relayMsg =>
            {
                OnNextConsensusMessage(relayMsg.signedMessage);
            });
        }

        public static Props Props(IActorRef localNode)
        {
            return Akka.Actor.Props.Create(() => new ConsensusService(localNode)).WithMailbox("consensus-service-mailbox");
        }

        public virtual void Send2P2pNetwork(SourceSignedMessage msg)
        {
            _log.LogInformation($"Consensus: SendMessage Called: msg From: {msg.From}");

            var sign = msg.Sign(NodeService.Instance.PosWallet.PrivateKey, msg.From);
            _log.LogInformation($"Consensus: Sign {msg.Hash} got: {sign} by prvKey: {NodeService.Instance.PosWallet.PrivateKey} pubKey: {msg.From}");

            _localNode.Tell(msg);
        }

        void OnNextConsensusMessage(SourceSignedMessage item)
        {
            _log.LogInformation($"Consensus: OnNextAsyncImpl Called: msg From: {item.From}");

            // verify the signatures of msg. make sure it is from the right node.
            //var nodeConfig = null;
            if (!item.VerifySignature(item.From))
            {
                _log.LogInformation($"Consensus: bad signature: {item.Hash} sign: {item.Signature} by pubKey: {item.From}");
                _log.LogInformation($"Consensus: hash: {item.Hash} rehash: {item.CalculateHash()}");
                return;
            }

            switch (item)
            {
                case AuthorizingMsg msg:
                    OnPrePrepare(msg);
                    break;
                case AuthorizedMsg authed:
                    OnPrepare(authed);
                    break;
                case AuthorizerCommitMsg commited:
                    OnCommit(commited);
                    break;
                //case ChatMsg chat:
                //    await OnChatMsg(chat);
                //    break;
                default:
                    // log msg unknown
                    break;
            }
        }

        //private async Task OnChatMsg(ChatMsg chat)
        //{

        //}

        private AuthState CreateAuthringState(AuthorizingMsg item)
        {
            _log.LogInformation($"Consensus: CreateAuthringState Called: BlockUIndex: {item.Block.UIndex}");

            var ukey = item.Block.Hash;
            if (_activeConsensus.ContainsKey(ukey))
            {
                return _activeConsensus[ukey];
            }

            var state = new AuthState
            {
                HashOfFirstBlock = ukey,
                InputMsg = item,
            };
            _activeConsensus.Add(ukey, state);
            return state;
        }

        private AuthorizedMsg LocalAuthorizingAsync(AuthorizingMsg item)
        {
            var authorizer = _authorizers[item.Block.BlockType];

            var localAuthResult = authorizer.Authorize(item.Block);
            var result = new AuthorizedMsg
            {
                From = NodeService.Instance.PosWallet.AccountId,
                MsgType = ChatMessageType.AuthorizerPrepare,
                BlockUIndex = _UIndexSeed++,
                BlockHash = item.Block.Hash,
                Result = localAuthResult.Item1,
                AuthSign = localAuthResult.Item2
            };            

            return result;
        }

        private void OnPrePrepare(AuthorizingMsg item)
        {
            _log.LogInformation($"Consensus: OnPrePrepare Called: BlockUIndex: {item.Block.UIndex}");

            var state = CreateAuthringState(item);

            _ = Task.Run(() =>
            {
                var result = LocalAuthorizingAsync(item);

                Send2P2pNetwork(result);
                state.AddAuthResult(result);
                CheckAuthorizedAllOk(state);
                _log.LogInformation($"Consensus: OnPrePrepare LocalAuthorized: {item.Block.UIndex}: {result.IsSuccess}");
            });
        }

        private void OnPrepare(AuthorizedMsg item)
        {
            _log.LogInformation($"Consensus: OnPrepare Called: Block Hash: {item.BlockHash}");

            var state = _activeConsensus[item.BlockHash];
            state.AddAuthResult(item);

            CheckAuthorizedAllOk(state);
        }

        private void CheckAuthorizedAllOk(AuthState state)
        {
            if (state.IsAuthoringSuccess)
            {
                _ = Task.Run(() =>
                {
                    // do commit
                    var block = state.InputMsg.Block;
                    block.Authorizations = state.OutputMsgs.Select(a => a.AuthSign).ToList();

                    // pickup UIndex
                    block.UIndex = state.OutputMsgs.First(a => a.From == ProtocolSettings.Default.StandbyValidators[0]).BlockUIndex;
                    block.UHash = SignableObject.CalculateHash($"{block.UIndex}|{block.Index}|{block.Hash}");

                    BlockChain.Singleton.AddBlock(block);

                    var msg = new AuthorizerCommitMsg
                    {
                        From = NodeService.Instance.PosWallet.AccountId,
                        MsgType = ChatMessageType.AuthorizerCommit,
                        BlockHash = state.InputMsg.Block.Hash,
                        BlockIndex = block.UIndex,
                        Commited = true
                    };

                    state.AddCommitedResult(msg);
                    Send2P2pNetwork(msg);

                    _log.LogInformation($"Consensus: OnPrepare Commited: BlockUIndex: {msg.BlockHash}");
                });
            }
        }

        private void OnCommit(AuthorizerCommitMsg item)
        {
            _log.LogInformation($"Consensus: OnCommit Called: BlockUIndex: {item.BlockIndex}");

            var state = _activeConsensus[item.BlockHash];
            state.AddCommitedResult(item);
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
