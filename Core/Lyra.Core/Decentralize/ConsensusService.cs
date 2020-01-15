using Akka.Actor;
using Akka.Cluster.Routing;
using Akka.Configuration;
using Akka.Routing;
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Neo.Network.P2P.LocalNode;

namespace Lyra.Core.Decentralize
{
    public class TransStats
    {
        public long ms { get; set; }
        public BlockTypes trans { get; set; }
    }
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
        private readonly IActorRef _router;

        ILogger _log;

        // hash, authState
        Dictionary<string, List<SourceSignedMessage>> _outOfOrderedMessages;
        Dictionary<string, AuthState> _activeConsensus;
        Dictionary<string, AuthState> _cleanedConsensus;
        private BillBoard _board;
        private List<TransStats> _stats;

        private long _UIndexSeed = -1;
        private object _seedLocker = new object();

        public long USeed
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
        public BillBoard Board { get => _board; set => _board = value; }
        public List<TransStats> Stats { get => _stats; set => _stats = value; }

        public ConsensusService(IActorRef localNode)
        {
            _localNode = localNode;
            _log = new SimpleLogger("ConsensusService").Logger;

            _outOfOrderedMessages = new Dictionary<string, List<SourceSignedMessage>>();
            _activeConsensus = new Dictionary<string, AuthState>();
            _cleanedConsensus = new Dictionary<string, AuthState>();
            _stats = new List<TransStats>();

            while (BlockChain.Singleton == null)
                Task.Delay(100).Wait();

            Mode = ConsensusWorkingMode.OutofSyncWaiting;

            var pool = new ConsistentHashingPool(50).WithHashMapping(o => o switch
            {
                AuthorizingMsg msg1 => msg1.Block.Hash,
                AuthorizedMsg msg2 => msg2.BlockHash,
                AuthorizerCommitMsg msg3 => msg3.BlockHash,
                AuthState state => state.HashOfFirstBlock,
                _ => null,
            });

            _router = LyraSystem.Singleton.ActorSystem.ActorOf(Akka.Actor.Props.Create<ConsensusWorker>(() => 
                new ConsensusWorker(this)
                )
                .WithRouter(pool), "some-pool");

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

            //Receive<SignedMessageRelay>(relayMsg =>
            //{
            //    if (relayMsg.signedMessage.Version == LyraGlobal.ProtocolVersion)
            //        OnNextConsensusMessage(relayMsg.signedMessage);
            //    else
            //        _log.LogWarning("Protocol Version Mismatch. Do nothing.");
            //});

            Receive<BlockChainSynced>(_ =>
            {
                Mode = ConsensusWorkingMode.Normal;
                USeed = BlockChain.Singleton.FindLatestBlock().UIndex + 1;

                _log.LogInformation($"The USeed is {USeed}");

                // declare to the network
                var msg = new ChatMsg
                {
                    From = NodeService.Instance.PosWallet.AccountId,
                    MsgType = ChatMessageType.NodeUp,
                    Text = "Staking with () Lyra"
                };
                msg.Sign(NodeService.Instance.PosWallet.PrivateKey, msg.From);

                Send2P2pNetwork(msg);
            });

            Receive<TransactionBlock>(block =>
            {

                //TODO: check  || _context.Board == null || !_context.Board.CanDoConsensus

                AuthorizingMsg msg = new AuthorizingMsg
                {
                    From = NodeService.Instance.PosWallet.AccountId,
                    Block = block,
                    MsgType = ChatMessageType.AuthorizerPrePrepare
                };
                msg.Sign(NodeService.Instance.PosWallet.PrivateKey, msg.From);

                var state = new AuthState
                {
                    HashOfFirstBlock = msg.Block.Hash,
                    InputMsg = msg
                };

                _router.Tell(state);
                Sender.Tell(state, Self);
            });

            Task.Run(async () =>
            {
                HeartBeat();
                int count = 0;
                while (true)
                {
                    if (Mode == ConsensusWorkingMode.Normal)
                    {
                        GenerateConsolidateBlock();
                    }

                    await Task.Delay(10000).ConfigureAwait(false);
                    count++;

                    if(count >= 3)
                    {
                        HeartBeat();
                        count = 0;
                    }
                }
            });
        }


