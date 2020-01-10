using Akka.Actor;
using Akka.Configuration;
using Clifton.Blockchain;
using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Authorizers;
using Lyra.Core.Blocks;
using Lyra.Core.Cryptography;
using Lyra.Core.Utils;
using Microsoft.Extensions.Logging;
using Neo;
using Neo.IO.Actors;
using Neo.Network.P2P;
using Newtonsoft.Json;
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
        public class Consolidate { }
        public class AskForBillboard { }
        public class BlockChainSynced { }
        public class Authorized { public bool IsSuccess { get; set; } }
        private readonly IActorRef _localNode;

        ILogger _log;

        // hash, authState
        Dictionary<string, List<SourceSignedMessage>> _outOfOrderedMessages;
        Dictionary<string, AuthState> _activeConsensus;
        Dictionary<string, AuthState> _cleanedConsensus;
        private BillBoard _board;

        private AuthorizersFactory _authorizers;
        private long _UIndexSeed = -1;

        public bool IsThisNodeSeed0 => NodeService.Instance.PosWallet.AccountId == ProtocolSettings.Default.StandbyValidators[0];
        public ConsensusWorkingMode Mode { get; private set; }

        public ConsensusService(IActorRef localNode)
        {
            _localNode = localNode;
            _log = new SimpleLogger("ConsensusService").Logger;

            _outOfOrderedMessages = new Dictionary<string, List<SourceSignedMessage>>();
            _activeConsensus = new Dictionary<string, AuthState>();
            _cleanedConsensus = new Dictionary<string, AuthState>();

            _authorizers = new AuthorizersFactory();
            while (BlockChain.Singleton == null)
                Task.Delay(100).Wait();

            _UIndexSeed = BlockChain.Singleton.GetBlockCount() + 1;
            Mode = ConsensusWorkingMode.OutofSyncWaiting;

            Receive<Consolidate>((_) =>
            {
                _log.LogInformation("Doing Consolidate");
                OnNodeActive(NodeService.Instance.PosWallet.AccountId);     // update billboard

                if (Mode == ConsensusWorkingMode.Normal)
                {
                    Task.Run(async () =>
                    {
                        await GenerateConsolidateBlockAsync();
                    });

                    BroadCastBillBoard();                
                }
            });

            Receive<BillBoard>((bb) => { 
                _board = bb;
            });

            Receive<AskForBillboard>((_) => Sender.Tell(_board));

            Receive<AuthorizingMsg>(async msg => {
                if (msg.Version != LyraGlobal.ProtocolVersion)
                    Sender.Tell(null);

                OnNodeActive(NodeService.Instance.PosWallet.AccountId);     // update billboard

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
                    StateClean(30);
                    await Task.Delay(1000).ConfigureAwait(false);
                }
            });
        }

        private async Task GenerateConsolidateBlockAsync()
        {
            var authGenesis = BlockChain.Singleton.GetLastServiceBlock();
            var lastCons = BlockChain.Singleton.GetSyncBlock();
            var consBlock = new ConsolidationBlock
            {
                UIndex = _UIndexSeed++,
                NetworkId = authGenesis.NetworkId,
                ShardId = authGenesis.ShardId,
                ServiceHash = authGenesis.Hash,
                SvcAccountID = NodeService.Instance.PosWallet.AccountId
            };

            // use merkle tree to consolidate all previous blocks, from lastCons.UIndex to xx[consBlock.UIndex -1]xx may lost the newest block
            // if the block is old enough ( > 2 mins ), it should be replaced by NullTransactionBlock.
            // in fact we should reserve consolidate block number and wait 2min to do consolidating
            // all null block's previous block is the last consolidate block, it's index is counted from 1 related to previous block
            await Task.Delay(33 * 1000);    // the cleaner clean block old than 30 seconds.

            var mt = new MerkleTree();
            int NullBlockIndex = 1;
            for (var ndx = lastCons.UIndex; ndx < consBlock.UIndex; ndx++)      // TODO: handling "losing" block here
            {
                var block = BlockChain.Singleton.GetBlockByUIndex(ndx);
                if(block == null)
                {
                    // block lost
                    _log.LogError($"Block lost for No. {ndx}. Create null transaction block.");

                    var nb = new NullTransactionBlock
                    {
                        UIndex = ndx,
                        Index = NullBlockIndex++,
                        NetworkId = authGenesis.NetworkId,
                        ShardId = authGenesis.ShardId,
                        ServiceHash = authGenesis.Hash,
                        AccountID = NodeService.Instance.PosWallet.AccountId
                    };
                    nb.InitializeBlock(lastCons, NodeService.Instance.PosWallet.PrivateKey,
                        authGenesis.NetworkId, authGenesis.ShardId,
                        NodeService.Instance.PosWallet.AccountId);
                    nb.UHash = SignableObject.CalculateHash($"{nb.UIndex}|{nb.Index}|{nb.Hash}");
                    SendServiceBlock(nb);

                    block = nb;
                }
                var mhash = MerkleHash.Create(block.UHash);
                mt.AppendLeaf(mhash);
            }
            consBlock.MerkelTreeHash = mt.BuildTree().ToString();

            consBlock.InitializeBlock(lastCons, NodeService.Instance.PosWallet.PrivateKey,
                authGenesis.NetworkId, authGenesis.ShardId,
                NodeService.Instance.PosWallet.AccountId);

            consBlock.UHash = SignableObject.CalculateHash($"{consBlock.UIndex}|{consBlock.Index}|{consBlock.Hash}");

            SendServiceBlock(consBlock);
        }

        private void SendServiceBlock(TransactionBlock svcBlock)
        {
            AuthorizingMsg msg = new AuthorizingMsg
            {
                From = NodeService.Instance.PosWallet.AccountId,
                Block = svcBlock,
                MsgType = ChatMessageType.AuthorizerPrePrepare
            };

            var state = CreateAuthringState(msg);
            var localAuthResult = LocalAuthorizingAsync(msg);
            state.AddAuthResult(localAuthResult);

            if (!localAuthResult.IsSuccess)
            {
                _log.LogError("Fatal Error: Consolidate block local authorization failed.");
            }
            else
            {
                Send2P2pNetwork(msg);
                Send2P2pNetwork(localAuthResult);
            }
        }

        private void StateClean(int seconds)
        {
            try
            {
                // first clean cleaned states
                var cleaned = _cleanedConsensus.Values.ToArray();
                for(int i = 0; i < cleaned.Length; i++)
                {
                    var state = cleaned[i];
                    if (state.Created - DateTime.Now > TimeSpan.FromMinutes(10)) // consensus timeout 30 seconds
                    {
                        _cleanedConsensus.Remove(state.InputMsg.Block.Hash);
                    }
                }

                var states = _activeConsensus.Values.ToArray();
                for (int i = 0; i < states.Length; i++)
                {
                    var state = states[i];
                    if (state.Created - DateTime.Now > TimeSpan.FromSeconds(seconds)) // consensus timeout 30 seconds
                    {
                        _activeConsensus.Remove(state.InputMsg.Block.Hash);
                        state.Done.Set();

                        _cleanedConsensus.Add(state.InputMsg.Block.Hash, state);
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

            int waitCount = 5;
            while (LocalNode.Singleton.RemoteNodes.Count < 1 && waitCount > 0)
            {
                _log.LogWarning("Not connected to Lyra Network. Delay sending... ");
                Task.Delay(1000).Wait();
                waitCount--;
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
                case ChatMsg chat when chat.MsgType == ChatMessageType.NodeUp:
                    OnNodeUp(chat);
                    break;
                case ChatMsg bbb when bbb.MsgType == ChatMessageType.StakingChanges:
                    OnBillBoardBroadcast(bbb);
                    break;
                case ChatMsg bcc when bcc.MsgType == ChatMessageType.BlockConsolidation:
                    OnBlockConsolication(bcc);
                    break;
                default:
                    // log msg unknown
                    break;
            }
        }

        private void OnBlockConsolication(ChatMsg msg)
        {
            if(!IsThisNodeSeed0)
            {
                var block = JsonConvert.DeserializeObject<ConsolidationBlock>(msg.Text);
                BlockChain.Singleton.AddBlock(block);
                _log.LogInformation($"Receive and store ConsolidateBlock of UIndex: {block.UIndex}");
            }
        }

        private void OnBillBoardBroadcast(ChatMsg msg)
        {
            if(!IsThisNodeSeed0)
            {
                _board = JsonConvert.DeserializeObject<BillBoard>(msg.Text);
                _log.LogInformation("BillBoard updated!");
            }
        }

        private void BroadCastBillBoard()
        {
            var msg = new ChatMsg(NodeService.Instance.PosWallet.AccountId, JsonConvert.SerializeObject(_board));
            msg.MsgType = ChatMessageType.StakingChanges;
            Send2P2pNetwork(msg);
        }

        private void OnNodeActive(string nodeAccountId)
        {
            if(_board != null)
                _board.Add(nodeAccountId);
        }

        private void OnNodeUp(ChatMsg chat)
        {
            if (_board == null)
                return;

            var node = _board.Add(chat.From);

            if(IsThisNodeSeed0)
            {
                // broadcast billboard
                BroadCastBillBoard();
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

            // add possible out of ordered messages belong to the block
            if(_outOfOrderedMessages.ContainsKey(item.Block.Hash))
            {
                var msgs = _outOfOrderedMessages[item.Block.Hash];
                _outOfOrderedMessages.Remove(item.Block.Hash);

                foreach(var msg in msgs)
                {
                    switch(msg)
                    {
                        case AuthorizedMsg authorized:
                            state.AddAuthResult(authorized);
                            break;
                        case AuthorizerCommitMsg committed:
                            state.AddCommitedResult(committed);
                            break;
                    }
                }
            }

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
                BlockHash = item.Block.Hash,
                Result = localAuthResult.Item1,
                AuthSign = localAuthResult.Item2
            };            

            if(item.Block.BlockType == BlockTypes.Consolidation)
            {
                // do nothing. the UIndex has already take cared of.
            }
            else
            {
                result.BlockUIndex = Mode == ConsensusWorkingMode.Normal ? _UIndexSeed++ : 0;     // if seed out of sync, then others know
            }

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

            if(_activeConsensus.ContainsKey(item.BlockHash))
            {
                var state = _activeConsensus[item.BlockHash];
                state.AddAuthResult(item);

                CheckAuthorizedAllOk(state);
            }
            else
            {
                // maybe outof ordered message
                if(_cleanedConsensus.ContainsKey(item.BlockHash))
                {
                    return;
                }

                List<SourceSignedMessage> msgs;
                if (_outOfOrderedMessages.ContainsKey(item.BlockHash))
                    msgs = _outOfOrderedMessages[item.BlockHash];
                else
                {
                    msgs = new List<SourceSignedMessage>();
                    msgs.Add(item);
                }

                msgs.Add(item);
            }
        }

        private void CheckAuthorizedAllOk(AuthState state)
        {
            if (state.GetIsAuthoringSuccess(_board))
            {
                if (state.Saving)
                    return;

                state.Saving = true;
                _ = Task.Run(() =>
                {
                    // do commit
                    var block = state.InputMsg.Block;
                    block.Authorizations = state.OutputMsgs.Select(a => a.AuthSign).ToList();

                    if(block.BlockType != BlockTypes.Consolidation)
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

            if(_activeConsensus.ContainsKey(item.BlockHash))
            {
                var state = _activeConsensus[item.BlockHash];
                state.AddCommitedResult(item);

                OnNodeActive(item.From);        // track latest activities via billboard
            }
            else
            {
                // maybe outof ordered message
                if (_cleanedConsensus.ContainsKey(item.BlockHash))
                {
                    return;
                }

                List<SourceSignedMessage> msgs;
                if (_outOfOrderedMessages.ContainsKey(item.BlockHash))
                    msgs = _outOfOrderedMessages[item.BlockHash];
                else
                {
                    msgs = new List<SourceSignedMessage>();
                    msgs.Add(item);
                }

                msgs.Add(item);
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
