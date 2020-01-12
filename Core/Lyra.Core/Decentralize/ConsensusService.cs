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
using System.Threading;
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
        public class AskForStats { }
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
        private object _seedLocker = new object();

        private long USeed
        {
            get
            {
                lock (_seedLocker)
                {
                    return _UIndexSeed;
                }
            }
            set
            {
                lock(_seedLocker)
                {
                    _UIndexSeed = value;
                }
            }
        }

        public bool IsThisNodeSeed0 => NodeService.Instance.PosWallet.AccountId == ProtocolSettings.Default.StandbyValidators[0];
        public ConsensusWorkingMode Mode { get; private set; }

        public class TransStat
        {
            public TimeSpan TS { get; set; }
            public BlockTypes TransType { get; set; }
        }
        private List<TransStat> _stats;

        public ConsensusService(IActorRef localNode)
        {
            _localNode = localNode;
            _log = new SimpleLogger("ConsensusService").Logger;

            _outOfOrderedMessages = new Dictionary<string, List<SourceSignedMessage>>();
            _activeConsensus = new Dictionary<string, AuthState>();
            _cleanedConsensus = new Dictionary<string, AuthState>();
            _stats = new List<TransStat>();

            _authorizers = new AuthorizersFactory();
            while (BlockChain.Singleton == null)
                Task.Delay(100).Wait();

            Mode = ConsensusWorkingMode.OutofSyncWaiting;

            Receive<Consolidate>((_) =>
            {
                //_log.LogInformation("Doing Consolidate");
                //OnNodeActive(NodeService.Instance.PosWallet.AccountId);     // update billboard

                //if (Mode == ConsensusWorkingMode.Normal && _board != null && _board.CanDoConsensus)
                //{
                //    Task.Run(async () =>
                //    {
                //        await GenerateConsolidateBlockAsync();
                //    });
                //}

                //BroadCastBillBoard();
            });

            Receive<BillBoard>((bb) =>
            {
                _board = bb;
            });

            Receive<AskForBillboard>((_) => Sender.Tell(_board));
            Receive<AskForStats>((_) => Sender.Tell(_stats));

            Receive<AuthorizingMsg>(async msg =>
            {
                OnNodeActive(NodeService.Instance.PosWallet.AccountId);     // update billboard

                if (msg.Version != LyraGlobal.ProtocolVersion || _board == null || !_board.CanDoConsensus)
                {
                    Sender.Tell(null);
                    return;
                }
                    
                var dtStart = DateTime.Now;

                // first try auth locally
                var state = CreateAuthringState(msg);
                if(state == null)
                {
                    Sender.Tell(null);
                    return;
                }
                var localAuthResult = LocalAuthorizingAsync(msg);
                state.AddAuthResult(localAuthResult);

                if (!localAuthResult.IsSuccess)
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
                    });

                    var ts = DateTime.Now - dtStart;
                    if (_stats.Count > 1000)
                        _stats.RemoveRange(0, 50);

                    _stats.Add(new TransStat { TS = ts, TransType = state.InputMsg.Block.BlockType });

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
                USeed = BlockChain.Singleton.FindLatestBlock().UIndex + 1;

                // declare to the network
                var msg = new ChatMsg
                {
                    From = NodeService.Instance.PosWallet.AccountId,
                    MsgType = ChatMessageType.NodeUp,
                    Text = "Staking with () Lyra"
                };

                Send2P2pNetwork(msg);
            });

            Task.Run(async () =>
            {
                while (true)
                {
                    OnNodeActive(NodeService.Instance.PosWallet.AccountId);     // update billboard

                    if (IsThisNodeSeed0 && Mode == ConsensusWorkingMode.Normal && _board != null && _board.CanDoConsensus)
                    {
                        _log.LogInformation("Doing Consolidate");
                        BroadCastBillBoard();

                        GenerateConsolidateBlock();
                    }

                    // declare to the network
                    var msg = new ChatMsg
                    {
                        From = NodeService.Instance.PosWallet.AccountId,
                        MsgType = ChatMessageType.HeartBeat,
                        Text = "I'm live"
                    };

                    Send2P2pNetwork(msg);

                    await Task.Delay(30000).ConfigureAwait(false);
                }
            });
        }

        private void GenerateConsolidateBlock()
        {
            // should lock the uindex seed here.
            // after clean, if necessary, insert a consolidate block into the queue
            // next time do clean, if no null block before the consolidate block, then send out the consolidate block.
            // 2 phase consolidation
            var lastCons = BlockChain.Singleton.GetSyncBlock();
            ConsolidationBlock currentCons = null;
            try
            {
                Monitor.Enter(_seedLocker);

                // first clean cleaned states
                var cleaned = _cleanedConsensus.Values.ToArray();
                for (int i = 0; i < cleaned.Length; i++)
                {
                    var state = cleaned[i];
                    if (DateTime.Now - state.Created > TimeSpan.FromMinutes(2)) // 2 mins
                    {
                        _cleanedConsensus.Remove(state.InputMsg.Block.Hash);
                    }
                }

                var states = _activeConsensus.Values.ToArray();
                for (int i = 0; i < states.Length; i++)
                {
                    var state = states[i];
                    if (DateTime.Now - state.Created > TimeSpan.FromSeconds(30)) // consensus timeout 30 seconds
                    {
                        _activeConsensus.Remove(state.InputMsg.Block.Hash);
                        state.Done.Set();

                        _cleanedConsensus.Add(state.InputMsg.Block.Hash, state);

                        if (state.IsConsensusSuccess == true)
                            continue;

                        // replace the failed block with nulltrans
                        var myAuthResult = state.OutputMsgs.FirstOrDefault(a => a.From == NodeService.Instance.PosWallet.AccountId);
                        if (myAuthResult == null)
                        {
                            // fatal error. should not happen
                            _log.LogError("No auth result from seed0. should not happen.");
                            continue;
                        }

                        var ndx = myAuthResult.BlockUIndex;
                        if (ndx == 0)    // not got yet
                            continue;

                        // check if the block is orphaned success block
                        var existingBlock = BlockChain.Singleton.GetBlockByUIndex(ndx);
                        if (existingBlock != null)
                        {
                            _log.LogInformation($"in GenerateConsolidateBlock: orphaned message for {ndx} detected.");
                            continue;
                        }

                        var nb = new NullTransactionBlock
                        {
                            UIndex = ndx,
                            FailedBlockHash = myAuthResult.BlockHash,
                            NetworkId = lastCons.NetworkId,
                            ShardId = lastCons.ShardId,
                            ServiceHash = lastCons.ServiceHash,
                            AccountID = NodeService.Instance.PosWallet.AccountId,
                            PreviousConsolidateHash = lastCons.Hash
                        };
                        nb.InitializeBlock(null, NodeService.Instance.PosWallet.PrivateKey,
                            lastCons.NetworkId, lastCons.ShardId,
                            NodeService.Instance.PosWallet.AccountId);
                        nb.UHash = SignableObject.CalculateHash($"{nb.UIndex}|{nb.Index}|{nb.Hash}");

                        SendServiceBlock(nb);
                    }
                }

                //// if necessary, insert a new ConsolidateBlock
                //if (USeed - lastCons.UIndex > 1024 || DateTime.Now - lastCons.TimeStamp > TimeSpan.FromMinutes(10))
                //{
                //    var authGenesis = BlockChain.Singleton.GetLastServiceBlock();
                //    currentCons = new ConsolidationBlock
                //    {
                //        UIndex = USeed++,
                //        NetworkId = authGenesis.NetworkId,
                //        ShardId = authGenesis.ShardId,
                //        ServiceHash = authGenesis.Hash,
                //        SvcAccountID = NodeService.Instance.PosWallet.AccountId
                //    };
                //}
            }
            catch (Exception ex)
            {
                _log.LogError("In GenerateConsolidateBlock: " + ex.Message);
            }
            finally
            {
                Monitor.Exit(_seedLocker);
            }

            //if (currentCons != null)
            //{
            //    var mt = new MerkleTree();
            //    for (var ndx = lastCons.UIndex + 1; ndx < currentCons.UIndex; ndx++)      // TODO: handling "losing" block here
            //    {
            //        var block = BlockChain.Singleton.GetBlockByUIndex(ndx);
            //        if (block == null)
            //        {
            //            _log.LogError("GenerateConsolidateBlock Fatal Error!!! should not happend.");
            //            Task.Delay(100000000).Wait();
            //        }
            //        var mhash = MerkleHash.Create(block.UHash);
            //        mt.AppendLeaf(mhash);
            //    }

            //    currentCons.MerkelTreeHash = mt.BuildTree().ToString();
            //    currentCons.InitializeBlock(lastCons, NodeService.Instance.PosWallet.PrivateKey,
            //        currentCons.NetworkId, currentCons.ShardId,
            //        NodeService.Instance.PosWallet.AccountId);

            //    currentCons.UHash = SignableObject.CalculateHash($"{currentCons.UIndex}|{currentCons.Index}|{currentCons.Hash}");

            //    SendServiceBlock(currentCons);

            //    // use merkle tree to consolidate all previous blocks, from lastCons.UIndex to xx[consBlock.UIndex -1]xx may lost the newest block
            //    // if the block is old enough ( > 2 mins ), it should be replaced by NullTransactionBlock.
            //    // in fact we should reserve consolidate block number and wait 2min to do consolidating
            //    // all null block's previous block is the last consolidate block, it's index is counted from 1 related to previous block
            //}
        }

        private AuthState SendServiceBlock(TransactionBlock svcBlock)
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
                _log.LogError($"Fatal Error: Consolidate block local authorization failed: {localAuthResult.Result}");
            }
            else
            {
                Send2P2pNetwork(msg);
                Send2P2pNetwork(localAuthResult);
            }

            return state;
        }

        public static Props Props(IActorRef localNode)
        {
            return Akka.Actor.Props.Create(() => new ConsensusService(localNode)).WithMailbox("consensus-service-mailbox");
        }

        public virtual void Send2P2pNetwork(SourceSignedMessage msg)
        {
            //_log.LogInformation($"Consensus: SendMessage Called: msg From: {msg.From}");

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
            //_log.LogInformation($"Consensus: OnNextAsyncImpl Called: msg From: {item.From}");

            // verify the signatures of msg. make sure it is from the right node.
            //var nodeConfig = null;
            if (!item.VerifySignature(item.From))
            {
                _log.LogInformation($"Consensus: bad signature: {item.Hash} sign: {item.Signature} by pubKey: {item.From}");
                _log.LogInformation($"Consensus: hash: {item.Hash} rehash: {item.CalculateHash()}");
                return;
            }

            OnNodeActive(item.From);

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
                case ChatMsg chat when chat.MsgType == ChatMessageType.HeartBeat:
                    OnHeartBeat(chat);
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
            if (!IsThisNodeSeed0)
            {
                var block = JsonConvert.DeserializeObject<ConsolidationBlock>(msg.Text);
                try
                {
                    BlockChain.Singleton.AddBlock(block);
                    _log.LogInformation($"Receive and store ConsolidateBlock of UIndex: {block.UIndex}");
                }
                catch(Exception e)
                {
                    _log.LogInformation($"OnBlockConsolication UIndex: {block.UIndex} Failed: {e.Message}");
                }                
            }
        }

        private void OnBillBoardBroadcast(ChatMsg msg)
        {
            if (!IsThisNodeSeed0) //TODO: only accept bbb from seeds
            {
                _board = JsonConvert.DeserializeObject<BillBoard>(msg.Text);
                _log.LogInformation("BillBoard updated!");
            }
        }

        private void BroadCastBillBoard()
        {
            if(_board != null)
            {
                var msg = new ChatMsg(NodeService.Instance.PosWallet.AccountId, JsonConvert.SerializeObject(_board));
                msg.MsgType = ChatMessageType.StakingChanges;
                Send2P2pNetwork(msg);
            }
        }

        private void OnNodeActive(string nodeAccountId)
        {
            if (_board != null)
                _board.Add(nodeAccountId);
        }

        private void OnHeartBeat(ChatMsg chat)
        {
            if (_board == null)
                return;

            var node = _board.Add(chat.From);
        }

        private void OnNodeUp(ChatMsg chat)
        {
            if (_board == null)
                return;

            var node = _board.Add(chat.From);

            if (IsThisNodeSeed0)
            {
                // broadcast billboard
                BroadCastBillBoard();
            }

            if (node.Balance < LyraGlobal.MinimalAuthorizerBalance)
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
            if (_outOfOrderedMessages.ContainsKey(item.Block.Hash))
            {
                var msgs = _outOfOrderedMessages[item.Block.Hash];
                _outOfOrderedMessages.Remove(item.Block.Hash);

                foreach (var msg in msgs)
                {
                    switch (msg)
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

            // check if block existing
            if (null != BlockChain.Singleton.FindBlockByHash(item.Block.Hash))
            {
                _log.LogInformation("CreateAuthringState: Block is already in database.");
                return null;
            }                

            // check if block was replaced by nulltrans
            if (null != BlockChain.Singleton.FindNullTransBlockByHash(item.Block.Hash))
            {
                _log.LogInformation("CreateAuthringState: Block is already consolidated by nulltrans.");
                return null;
            }

            _activeConsensus.Add(ukey, state);
            return state;
        }

        private AuthorizedMsg LocalAuthorizingAsync(AuthorizingMsg item)
        {
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
                    _log.LogWarning($"Give UIndex {USeed} to block {Shorten(item.Block.Hash)} of Type {item.Block.BlockType}");
                    result.BlockUIndex = USeed++;
                }
            }
            catch(Exception e)
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

            return result;
        }

        private void OnPrePrepare(AuthorizingMsg item)
        {
            _log.LogInformation($"Consensus: OnPrePrepare Called: BlockUIndex: {item.Block.UIndex}");

            var state = CreateAuthringState(item);
            if (state == null)
                return;

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

            if (_activeConsensus.ContainsKey(item.BlockHash))
            {
                var state = _activeConsensus[item.BlockHash];
                state.AddAuthResult(item);

                CheckAuthorizedAllOk(state);
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

        private void CheckAuthorizedAllOk(AuthState state)
        {
            // check state
            // debug: show all states
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine($"Block {Shorten(state.InputMsg.Block.AccountID)} {state.InputMsg.Block.BlockType} Index: {state.InputMsg.Block.Index} Hash: {Shorten(state.InputMsg.Block.Hash)}");
            foreach(var msg in state.OutputMsgs)
            {
                var seed0 = msg.From == ProtocolSettings.Default.StandbyValidators[0] ? "[seed0]" : "";
                var voice = msg.IsSuccess ? "Yay" : "Nay";
                sb.AppendLine($"{voice} {msg.Result} By: {Shorten(msg.From)} CanAuth: {_board.AllNodes[msg.From].AbleToAuthorize} {seed0}");
            }
            _log.LogInformation(sb.ToString());

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

                    try
                    {
                        BlockChain.Singleton.AddBlock(block);
                        _log.LogInformation($"CheckAuthorizedAllOk of UIndex: {block.UIndex}");
                    }
                    catch (Exception e)
                    {
                        _log.LogInformation($"CheckAuthorizedAllOk Failed UIndex: {block.UIndex} Why: {e.Message}");
                        return;
                    }

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

            if (_activeConsensus.ContainsKey(item.BlockHash))
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

        private string Shorten(string addr)
        {
            if (string.IsNullOrWhiteSpace(addr) || addr.Length < 10)
                return addr;

            return $"{addr.Substring(0, 3)}...{addr.Substring(addr.Length - 6, 6)}";
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
