using Akka.Actor;
using Akka.Configuration;
using Lyra.Core.Accounts;
using Lyra.Core.Authorizers;
using Lyra.Core.Blocks;
using Lyra.Core.Utils;
using Microsoft.Extensions.Logging;
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
    // listen to gossip messages and activate the necessary grains to do works.
    public class ConsensusService : ReceiveActor
    {
        public class Authorized { public bool IsSuccess { get; set; } }
        private readonly IActorRef _localNode;

        Dictionary<BlockTypes, string> _authorizers;
        ILogger _log;

        // queue bellow
        Dictionary<long, AuthState> _activeConsensus;

        public ConsensusService(IActorRef localNode)
        {
            _localNode = localNode;
            _log = new SimpleLogger("ConsensusService").Logger;

            _activeConsensus = new Dictionary<long, AuthState>();

            _authorizers = new Dictionary<BlockTypes, string>();
            _authorizers.Add(BlockTypes.SendTransfer, "SendTransferAuthorizer");
            _authorizers.Add(BlockTypes.LyraTokenGenesis, "GenesisAuthorizer");
            _authorizers.Add(BlockTypes.ReceiveFee, "ReceiveTransferAuthorizer");
            _authorizers.Add(BlockTypes.OpenAccountWithReceiveFee, "NewAccountAuthorizer");
            _authorizers.Add(BlockTypes.OpenAccountWithReceiveTransfer, "NewAccountAuthorizer");
            _authorizers.Add(BlockTypes.OpenAccountWithImport, "NewAccountWithImportAuthorizer");
            _authorizers.Add(BlockTypes.ReceiveTransfer, "ReceiveTransferAuthorizer");
            _authorizers.Add(BlockTypes.ImportAccount, "ImportAccountAuthorizer");
            _authorizers.Add(BlockTypes.TokenGenesis, "NewTokenAuthorizer");

            Receive<AuthorizingMsg>(msg => {
                var state = SendAuthorizingMessage(msg);
                Task.Run(() =>
                {
                    state.Done.WaitOne();
                    return state;
                }).PipeTo(Self, Sender);
            });

            Receive<SignedMessageRelay>(relayMsg =>
            {
                OnNextAsyncImpl(relayMsg.signedMessage);
            });
        }

        public static Props Props(IActorRef localNode)
        {
            return Akka.Actor.Props.Create(() => new ConsensusService(localNode)).WithMailbox("consensus-service-mailbox");
        }

        public virtual void SendMessage(SourceSignedMessage msg)
        {
            _log.LogInformation($"GossipListener: SendMessage Called: msg From: {msg.From}");

            var sign = msg.Sign(NodeService.Instance.PosWallet.PrivateKey, msg.From);
            _log.LogInformation($"GossipListener: Sign {msg.Hash} got: {sign} by prvKey: {NodeService.Instance.PosWallet.PrivateKey} pubKey: {msg.From}");

            _localNode.Tell(msg);
        }

        public AuthState SendAuthorizingMessage(AuthorizingMsg block)
        {
            SendMessage(block);
            var state = CreateAuthringState(block);
            return state;
        }

        public Task OnNextAsync(SourceSignedMessage item)
        {
            OnNextAsyncImpl(item);
            return Task.CompletedTask;
        }
        void OnNextAsyncImpl(SourceSignedMessage item)
        {
            _log.LogInformation($"GossipListener: OnNextAsyncImpl Called: msg From: {item.From}");

            // verify the signatures of msg. make sure it is from the right node.
            //var nodeConfig = null;
            if (!item.VerifySignature(item.From))
            {
                _log.LogInformation($"GossipListener: bad signature: {item.Hash} sign: {item.Signature} by pubKey: {item.From}");
                _log.LogInformation($"GossipListener: hash: {item.Hash} rehash: {item.CalculateHash()}");
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
            _log.LogInformation($"GossipListener: CreateAuthringState Called: BlockUIndex: {item.Block.UIndex}");

            var ukey = item.Block.UIndex;
            if (_activeConsensus.ContainsKey(ukey))
            {
                return _activeConsensus[ukey];
            }

            var state = new AuthState
            {
                UIndexOfFirstBlock = ukey,
                InputMsg = item,
            };
            _activeConsensus.Add(ukey, state);
            return state;
        }

        private void OnPrePrepare(AuthorizingMsg item)
        {
            _log.LogInformation($"GossipListener: OnPrePrepare Called: BlockUIndex: {item.Block.UIndex}");

            var state = CreateAuthringState(item);

            _ = Task.Run(async () =>
            {
                var authrName = _authorizers[item.Block.BlockType];
                var authorizer = (IAuthorizer)Activator.CreateInstance(Type.GetType("Lyra.Core.Authorizers." + authrName));

                var localAuthResult = await authorizer.Authorize(item.Block);
                var result = new AuthorizedMsg
                {
                    From = NodeService.Instance.PosWallet.AccountId,
                    BlockIndex = item.Block.UIndex,
                    Result = localAuthResult.Item1,
                    AuthSign = localAuthResult.Item2
                };

                SendMessage(result);
                state.AddAuthResult(result);
                _log.LogInformation($"GossipListener: OnPrePrepare LocalAuthorized: {item.Block.UIndex}: {localAuthResult.Item1}");
            });
        }

        private void OnPrepare(AuthorizedMsg item)
        {
            _log.LogInformation($"GossipListener: OnPrepare Called: BlockUIndex: {item.BlockIndex}");

            var state = _activeConsensus[item.BlockIndex];
            state.AddAuthResult(item);

            if (state.IsAuthoringSuccess)
            {
                _ = Task.Run(async () =>
                {
                    // do commit
                    var block = state.InputMsg.Block;
                    block.Authorizations = state.OutputMsgs.Select(a => a.AuthSign).ToList();

                    BlockChain.Singleton.AddBlock(block);

                    var msg = new AuthorizerCommitMsg
                    {
                        From = NodeService.Instance.PosWallet.AccountId,
                        BlockIndex = block.UIndex,
                        Commited = true
                    };

                    SendMessage(msg);

                    _log.LogInformation($"GossipListener: OnPrepare Commited: BlockUIndex: {item.BlockIndex}");
                });
            }
        }

        private void OnCommit(AuthorizerCommitMsg item)
        {
            _log.LogInformation($"GossipListener: OnCommit Called: BlockUIndex: {item.BlockIndex}");

            var state = _activeConsensus[item.BlockIndex];
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
