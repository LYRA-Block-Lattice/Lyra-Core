using Akka.Actor;
using Akka.Configuration;
using Clifton.Blockchain;
using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Cryptography;
using Lyra.Core.Decentralize;
using Lyra.Core.Utils;
using Lyra.Shared;
using Microsoft.Extensions.Logging;
using Neo;
using Neo.Cryptography.ECC;
using Neo.IO.Actors;
using Stateless;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Settings = Neo.Settings;

namespace Lyra
{
    public enum BlockChainState 
    { 
        Initializing, 
        Startup,    // the default mode. app started. wait for p2p stack up.
        Engaging,   // storing new commit while syncing blocks
        Almighty,   // fullly synced and working
        Genesis
    }

    public enum BlockChainTrigger
    {
        // initializing
        LocalNodeStartup,

        // startup
        QueryingConsensusNode,
        ConsensusBlockChainEmpty,
        ConsensusNodesSynced,

        // engage
        LocalNodeConsolidated,

        // almighty
        LocalNodeOutOfSync,

        // genesis
        GenesisDone
    }

    public class BlockChain : UntypedActor
    {
        public class NeedSync { public long ToUIndex { get; set; } }
        public class Startup { }
        public class PersistCompleted { }
        public class Import { }
        public class ImportCompleted { }
        public class FillMemoryPool { public IEnumerable<Transaction> Transactions; }
        public class FillCompleted { }
        public class BlockAdded {
            public string hash { get; set; }
        }

        public static bool IsThisNodeSeed0 => NodeService.Instance.PosWallet.AccountId == ProtocolSettings.Default.StandbyValidators[0];
        private LyraRestClient _seed0Client;

        //private long _UIndexSeed = 0;
        //public async Task<long> GenSeedAsync()
        //{
        //    if(ConsensusService.IsThisNodeSeed0)
        //        return _UIndexSeed++;
        //    else
        //    {
        //        // assign UID from seed0
        //        var uidResult = await (await GetClientForSeed0()).CreateBlockUId(
        //            NodeService.Instance.PosWallet.AccountId,
        //            Signatures.GetSignature(NodeService.Instance.PosWallet.PrivateKey, hash, NodeService.Instance.PosWallet.AccountId),
        //            hash);
        //        if (uidResult.ResultCode != APIResultCodes.Success)
        //        {
        //            return uidResult.uid;
        //        }
        //        else
        //            return -1;
        //    }
        //}

        public static BlockChain Singleton;
        public static readonly ECPoint[] StandbyValidators = ProtocolSettings.Default.StandbyValidators.OfType<string>().Select(p => //ECPoint.DecodePoint(p.HexToBytes(), ECCurve.Secp256r1)).ToArray();
                                                        ECPoint.FromBytes(Base58Encoding.DecodeAccountId(p), ECCurve.Secp256r1)).ToArray();

        public uint Height;
        public string NetworkID { get; private set; }
        public bool InSyncing { get; private set; }

        private readonly StateMachine<BlockChainState, BlockChainTrigger> _stateMachine;
        private readonly StateMachine<BlockChainState, BlockChainTrigger>.TriggerWithParameters<long> _engageTriggerStartupSync;
        private readonly StateMachine<BlockChainState, BlockChainTrigger>.TriggerWithParameters<string> _engageTriggerConsolidateFailed;
        public BlockChainState CurrentState => _stateMachine.State;

        AuthorizersFactory _authorizerFactory = new AuthorizersFactory();

        private LyraConfig _nodeConfig;
        private readonly IAccountCollectionAsync _store;
        private LyraSystem _sys;
        private ILogger _log;