        private void HeartBeat()
        {
            OnNodeActive(NodeService.Instance.PosWallet.AccountId);     // update billboard

            // declare to the network
            var msg = new ChatMsg
            {
                From = NodeService.Instance.PosWallet.AccountId,
                MsgType = ChatMessageType.HeartBeat,
                Text = "I'm live"
            };
            msg.Sign(NodeService.Instance.PosWallet.PrivateKey, msg.From);

            Send2P2pNetwork(msg);

            if(IsThisNodeSeed0)
            {
                BroadCastBillBoard();
            }
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
                    if (DateTime.Now - state.Created > TimeSpan.FromSeconds(10)) // consensus timeout
                    {
                        var finalResult = state.GetIsAuthoringSuccess(_board);
                        _activeConsensus.Remove(state.InputMsg.Block.Hash);
                        state.Done.Set();

                        _cleanedConsensus.Add(state.InputMsg.Block.Hash, state);

                        //if (finalResult == true)
                        //    continue;

                        //// replace the failed block with nulltrans
                        //var myAuthResult = state.OutputMsgs.FirstOrDefault(a => a.From == NodeService.Instance.PosWallet.AccountId);
                        //if (myAuthResult == null)
                        //{
                        //    // fatal error. should not happen
                        //    _log.LogError("No auth result from seed0. should not happen.");
                        //    continue;
                        //}

                        //var ndx = myAuthResult.BlockUIndex;
                        //if (ndx == 0)    // not got yet
                        //    continue;

                        //// check if the block is orphaned success block
                        //var existingBlock = BlockChain.Singleton.GetBlockByUIndex(ndx);
                        //if (existingBlock != null)
                        //{
                        //    _log.LogInformation($"in GenerateConsolidateBlock: orphaned message for {ndx} detected.");
                        //    continue;
                        //}
                        
                        // no need for this. just leave it as hole
                        //var nb = new NullTransactionBlock
                        //{
                        //    UIndex = ndx,
                        //    FailedBlockHash = myAuthResult.BlockHash,
                        //    NetworkId = lastCons.NetworkId,
                        //    ShardId = lastCons.ShardId,
                        //    ServiceHash = lastCons.ServiceHash,
                        //    AccountID = NodeService.Instance.PosWallet.AccountId,
                        //    PreviousConsolidateHash = lastCons.Hash
                        //};
                        //nb.InitializeBlock(null, NodeService.Instance.PosWallet.PrivateKey,
                        //    lastCons.NetworkId, lastCons.ShardId,
                        //    NodeService.Instance.PosWallet.AccountId);
                        //nb.UHash = SignableObject.CalculateHash($"{nb.UIndex}|{nb.Index}|{nb.Hash}");

                        //SendServiceBlock(nb);
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
                _log.LogError("Error In GenerateConsolidateBlock: " + ex.Message);
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

        //private AuthState SendServiceBlock(TransactionBlock svcBlock)
        //{
        //    AuthorizingMsg msg = new AuthorizingMsg
        //    {
        //        From = NodeService.Instance.PosWallet.AccountId,
        //        Block = svcBlock,
        //        MsgType = ChatMessageType.AuthorizerPrePrepare
        //    };

        //    var state = CreateAuthringState(msg);
        //    var localAuthResult = LocalAuthorizingAsync(msg);
        //    state.AddAuthResult(localAuthResult);

        //    if (!localAuthResult.IsSuccess)
        //    {
        //        _log.LogError($"Fatal Error: Consolidate block local authorization failed: {localAuthResult.Result}");
        //    }
        //    else
        //    {
        //        Send2P2pNetwork(msg);
        //        Send2P2pNetwork(localAuthResult);
        //    }

        //    return state;
        //}

        public static Props Props(IActorRef localNode)
        {
            return Akka.Actor.Props.Create(() => new ConsensusService(localNode)).WithMailbox("consensus-service-mailbox");
        }

        public virtual void Send2P2pNetwork(SourceSignedMessage msg)
        {
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
                case AuthorizingMsg _:
                case AuthorizedMsg _:
                case AuthorizerCommitMsg _:
                    _router.Tell(item);
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
                msg.Sign(NodeService.Instance.PosWallet.PrivateKey, msg.From);
                Send2P2pNetwork(msg);
            }
        }

        public void OnNodeActive(string nodeAccountId)
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
