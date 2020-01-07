using Akka.Actor;
using Akka.Configuration;
using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Authorizers;
using Lyra.Core.Blocks;
using Lyra.Core.Utils;
using Microsoft.Extensions.Logging;
using Neo;
using Neo.IO.Actors;
using Neo.Network.P2P;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Neo.Network.P2P.LocalNode;

namespace Lyra.Core.Decentralize
{
    // when out of sync, we adjust useed, continue to save blocks, and told blockchain to do sync.
    public enum ConsensusWorkingMode { Normal, OutofSyncWaiting }
    /// <summary>
    /// about seed generation: the seed0 seed will generate UIndex whild sending authorization message.
    /// </summary>
    public class ConsensusService : ReceiveActor
    {
        public class AskForBillboard { }
        public class BlockChainSynced { }
        public class Authorized { public bool IsSuccess { get; set; } }
        private readonly IActorRef _localNode;

        ILogger _log;

        // hash, authState
        Dictionary<string, AuthState> _activeConsensus;
        private BillBoard _board;

        private AuthorizersFactory _authorizers;
        private long _UIndexSeed = -1;

        public ConsensusWorkingMode Mode { get; private set; }

        public ConsensusService(IActorRef localNode)
        {
            _localNode = localNode;
            _log = new SimpleLogger("ConsensusService").Logger;

            _activeConsensus = new Dictionary<string, AuthState>();
            _board = new BillBoard();

            _authorizers = new AuthorizersFactory();
            while (BlockChain.Singleton == null)
                Task.Delay(100).Wait();

            _UIndexSeed = BlockChain.Singleton.GetBlockCount() + 1;
            Mode = ConsensusWorkingMode.OutofSyncWaiting;

            Receive<AskForBillboard>((_) => Sender.Tell(_board));

            Receive<AuthorizingMsg>(async msg => {
                if (msg.Version != LyraGlobal.ProtocolVersion)
                    Sender.Tell(null);

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
                        _ = state.Done.WaitOne();
                    }).ConfigureAwait(false);

                    sender.Tell(state);                    
                }
            });

            Receive<SignedMessageRelay>(relayMsg =>
            {
                if (relayMsg.signedMessage.Version == LyraGlobal.ProtocolVersion)
                    OnNextConsensusMessage(relayMsg.signedMessage);
                else
                    _log.LogWarning("Protocol Version Mismatch. Do nothing.");
            });

            Receive<BlockChainSynced>(_ =>
            {
                Mode = ConsensusWorkingMode.Normal;
                _UIndexSeed = BlockChain.Singleton.GetBlockCount() + 1;
                
                // declare to the network
                var msg = new ChatMsg
                {
                    From = NodeService.Instance.PosWallet.AccountId,
                    MsgType = ChatMessageType.NodeUp,
                    Text = "Staking with () Lyra"
                };

                Send2P2pNetwork(msg);
            });

            Task.Run(async () => { 
                while(true)
                {
                    StateClean();
                    await Task.Delay(1000).ConfigureAwait(false);
                }
            });
        }

        private void StateClean()
        {
            try
            {
                var states = _activeConsensus.Values.ToArray();
                for (int i = 0; i < states.Length; i++)
                {
                    var state = states[i];
                    if (state.Created - DateTime.Now > TimeSpan.FromSeconds(30)) // consensus timeout 30 seconds
                    {
                        _activeConsensus.Remove(state.InputMsg.Block.Hash);
                        state.Done.Set();
                    }
                }
            }
            catch(Exception ex)
            {
                _log.LogError("In StateClean: " + ex.Message);
            }
        }

        public static Props Props(IActorRef localNode)
        {
            return Akka.Actor.Props.Create(() => new ConsensusService(localNode)).WithMailbox("consensus-service-mailbox");
        }

        public virtual void Send2P2pNetwork(SourceSignedMessage msg)
        {
            _log.LogInformation($"Consensus: SendMessage Called: msg From: {msg.From}");

            var sign = msg.Sign(NodeService.Instance.PosWallet.PrivateKey, msg.From);
            //_log.LogInformation($"Consensus: Sign {msg.Hash} got: {sign} by prvKey: {NodeService.Instance.PosWallet.PrivateKey} pubKey: {msg.From}");

            while (LocalNode.Singleton.RemoteNodes.Count < 1)
            {
                _log.LogWarning("Not connected to Lyra Network. Delay sending... ");
                Task.Delay(2000).Wait();
            }

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
                case ChatMsg chat when chat.MsgType == ChatMessageType.NodeUp || chat.MsgType == ChatMessageType.StakingChanges:
                    OnNodeUp(chat);
                    break;
                default:
                    // log msg unknown
                    break;
            }
        }

        private void OnNodeUp(ChatMsg chat)
        {
            PosNode node;
            if (_board.AllNodes.ContainsKey(chat.From))
                node = _board.AllNodes[chat.From];
            else
            {
                node = new PosNode(chat.From);
                _board.AllNodes.Add(chat.From, node);
            }
            // lookup balance
            var block = BlockChain.Singleton.FindLatestBlock(node.AccountID);
            if(block != null && block.Balances.ContainsKey(LyraGlobal.LYRATICKERCODE))
            {
                node.Balance = block.Balances[LyraGlobal.LYRATICKERCODE];
            }

            if(node.Balance < LyraGlobal.MinimalAuthorizerBalance)
            {
                _log.LogInformation("Node {0} has not enough balance: {1}.", node.AccountID, node.Balance);
            }
        }

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
                BlockUIndex = Mode == ConsensusWorkingMode.Normal ? _UIndexSeed++ : 0,     // if seed out of sync, then others know
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
                if (state.Saving)
                    return;

                state.Saving = true;
                _ = Task.Run(() =>
                {
                    // do commit
                    var block = state.InputMsg.Block;
                    block.Authorizations = state.OutputMsgs.Select(a => a.AuthSign).ToList();

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

                    if (block.UIndex != _UIndexSeed - 1)
                    {
                        // local node out of sync
                        _UIndexSeed = block.UIndex + 1;
                        Mode = ConsensusWorkingMode.OutofSyncWaiting;
                        LyraSystem.Singleton.TheBlockchain.Tell(new BlockChain.NeedSync { ToUIndex = block.UIndex });
                    }

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