        // status inquiry
        private List<NodeStatus> _nodeStatus;
        public BlockChain(LyraSystem sys)
        {
            if (Singleton != null)
                throw new Exception("Blockchain reinitialization");

            _sys = sys;

            _stateMachine = new StateMachine<BlockChainState, BlockChainTrigger>(BlockChainState.Initializing);
            _engageTriggerStartupSync = _stateMachine.SetTriggerParameters<long>(BlockChainTrigger.ConsensusNodesSynced);
            _engageTriggerConsolidateFailed = _stateMachine.SetTriggerParameters<string>(BlockChainTrigger.LocalNodeOutOfSync);
            CreateStateMachine();

            var nodeConfig = Neo.Settings.Default.LyraNode;
            _store = new MongoAccountCollection();

            //_store = new LiteAccountCollection(Utilities.LyraDataDir);
            _log = new SimpleLogger("BlockChain").Logger;
            _nodeConfig = nodeConfig;
            NetworkID = nodeConfig.Lyra.NetworkId;

            ResetUIDAsync().Wait();

            Singleton = this;
        }
        public static Props Props(LyraSystem system)
        {
            return Akka.Actor.Props.Create(() => new BlockChain(system)).WithMailbox("blockchain-mailbox");
        }

        private async Task ResetUIDAsync()
        {
            long uid = -1;
            if(_sys.Consensus != null)
            {
                var uidObj = await _sys.Consensus.Ask(new ConsensusService.AskForMaxActiveUID()) as ConsensusService.ReplyForMaxActiveUID;
                if (uidObj != null && uidObj.uid.HasValue)
                {
                    uid = uidObj.uid.Value;
                }
            }
        }

        private void CreateStateMachine()
        {
            _stateMachine.Configure(BlockChainState.Initializing)
                .Permit(BlockChainTrigger.LocalNodeStartup, BlockChainState.Startup);

            _stateMachine.Configure(BlockChainState.Startup)
                .PermitReentry(BlockChainTrigger.QueryingConsensusNode)
                .OnEntry(() => Task.Run(async () =>
                {
                    while (Neo.Network.P2P.LocalNode.Singleton.ConnectedCount < 2)
                    {
                        await Task.Delay(1000);
                    }

                    _sys.Consensus.Tell(new ConsensusService.Startup());

                    _nodeStatus = new List<NodeStatus>();
                    _sys.Consensus.Tell(new ConsensusService.NodeInquiry());

                    await Task.Delay(10000);

                    var q = from ns in _nodeStatus
                            where ConsensusService.Board.PrimaryAuthorizers.Contains(ns.accountId) 
                            group ns by ns.totalBlockCount into heights
                            orderby heights.Count()
                            select new
                            {
                                Height = heights.Key,
                                Count = heights.Count()
                            };

                    if (q.Any())
                    {
                        var majorHeight = q.First();

                        _log.LogInformation($"CheckInquiryResult: Major Height = {majorHeight.Height} of {majorHeight.Count}");

                        var myStatus = await GetNodeStatusAsync();
                        if (myStatus.totalBlockCount == 0 && majorHeight.Height == 0 && majorHeight.Count >= 2)
                        {
                            _stateMachine.Fire(BlockChainTrigger.ConsensusBlockChainEmpty);
                        }
                        else if (myStatus.totalBlockCount <= majorHeight.Height && majorHeight.Height >= 2 && majorHeight.Count >= 2)
                        {
                            _stateMachine.Fire(_engageTriggerStartupSync, majorHeight.Height);
                        }
                        //else if (majorHeight.Height > 2 && majorHeight.Count < 2)
                        //{
                        //    _state.Fire(BlockChainTrigger.ConsensusNodesOutOfSync);
                        //}
                        else
                        {
                            _stateMachine.Fire(BlockChainTrigger.QueryingConsensusNode);
                        }
                    }
                }))
                .Permit(BlockChainTrigger.ConsensusBlockChainEmpty, BlockChainState.Genesis)
                //.Permit(BlockChainTrigger.ConsensusNodesOutOfSync, BlockChainState.Failed)
                .Permit(BlockChainTrigger.ConsensusNodesSynced, BlockChainState.Engaging);

            _stateMachine.Configure(BlockChainState.Genesis)
                .OnEntry(() => Task.Run(async () =>
                {
                    await ResetUIDAsync();
                    if (await FindLatestBlockAsync() == null && ConsensusService.IsThisNodeSeed0)
                    {
                        Genesis();
                    }
                }))
                .Permit(BlockChainTrigger.GenesisDone, BlockChainState.Startup);

            _stateMachine.Configure(BlockChainState.Engaging)
                .OnEntryFrom(_engageTriggerStartupSync, (uid) => Task.Run(async () =>
                {
                    // loop check here 
                    var lastCons = await GetLastConsolidationBlockAsync();
                    if(lastCons.totalBlockCount == uid)
                    {
                        // the latest
                        _stateMachine.Fire(BlockChainTrigger.LocalNodeConsolidated);
                    }
                    else
                    {
                        // do sync with others
                    }

                    //long startUIndex = 0;
                    //// sync blocks + save letest
                    //await SyncManyBlocksAsync(startUIndex, uid, false);
                    //startUIndex = uid;

                    //var consolidateToUIndex = _lastSavedUIndex;
                    //do
                    //{
                    //    if (startUIndex >= _lastSavedUIndex)
                    //    {
                    //        _stateMachine.Fire(BlockChainTrigger.LocalNodeConsolidated);
                    //        break;
                    //    }
                    //    else
                    //    {
                    //        await SyncManyBlocksAsync(startUIndex, _lastSavedUIndex, false);
                    //        startUIndex = _lastSavedUIndex;
                    //    }
                    //    await Task.Delay(1000);
                    //} while (true);

                    // TODO: consolidate all blocks to make sure no error.
                    // start a new thread to verify and switch state if necessary.
                }))
                .OnEntryFrom(_engageTriggerConsolidateFailed, (hash) => Task.Run(async () =>
                {
                    var block = await FindBlockByHashAsync(hash) as ConsolidationBlock;
                    if (block == null)
                    {
                        // should not happen
                        _log.LogCritical("Can't find block for ConsolidateFailed hash: " + hash);
                    }
                    else
                    {
                        //var mt = new MerkleTree();
                        //var emptyNdx = new List<long>();
                        //for (var ndx = block.StartUIndex; ndx <= block.EndUIndex; ndx++)
                        //{
                        //    Block bndx = null;// await BlockChain.Singleton.GetBlockByUIndexAsync(ndx);

                        //    if (bndx == null)
                        //    {
                        //        if (block.NullUIndexes != null && block.NullUIndexes.Contains(ndx))
                        //            continue;
                        //        else
                        //        {
                        //            // missing block
                        //            // fetch, save
                        //            var ret = await BlockChain.Singleton.SyncOneBlock(ndx, false);

                        //            // check if result is ok

                        //            ndx--;
                        //            continue;
                        //        }
                        //    }
                        //    else
                        //    {
                        //        if (block.NullUIndexes != null && block.NullUIndexes.Contains(ndx))
                        //        {
                        //            // extra block
                        //            // remove block
                        //            await BlockChain.Singleton.RemoveBlockAsync(ndx);
                        //            continue;
                        //        }
                        //        else
                        //        {
                        //            mt.AppendLeaf(MerkleHash.Create(bndx.Hash));
                        //        }
                        //    }
                        //}

                        //var mkhash = mt.BuildTree().ToString();
                        //if (block.MerkelTreeHash == mkhash)
                        //{
                        //    // success
                        //    _log.LogInformation("ConsolidateFailed fixed OK.");
                        //}
                        //else
                        //{
                        //    _log.LogCritical("ConsolidateFailed can't fix.");
                        //}
                    }
                }))
                .Permit(BlockChainTrigger.LocalNodeConsolidated, BlockChainState.Almighty);

            _stateMachine.Configure(BlockChainState.Almighty)
                .OnEntry(() => Task.Run(async () =>
                {
                    await ResetUIDAsync();
                }))
                .Permit(BlockChainTrigger.LocalNodeOutOfSync, BlockChainState.Startup);

            _stateMachine.OnTransitioned(t => _log.LogWarning($"OnTransitioned: {t.Source} -> {t.Destination} via {t.Trigger}({string.Join(", ", t.Parameters)})"));
        }

        private void Genesis()
        {
            Task.Run(async () =>
            {
                // genesis
                _log.LogInformation("all seed nodes are ready. do genesis.");

                var svcGen = await GetServiceGenesisBlockAsync();
                await SendBlockToConsensusAsync(svcGen);

                await Task.Delay(1000);

                var tokenGen = await GetLyraTokenGenesisBlockAsync(svcGen);
                await SendBlockToConsensusAsync(tokenGen);

                await Task.Delay(1000);

                var consGen = await GetConsolidationGenesisBlockAsync(svcGen, tokenGen);
                await SendBlockToConsensusAsync(consGen);

                await Task.Delay(1000);

                _log.LogInformation("svc genesis is done.");

                await Task.Delay(3000);

                _stateMachine.Fire(BlockChainTrigger.GenesisDone);
            });
        }

        public void ConsolidationBlockFailed(string hash)
        {
            if(_stateMachine.State == BlockChainState.Almighty)
                _stateMachine.Fire(_engageTriggerConsolidateFailed, hash);
        }

        public void AuthorizerCountChanged(int count)
        {

        }

        public async Task<NodeStatus> GetNodeStatusAsync()
        {
            var lastCons = await GetLastConsolidationBlockAsync();
            var status = new NodeStatus
            {
                accountId = NodeService.Instance.PosWallet.AccountId,
                version = LyraGlobal.NodeAppName,
                mode = _stateMachine.State,
                totalBlockCount = lastCons == null ? 0 : lastCons.totalBlockCount,
                lastConsolidationHash = lastCons?.Hash,
                lastUnSolidationHash = null,
                connectedPeers = Neo.Network.P2P.LocalNode.Singleton.ConnectedCount
            };
            return status;
        }

        #region storage api
        private async Task<bool> AddBlockImplAsync(Block block)
        {
            var result = await _store.AddBlockAsync(block);
            if (result)
            {
                LyraSystem.Singleton.Consensus.Tell(new BlockAdded { hash = block.Hash });
            }

            if(block is ConsolidationBlock)
            {
                // we need to update the consolidation flag
                foreach(var hash in (block as ConsolidationBlock).blockHashes)
                {
                    if (!await _store.ConsolidateBlock(hash))
                        _log.LogCritical($"BlockChain Not consolidate block properly: {hash}");
                }
            }
            return result;
        }

        internal async Task<ConsolidationBlock> GetLastConsolidationBlockAsync() => await StopWatcher.Track(_store.GetLastConsolidationBlockAsync(), StopWatcher.GetCurrentMethod());//_store.GetSyncBlockAsync();
        public async Task<List<ConsolidationBlock>> GetConsolidationBlocksAsync(long startUIndex) => await StopWatcher.Track(_store.GetConsolidationBlocksAsync(startUIndex), StopWatcher.GetCurrentMethod());
        internal async Task<ServiceBlock> GetLastServiceBlockAsync() => await StopWatcher.Track(_store.GetLastServiceBlockAsync(), StopWatcher.GetCurrentMethod());//_store.GetLastServiceBlockAsync();

        // forward api. should have more control here.
        public async Task<bool> AddBlockAsync(Block block) => await StopWatcher.Track(AddBlockImplAsync(block), StopWatcher.GetCurrentMethod());
        public async Task RemoveBlockAsync(string hash) => await _store.RemoveBlockAsync(hash);
        public async Task AddBlockAsync(ServiceBlock serviceBlock) => await StopWatcher.Track(_store.AddBlockAsync(serviceBlock), StopWatcher.GetCurrentMethod());//_store.AddBlockAsync(serviceBlock);

        // bellow readonly access
        public async Task<bool> AccountExistsAsync(string AccountId) => await StopWatcher.Track(_store.AccountExistsAsync(AccountId), StopWatcher.GetCurrentMethod());//_store.AccountExistsAsync(AccountId);
        public async Task<Block> FindLatestBlockAsync() => await StopWatcher.Track(_store.FindLatestBlockAsync(), StopWatcher.GetCurrentMethod());//_store.FindLatestBlockAsync();
        public async Task<Block> FindLatestBlockAsync(string AccountId) => await StopWatcher.Track(_store.FindLatestBlockAsync(AccountId), StopWatcher.GetCurrentMethod());//_store.FindLatestBlockAsync(AccountId);
        public async Task<NullTransactionBlock> FindNullTransBlockByHashAsync(string hash) => await StopWatcher.Track(_store.FindNullTransBlockByHashAsync(hash), StopWatcher.GetCurrentMethod());//_store.FindNullTransBlockByHashAsync(hash);
        public async Task<Block> FindBlockByHashAsync(string hash) => await StopWatcher.Track(_store.FindBlockByHashAsync(hash), StopWatcher.GetCurrentMethod());//_store.FindBlockByHashAsync(hash);
        public async Task<Block> FindBlockByHashAsync(string AccountId, string hash) => await StopWatcher.Track(_store.FindBlockByHashAsync(AccountId, hash), StopWatcher.GetCurrentMethod());//_store.FindBlockByHashAsync(AccountId, hash);
        public async Task<List<TokenGenesisBlock>> FindTokenGenesisBlocksAsync(string keyword) => await StopWatcher.Track(_store.FindTokenGenesisBlocksAsync(keyword), StopWatcher.GetCurrentMethod());//_store.FindTokenGenesisBlocksAsync(keyword);
        public async Task<TokenGenesisBlock> FindTokenGenesisBlockAsync(string Hash, string Ticker) => await StopWatcher.Track(_store.FindTokenGenesisBlockAsync(Hash, Ticker), StopWatcher.GetCurrentMethod());//_store.FindTokenGenesisBlockAsync(Hash, Ticker);
        public async Task<ReceiveTransferBlock> FindBlockBySourceHashAsync(string hash) => await StopWatcher.Track(_store.FindBlockBySourceHashAsync(hash), StopWatcher.GetCurrentMethod());//_store.FindBlockBySourceHashAsync(hash);
        public async Task<long> GetBlockCountAsync() => await StopWatcher.Track(_store.GetBlockCountAsync(), StopWatcher.GetCurrentMethod());//_store.GetBlockCountAsync();
        public async Task<TransactionBlock> FindBlockByIndexAsync(string AccountId, long index) => await StopWatcher.Track(_store.FindBlockByIndexAsync(AccountId, index), StopWatcher.GetCurrentMethod());//_store.FindBlockByIndexAsync(AccountId, index);
        public async Task<List<NonFungibleToken>> GetNonFungibleTokensAsync(string AccountId) => await StopWatcher.Track(_store.GetNonFungibleTokensAsync(AccountId), StopWatcher.GetCurrentMethod());//_store.GetNonFungibleTokensAsync(AccountId);
        public async Task<SendTransferBlock> FindUnsettledSendBlockAsync(string AccountId) => await StopWatcher.Track(_store.FindUnsettledSendBlockAsync(AccountId), StopWatcher.GetCurrentMethod());//_store.FindUnsettledSendBlockAsync(AccountId);
        public async Task<TransactionBlock> FindBlockByPreviousBlockHashAsync(string previousBlockHash) => await StopWatcher.Track(_store.FindBlockByPreviousBlockHashAsync(previousBlockHash), StopWatcher.GetCurrentMethod());//_store.FindBlockByPreviousBlockHashAsync(previousBlockHash);
        #endregion
        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case NeedSync cmd:
                    SyncBlocksFromSeeds(cmd.ToUIndex);
                    break;
                case Startup _:
                    _stateMachine.Fire(BlockChainTrigger.LocalNodeStartup);
                    break;
                case NodeStatus nodeStatus:
                    // only accept status from seeds.
                    _log.LogInformation($"NodeStatus from {nodeStatus.accountId.Shorten()}");
                    if (_nodeStatus != null)
                    {
                        if (!_nodeStatus.Any(a => a.accountId == nodeStatus.accountId))
                            _nodeStatus.Add(nodeStatus);
                    }
                    break;
                    //    case Import import:
                    //        OnImport(import.Blocks);
                    //        break;
                    //    case FillMemoryPool fill:
                    //        OnFillMemoryPool(fill.Transactions);
                    //        break;
                    //    case Header[] headers:
                    //        OnNewHeaders(headers);
                    //        break;
                    //    case Block block:
                    //        Sender.Tell(OnNewBlock(block));
                    //        break;
                    //    case Transaction[] transactions:
                    //        {
                    //            // This message comes from a mempool's revalidation, already relayed
                    //            foreach (var tx in transactions) OnNewTransaction(tx, false);
                    //            break;
                    //        }
                    //    case Transaction transaction:
                    //        Sender.Tell(OnNewTransaction(transaction, true));
                    //        break;
                    //    case ConsensusPayload payload:
                    //        Sender.Tell(OnNewConsensus(payload));
                    //        break;
                    //    case Idle _:
                    //        if (MemPool.ReVerifyTopUnverifiedTransactionsIfNeeded(MaxTxToReverifyPerIdle, currentSnapshot))
                    //            Self.Tell(Idle.Instance, ActorRefs.NoSender);
                    //        break;
            }
        }

        public async Task<LyraRestClient> GetClientForSeed0()
        {
            if (_seed0Client == null)
            {
                var addr = ProtocolSettings.Default.SeedList[0].Split(':')[0];
                var apiUrl = $"https://{addr}:4505/api/LyraNode/";
                _log.LogInformation("Platform {1} Use seed node of {0}", apiUrl, Environment.OSVersion.Platform);
                _seed0Client = await LyraRestClient.CreateAsync(NetworkID, Environment.OSVersion.Platform.ToString(), "LyraNode2", "1.0", apiUrl);

            }
            return _seed0Client;
        }

        public async Task<ConsolidationBlock> GetConsolidationGenesisBlockAsync(ServiceBlock svcGen, LyraTokenGenesisBlock lyraGen)
        {
            var consBlock = new ConsolidationBlock
            {
                blockHashes = new List<string>()
                {
                    svcGen.Hash, lyraGen.Hash
                },
                totalBlockCount = 2     // not including self
            };

            var mt = new MerkleTree();
            mt.AppendLeaf(MerkleHash.Create(svcGen.Hash));
            mt.AppendLeaf(MerkleHash.Create(lyraGen.Hash));

            consBlock.MerkelTreeHash = mt.BuildTree().ToString();
            consBlock.InitializeBlock(null, NodeService.Instance.PosWallet.PrivateKey,
                NodeService.Instance.PosWallet.AccountId,
                await GetClientForSeed0());

            return consBlock;
        }

        public async Task<LyraTokenGenesisBlock> GetLyraTokenGenesisBlockAsync(ServiceBlock svcGen)
        {
            // initiate test coins
            var openTokenGenesisBlock = new LyraTokenGenesisBlock
            {
                Height = 1,
                AccountType = AccountTypes.Standard,
                Ticker = LyraGlobal.LYRATICKERCODE,
                DomainName = "Lyra",
                ContractType = ContractTypes.Cryptocurrency,
                Description = "Lyra Permissioned Gas Token",
                Precision = LyraGlobal.LYRAPRECISION,
                IsFinalSupply = true,
                AccountID = NodeService.Instance.PosWallet.AccountId,
                Balances = new Dictionary<string, decimal>(),
                PreviousHash = svcGen.Hash,
                ServiceHash = svcGen.Hash,
                Fee = svcGen.TokenGenerationFee,
                FeeType = AuthorizationFeeTypes.Regular,
                Icon = "https://i.imgur.com/L3h0J1K.png",
                Image = "https://i.imgur.com/B8l4ZG5.png",
                RenewalDate = DateTime.Now.AddYears(1000)
            };
            var transaction = new TransactionInfo() { TokenCode = openTokenGenesisBlock.Ticker, Amount = LyraGlobal.LYRAGENESISAMOUNT };
            openTokenGenesisBlock.Balances.Add(transaction.TokenCode, transaction.Amount); // This is current supply in atomic units (1,000,000.00)
            openTokenGenesisBlock.InitializeBlock(null, NodeService.Instance.PosWallet.PrivateKey, AccountId: NodeService.Instance.PosWallet.AccountId, await GetClientForSeed0());

            return openTokenGenesisBlock;
        }

        public async Task<ServiceBlock> GetServiceGenesisBlockAsync()
        {
            var svcGenesis = new ServiceBlock
            {
                NetworkId = NetworkID,
                Height = 1,
                TransferFee = 1,
                TokenGenerationFee = 100,
                TradeFee = 0.1m
            };

            svcGenesis.Authorizers = new List<PosNode>();
            foreach(var pn in ConsensusService.Board.AllNodes.Values.Where(a => ProtocolSettings.Default.StandbyValidators.Contains(a.AccountID)))
            {
                svcGenesis.Authorizers.Add(pn);
            }
            svcGenesis.InitializeBlock(null, NodeService.Instance.PosWallet.PrivateKey,
                NodeService.Instance.PosWallet.AccountId,
                await GetClientForSeed0());
            return svcGenesis;
        }

        private async Task SendBlockToConsensusAsync(Block block)
        {
            AuthorizingMsg msg = new AuthorizingMsg
            {
                From = NodeService.Instance.PosWallet.AccountId,
                Block = block,
                MsgType = ChatMessageType.AuthorizerPrePrepare
            };

            var state = new AuthState(true);
            state.HashOfFirstBlock = block.Hash;
            state.InputMsg = msg;

            _sys.Consensus.Tell(state);

            await state.Done.AsTask();
            state.Done.Close();
            state.Done = null;
        }

        private async Task SyncManyBlocksAsync(long startUid, long endUid, bool withAuthorize)
        {
            //for (long i = startUid; i <= endUid; i++)
            //{
            //    var myBlock = await GetBlockByUIndexAsync(i);
            //    if (myBlock != null)
            //        continue;

            //    if (await SyncOneBlock(i, withAuthorize))
            //    {
            //        _log.LogInformation($"Block {i} synced.");
            //    }
            //    else
            //    {
            //        _log.LogInformation($"Block {i} failed to sync.");
            //    }
            //}
        }

        LyraRestClient _clientForSync;
        public async Task<bool> SyncOneBlock(long uid, bool withAuthorize)
        {/*
            _log.LogInformation($"SyncOneBlock: {uid}");

            if (_clientForSync == null)
                _clientForSync = await FindValidSeedForSyncAsync();

            var result = await _clientForSync.GetBlockByUIndex(uid);
            if (result.ResultCode == APIResultCodes.Success)
            {
                var block = result.GetBlock();

                if(withAuthorize)
                {
                    var authorizer = _authorizerFactory.Create(block.BlockType);
                    var localAuthResult = await authorizer.AuthorizeAsync(block);
                    if (localAuthResult.Item1 == APIResultCodes.Success)
                        return await _store.AddBlockAsync(block);       // use this api directly to avoid confuse with the consensused block add
                    else
                    {
                        _log.LogError($"Engaging: unable to authorize block {uid}");
                    }
                }
                else
                {
                    return await _store.AddBlockAsync(block);       // use this api directly to avoid confuse with the consensused block add
                }
            }*/
            return false;
        }

        private async Task<LyraRestClient> FindValidSeedForSyncAsync()
        {
            do
            {
                var rand = new Random();
                int ndx;
                do
                {
                    ndx = rand.Next(0, ProtocolSettings.Default.SeedList.Length);
                } while (NodeService.Instance.PosWallet.AccountId == ProtocolSettings.Default.StandbyValidators[ndx]);

                var addr = ProtocolSettings.Default.SeedList[ndx].Split(':')[0];
                var apiUrl = $"https://{addr}:4505/api/LyraNode/";
                _log.LogInformation("Platform {1} Use seed node of {0}", apiUrl, Environment.OSVersion.Platform);
                var client = await LyraRestClient.CreateAsync(NetworkID, Environment.OSVersion.Platform.ToString(), "LyraNode2", "1.0", apiUrl);
                var mode = await client.GetSyncState();
                if (mode.ResultCode == APIResultCodes.Success && mode.SyncState == ConsensusWorkingMode.Normal)
                {
                    return client;
                }
                await Task.Delay(10000);    // incase of hammer
            } while (true);
        }

        /// <summary>
        /// if this node is seed0 then sync with seeds others (random choice the one that is in normal state)
        /// if this node is seed1+ then sync with seed0
        /// otherwise sync with any seed node
        /// </summary>
        private void SyncBlocksFromSeeds(long ToUIndex)
        {
            return;

            InSyncing = true;
            Task.Run(async () => {

                while(true)
                {
                    _log.LogInformation("BlockChain Doing Sync...");
                    string syncWithUrl = null;
                    LyraRestClient client = null;
                    long syncToUIndex = ToUIndex;


                    if (syncWithUrl == null)
                    {
                        // no node to sync.
                        if (NodeService.Instance.PosWallet.AccountId == ProtocolSettings.Default.StandbyValidators[0])
                        {
                            // seed0. no seed to sync. this seed must have the NORMAL blockchain     
                            break;
                        }
                        else
                        {
                            _log.LogError("No seed node in normal state. Wait...");
                            await Task.Delay(300 * 1000);
                        }
                    }
                    else
                    {
                        // update latest billboard
                        var board = await client.GetBillBoardAsync();
                        LyraSystem.Singleton.Consensus.Tell(board);

                        // do sync with node
                        long startUIndex = 0;//await _store.GetNewestBlockUIndexAsync() + 1;

                        // seed0 not rollback. seed0 rollback manually if necessary.
                        if( startUIndex - 1 > syncToUIndex && NodeService.Instance.PosWallet.AccountId != ProtocolSettings.Default.StandbyValidators[0])
                        {
                            // detect blockchain rollback
                            _log.LogCritical($"BlockChain roll back detected!!! Roll back from {startUIndex} to {syncToUIndex}.");// Confirm? [Y/n]");
                            string answer = "y";// Console.ReadLine();
                            if (string.IsNullOrEmpty(answer) || answer.ToLower() == "y" || answer.ToLower() == "yes")
                            {
                                for(var i = syncToUIndex + 1; i <= startUIndex - 1; i++)
                                {
                                    //await RemoveBlockAsync(i);
                                }
                                startUIndex = syncToUIndex + 1;
                            }
                            else
                            {
                                // can't go
                                Environment.Exit(1);
                            }
                        }

                        _log.LogInformation($"BlockChain Doing sync from {startUIndex} to {syncToUIndex} from node {syncWithUrl}");

                        async Task<bool> DoCopyBlock(long fromUIndex, long toUIndex)
                        {
                            var authorizers = new AuthorizersFactory();

                            for (long j = fromUIndex; j <= toUIndex; j++)
                            {
                                /*var blockResult = await client.GetBlockByUIndex(j).ConfigureAwait(false);
                                if (blockResult.ResultCode == APIResultCodes.Success)
                                {
                                    var blockX = blockResult.GetBlock() as TransactionBlock;
                                    //if(blockX.UIndex <= 2)      // the two genesis service block
                                    //{
                                    //    await AddBlockAsync(blockX);
                                    //    continue;
                                    //}

                                    //var stopwatch = Stopwatch.StartNew();

                                    var authorizer = authorizers.Create(blockX.BlockType);
                                    var localAuthResult = await authorizer.AuthorizeAsync(blockX, false);

                                    //stopwatch.Stop();
                                    //_log.LogInformation($"Authorize takes {stopwatch.ElapsedMilliseconds} ms");

                                    if(localAuthResult.Item1 == APIResultCodes.Success)
                                    {
                                        await AddBlockAsync(blockX);
                                        fromUIndex = j + 1;
                                        _log.LogInformation($"BlockChain Synced Block Number: {j}");
                                    }
                                    else
                                    {
                                        _log.LogInformation($"BlockChain Block Number: {j} verify failed for {localAuthResult.Item1}");
                                        return false;
                                    }
                                }
                                else
                                {
                                    // error
                                    _log.LogInformation($"Error syncing block: {blockResult.ResultCode}");
                                    continue;
                                }*/
                            }
                            return true;
                        }

                        var copyOK = await DoCopyBlock(startUIndex, syncToUIndex).ConfigureAwait(false);
                        if(copyOK)
                        {
                            //// check missing block
                            //for(long k = 1; k <= startUIndex; k++)
                            //{
                            //    if(await BlockChain.Singleton.GetBlockByUIndex(k) == null)
                            //    {
                            //        _log.LogInformation($"syncing one missing block: {k}");
                            //        await DoCopyBlock(k, k).ConfigureAwait(false);
                            //    }
                            //}
                            break;
                        }
                        else
                        {
                            await Task.Delay(5000).ConfigureAwait(false);
                        }
                    }
                }

                InSyncing = false;
                LyraSystem.Singleton.Consensus.Tell(new ConsensusService.BlockChainSynced());
                _log.LogInformation("BlockChain Sync Completed.");
            });
        }
    }

    internal class BlockchainMailbox : PriorityMailbox
    {
        public BlockchainMailbox(Akka.Actor.Settings settings, Config config)
            : base(settings, config)
        {
        }

        internal protected override bool IsHighPriority(object message)
        {
            switch (message)
            {
                //case Header[] _:
                //case Block _:
                //case ConsensusPayload _:
                case Terminated _:
                    return true;
                default:
                    return false;
            }
        }
    }
}
