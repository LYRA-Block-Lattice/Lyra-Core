using Akka.Actor;
using Akka.Configuration;
using Clifton.Blockchain;
using Core.Authorizers;
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
using Neo.Network.P2P.Payloads;
using Newtonsoft.Json;
using Stateless;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Lyra.Core.Decentralize.ConsensusService;
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

    public class BlockChain : ReceiveActor
    {
        public class QueryBlockchainStatus { }
        public class NeedSync { public long ToUIndex { get; set; } }
        public class Startup { }
        public class PersistCompleted { }
        public class Import { }
        public class ImportCompleted { }
        public class FillMemoryPool { public IEnumerable<Transaction> Transactions; }
        public class FillCompleted { }
        public class BlockAdded
        {
            public string hash { get; set; }
        }
        private class ConsolidationState
        {
            public long LocalLastConsolidationHeight { get; set; }
        }
        public class AuthorizerCountChanged { public int count { get; set; } }

        private LyraRestClient _seed0Client;

        public uint Height;
        public string NetworkID { get; private set; }

        private readonly StateMachine<BlockChainState, BlockChainTrigger> _stateMachine;
        private readonly StateMachine<BlockChainState, BlockChainTrigger>.TriggerWithParameters<long> _engageTriggerStartupSync;
        private readonly StateMachine<BlockChainState, BlockChainTrigger>.TriggerWithParameters<string> _engageTriggerConsolidateFailed;
        public BlockChainState CurrentState => _stateMachine.State;

        AuthorizersFactory _authorizerFactory = new AuthorizersFactory();

        private LyraConfig _nodeConfig;
        private readonly IAccountCollectionAsync _store;
        private DagSystem _sys;
        private ILogger _log;
        private bool _creatingSvcBlock = false;

        // status inquiry
        private List<NodeStatus> _nodeStatus;
        public BlockChain(DagSystem sys, IAccountCollectionAsync store)
        {
            _sys = sys;

            _stateMachine = new StateMachine<BlockChainState, BlockChainTrigger>(BlockChainState.Initializing);
            _engageTriggerStartupSync = _stateMachine.SetTriggerParameters<long>(BlockChainTrigger.ConsensusNodesSynced);
            _engageTriggerConsolidateFailed = _stateMachine.SetTriggerParameters<string>(BlockChainTrigger.LocalNodeOutOfSync);
            CreateStateMachine();

            var nodeConfig = Neo.Settings.Default.LyraNode;
            _store = store; //new MongoAccountCollection();

            //_store = new LiteAccountCollection(Utilities.LyraDataDir);
            _log = new SimpleLogger("BlockChain").Logger;
            _nodeConfig = nodeConfig;
            NetworkID = nodeConfig.Lyra.NetworkId;

            ReceiveAsync<QueryBlockchainStatus>(async _ =>
            {
                var status = await GetNodeStatusAsync();
                Sender.Tell(status);
            });

            Receive<NeedSync>(cmd => SyncBlocksFromSeeds(cmd.ToUIndex));
            Receive<Startup>(_ => _stateMachine.Fire(BlockChainTrigger.LocalNodeStartup));
            Receive<NodeStatus>(nodeStatus => {
                // only accept status from seeds.
                _log.LogInformation($"NodeStatus from {nodeStatus.accountId.Shorten()}");
                if (_nodeStatus != null)
                {
                    if (!_nodeStatus.Any(a => a.accountId == nodeStatus.accountId))
                        _nodeStatus.Add(nodeStatus);
                }
            });
            Receive<Idle>(_ => { });
            ReceiveAsync<ConsolidateFailed>(async (x) =>
            {
                await ConsolidationBlockFailedAsync(x.consolidationBlockHash);
            });
            Receive<AuthorizerCountChanged>(x => AuthorizerCountChangedProc(x.count));
        }

        //protected override void OnReceive(object message)
        //{
        //    switch (message)
        //    {
        //        case NeedSync cmd:
        //            SyncBlocksFromSeeds(cmd.ToUIndex);
        //            break;
        //        case Startup _:
        //            _stateMachine.Fire(BlockChainTrigger.LocalNodeStartup);
        //            break;
        //        case NodeStatus nodeStatus:
        //            // only accept status from seeds.
        //            _log.LogInformation($"NodeStatus from {nodeStatus.accountId.Shorten()}");
        //            if (_nodeStatus != null)
        //            {
        //                if (!_nodeStatus.Any(a => a.accountId == nodeStatus.accountId))
        //                    _nodeStatus.Add(nodeStatus);
        //            }
        //            break;
        //        //    case Import import:
        //        //        OnImport(import.Blocks);
        //        //        break;
        //        //    case FillMemoryPool fill:
        //        //        OnFillMemoryPool(fill.Transactions);
        //        //        break;
        //        //    case Header[] headers:
        //        //        OnNewHeaders(headers);
        //        //        break;
        //        //    case Block block:
        //        //        Sender.Tell(OnNewBlock(block));
        //        //        break;
        //        //    case Transaction[] transactions:
        //        //        {
        //        //            // This message comes from a mempool's revalidation, already relayed
        //        //            foreach (var tx in transactions) OnNewTransaction(tx, false);
        //        //            break;
        //        //        }
        //        //    case Transaction transaction:
        //        //        Sender.Tell(OnNewTransaction(transaction, true));
        //        //        break;
        //        //    case ConsensusPayload payload:
        //        //        Sender.Tell(OnNewConsensus(payload));
        //        //        break;
        //        case Idle _:
        //            //        if (MemPool.ReVerifyTopUnverifiedTransactionsIfNeeded(MaxTxToReverifyPerIdle, currentSnapshot))
        //            //            Self.Tell(Idle.Instance, ActorRefs.NoSender);
        //            break;
        //    }
        //}

        public static Props Props(DagSystem system, IAccountCollectionAsync store)
        {
            return Akka.Actor.Props.Create(() => new BlockChain(system, store)).WithMailbox("blockchain-mailbox");
        }

        //private async Task ResetUIDAsync()
        //{
        //    long uid = -1;
        //    if (_sys.Consensus != null)
        //    {
        //        var uidObj = await _sys.Consensus.Ask(new ConsensusService.AskForMaxActiveUID()) as ConsensusService.ReplyForMaxActiveUID;
        //        if (uidObj != null && uidObj.uid.HasValue)
        //        {
        //            uid = uidObj.uid.Value;
        //        }
        //    }
        //}

        private void CreateStateMachine()
        {
            _stateMachine.Configure(BlockChainState.Initializing)
                .Permit(BlockChainTrigger.LocalNodeStartup, BlockChainState.Startup);

            _stateMachine.Configure(BlockChainState.Startup)
                .PermitReentry(BlockChainTrigger.QueryingConsensusNode)
                .OnEntry(() => Task.Run(async () =>
                {
                    while(true)
                    {
                        _log.LogInformation($"Blockchain Startup... ");
                        while (Neo.Network.P2P.LocalNode.Singleton.ConnectedCount < 2)
                        {
                            await Task.Delay(1000);
                        }

                        _sys.Consensus.Tell(new ConsensusService.Startup());

                        await Task.Delay(10000);

                        _nodeStatus = new List<NodeStatus>();
                        _sys.Consensus.Tell(new ConsensusService.NodeInquiry());

                        await Task.Delay(10000);

                        _log.LogInformation($"Querying billboard... ");
                        var board = await _sys.Consensus.Ask<BillBoard>(new AskForBillboard());
                        var q = from ns in _nodeStatus
                                where board.PrimaryAuthorizers != null && board.PrimaryAuthorizers.Contains(ns.accountId)
                                group ns by ns.totalBlockCount into heights
                                orderby heights.Count() descending
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
                            if (myStatus.totalBlockCount == 0 && majorHeight.Height == 0 && majorHeight.Count >= 3)
                            {
                                _stateMachine.Fire(_engageTriggerStartupSync, majorHeight.Height);
                                //_stateMachine.Fire(BlockChainTrigger.ConsensusBlockChainEmpty);
                                var IsSeed0 = await _sys.Consensus.Ask<bool>(new ConsensusService.AskIfSeed0());
                                if (await FindLatestBlockAsync() == null && IsSeed0)
                                {
                                    await Task.Delay(15000);
                                    Genesis();
                                }
                            }
                            else if (majorHeight.Height >= 2 && majorHeight.Count >= 2)
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
                            break;
                        }
                    }
                }))
                .Permit(BlockChainTrigger.ConsensusBlockChainEmpty, BlockChainState.Genesis)
                .Permit(BlockChainTrigger.ConsensusNodesSynced, BlockChainState.Engaging);

            _stateMachine.Configure(BlockChainState.Genesis)
                .OnEntry(() => Task.Run(async () =>
                {
                    var IsSeed0 = await _sys.Consensus.Ask<bool>(new ConsensusService.AskIfSeed0());
                    if (await FindLatestBlockAsync() == null && IsSeed0)
                    {
                        Genesis();
                    }
                }))
                .Permit(BlockChainTrigger.GenesisDone, BlockChainState.Startup);

            _stateMachine.Configure(BlockChainState.Engaging)
                .OnEntryFrom(_engageTriggerStartupSync, (uid) => Task.Run(async () =>
                {
                    var stateFn = $"{Utilities.GetLyraDataDir(Settings.Default.LyraNode.Lyra.NetworkId, LyraGlobal.OFFICIALDOMAIN)}{Utilities.PathSeperator}Consolidation.json";

                    var state = new ConsolidationState { LocalLastConsolidationHeight = 0 };
                    if (File.Exists(stateFn))
                        state = JsonConvert.DeserializeObject<ConsolidationState>(File.ReadAllText(stateFn));

                    var unConsSynced = 0;
                    while (true)
                    {
                        var client = new LyraClientForNode(_sys, await FindValidSeedForSyncAsync());

                        // compare state
                        var seedSyncState = await client.GetSyncState();
                        var mySyncState = await GetNodeStatusAsync();
                        if (seedSyncState.ResultCode == APIResultCodes.Success && seedSyncState.Status.Equals(mySyncState))
                        {
                            _log.LogInformation("Fully Synced with seeds.");
                            break;
                        }

                        var latestSeedCons = (await client.GetLastConsolidationBlockAsync()).GetBlock() as ConsolidationBlock;

                        if (state.LocalLastConsolidationHeight < latestSeedCons.Height)
                        {
                            var consBlocksResult = await client.GetConsolidationBlocks(state.LocalLastConsolidationHeight);
                            if (consBlocksResult.ResultCode == APIResultCodes.Success)
                            {
                                var consBlocks = consBlocksResult.GetBlocks().Cast<ConsolidationBlock>();
                                foreach (var consBlock in consBlocks)
                                {
                                    if(!await VerifyConsolidationBlock(consBlock, latestSeedCons.Height))
                                        await SyncManyBlocksAsync(client, consBlock);

                                    state.LocalLastConsolidationHeight = consBlock.Height;
                                }
                            }
                        }
                        else
                        {
                            // sync unconsolidated blocks
                            var unConsBlockResult = await client.GetUnConsolidatedBlocks();
                            if (unConsBlockResult.ResultCode == APIResultCodes.Success)
                            {
                                if (unConsSynced < unConsBlockResult.Entities.Count)
                                {
                                    await SyncManyBlocksAsync(client, unConsBlockResult.Entities);
                                    unConsSynced = unConsBlockResult.Entities.Count;
                                }
                                else
                                    break;
                            }                                
                        }

                        File.WriteAllText(stateFn, JsonConvert.SerializeObject(state));
                    }

                    _stateMachine.Fire(BlockChainTrigger.LocalNodeConsolidated);
                }))
                .Permit(BlockChainTrigger.LocalNodeConsolidated, BlockChainState.Almighty);

            _stateMachine.Configure(BlockChainState.Almighty)
                .OnEntry(() => Task.Run(async () =>
                {
                    _sys.Consensus.Tell(new ConsensusService.Startup());
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

                var svcGen = GetServiceGenesisBlock();
                await SendBlockToConsensusAsync(svcGen);

                await Task.Delay(1000);

                var tokenGen = GetLyraTokenGenesisBlock(svcGen);
                // DEBUG
                //_log.LogInformation("genesis block string:\n" + tokenGen.GetHashInput());
                await SendBlockToConsensusAsync(tokenGen);

                await Task.Delay(1000);

                var consGen = GetConsolidationGenesisBlock(svcGen, tokenGen);
                await SendBlockToConsensusAsync(consGen);

                await Task.Delay(1000);

                _log.LogInformation("svc genesis is done.");

                await Task.Delay(3000);

                // distribute staking coin to pre-defined authorizers
                var memStore = new AccountInMemoryStorage();
                var gensWallet = Wallet.Create(memStore, "tmp", "", NetworkID, _sys.PosWallet.PrivateKey);
                foreach (var accId in ProtocolSettings.Default.StartupValidators)
                {
                    var client = await FindValidSeedForSyncAsync();
                    await gensWallet.Sync(client);
                    var amount = LyraGlobal.MinimalAuthorizerBalance + 10000;
                    var sendResult = await gensWallet.Send(amount, accId);
                    if (sendResult.ResultCode == APIResultCodes.Success)
                    {
                        _log.LogInformation($"Genesis send {amount} successfull to accountId: {accId}");
                    }
                    else
                    {
                        _log.LogError($"Genesis send {amount} failed to accountId: {accId}");
                    }
                }

                await Task.Delay(3000);

                if (ProtocolSettings.Default.StartupValidators.Any())
                {
                    _sys.Consensus.Tell(new ConsensusService.Consolidate());

                    await Task.Delay(3000);
                }
            });
        }

        public async Task ConsolidationBlockFailedAsync(string hash)
        {
            _log.LogError($"ConsolidationBlockFailed for {hash.Shorten()}");

            var client = new LyraClientForNode(_sys, await FindValidSeedForSyncAsync());
            var consBlockReq = await client.GetBlockByHash(hash);
            if(consBlockReq.ResultCode == APIResultCodes.Success)
            {
                var consBlock = consBlockReq.GetBlock() as ConsolidationBlock;

                if (!await VerifyConsolidationBlock(consBlock))
                {
                    await SyncManyBlocksAsync(client, consBlock.blockHashes);
                }
            }
            else
            {
                if (_stateMachine.State == BlockChainState.Almighty)
                    _stateMachine.Fire(_engageTriggerConsolidateFailed, hash);
            }
        }

        public void AuthorizerCountChangedProc(int count)
        {
            var IsSeed0 = _sys.Consensus.Ask<bool>(new ConsensusService.AskIfSeed0()).Result;

            if (this._stateMachine.State == BlockChainState.Almighty && IsSeed0 && count >= ProtocolSettings.Default.StandbyValidators.Length)
            {
                //_log.LogInformation($"AuthorizerCountChanged: {count}");
                // look for changes. if necessary create a new svc block.
                Task.Run(async () =>
                {
                    if (!_creatingSvcBlock)
                    {
                        _creatingSvcBlock = true;

                        try
                        {
                            var prevSvcBlock = await GetLastServiceBlockAsync();
                            var board = await _sys.Consensus.Ask<BillBoard>(new AskForBillboard());
                            if (prevSvcBlock != null && DateTime.UtcNow - prevSvcBlock.TimeStamp > TimeSpan.FromMinutes(1))
                            {
                                var comp = new MultiSetComparer<string>();
                                if (!comp.Equals(prevSvcBlock.Authorizers.Select(a => a.AccountID), board.PrimaryAuthorizers))
                                {
                                    _log.LogInformation($"PrimaryAuthorizers Changed: {count} Creating new ServiceBlock.");

                                    var svcBlock = new ServiceBlock
                                    {
                                        NetworkId = prevSvcBlock.NetworkId,
                                        Height = prevSvcBlock.Height + 1,
                                        FeeTicker = LyraGlobal.OFFICIALTICKERCODE,
                                        ServiceHash = prevSvcBlock.Hash,
                                        TransferFee = 1,           //zero for genesis. back to normal when genesis done
                                        TokenGenerationFee = 10000,
                                        TradeFee = 0.1m
                                    };

                                    svcBlock.Authorizers = new List<PosNode>();
                                    foreach (var accId in board.PrimaryAuthorizers)
                                    {
                                        if(board.AllNodes.ContainsKey(accId))
                                            svcBlock.Authorizers.Add(board.AllNodes[accId]);

                                        if (svcBlock.Authorizers.Count() >= LyraGlobal.MAXIMUM_AUTHORIZERS)
                                            break;
                                    }

                                    // fees aggregation
                                    var allConsBlocks = await _sys.Storage.GetConsolidationBlocksAsync(prevSvcBlock.Hash);
                                    svcBlock.FeesGenerated = allConsBlocks.Sum(a => a.totalFees);

                                    if(svcBlock.Authorizers.Count() >= prevSvcBlock.Authorizers.Count())
                                    {
                                        svcBlock.InitializeBlock(prevSvcBlock, _sys.PosWallet.PrivateKey,
                                            _sys.PosWallet.AccountId);

                                        await SendBlockToConsensusAsync(svcBlock);
                                    }
                                    else
                                    {
                                        _log.LogError($"Authorizers count can't be less than {prevSvcBlock.Authorizers.Count()}");
                                    }
                                }
                            }                            
                        }
                        finally
                        {
                            await Task.Delay(10000);
                            _creatingSvcBlock = false;
                        }                        
                    }
                });
            }
        }

        public string GetUnConsolidatedHash(List<string> unCons)
        {
            if (unCons.Count() == 0)
                return "";

            var mt = new MerkleTree();
            foreach (var hash in unCons)
            {
                mt.AppendLeaf(MerkleHash.Create(hash));
            }

            return mt.BuildTree().ToString();
        }

        public async Task<NodeStatus> GetNodeStatusAsync()
        {
            var lastCons = await GetLastConsolidationBlockAsync();
            var unCons = (await _store.GetAllUnConsolidatedBlockHashesAsync()).ToList();
            var status = new NodeStatus
            {
                accountId = _sys.PosWallet.AccountId,
                version = LyraGlobal.NodeAppName,
                state = _stateMachine.State,
                totalBlockCount = lastCons == null ? 0 : lastCons.totalBlockCount + unCons.Count(),
                lastConsolidationHash = lastCons?.Hash,
                lastUnSolidationHash = GetUnConsolidatedHash(unCons),
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
                _sys.Consensus.Tell(new BlockAdded { hash = block.Hash });
            }

            if (block is ConsolidationBlock)
            {
                var consBlock = block as ConsolidationBlock;
                // we need to update the consolidation flag
                foreach (var hash in consBlock.blockHashes)
                {
                    if (!await _store.ConsolidateBlock(hash) && _stateMachine.State != BlockChainState.Engaging)
                        _log.LogCritical($"BlockChain Not consolidate block properly: {hash}");
                }

                // debug
                var blockCountInDb = await _store.GetBlockCountAsync();
                if (consBlock.totalBlockCount + 1 > blockCountInDb)
                    _log.LogCritical($"Consolidation block miscalculate!! total: {blockCountInDb} calculated: {consBlock.totalBlockCount}");
            }
            return result;
        }

        //public async Task<IEnumerable<Block>> GetAllUnConsolidatedBlocksAsync() => await StopWatcher.Track(_store.GetAllUnConsolidatedBlocksAsync(), StopWatcher.GetCurrentMethod());
        public async Task<IEnumerable<string>> GetAllUnConsolidatedBlockHashesAsync() => await StopWatcher.Track(_store.GetAllUnConsolidatedBlockHashesAsync(), StopWatcher.GetCurrentMethod());
        internal async Task<ConsolidationBlock> GetLastConsolidationBlockAsync() => await StopWatcher.Track(_store.GetLastConsolidationBlockAsync(), StopWatcher.GetCurrentMethod());//_store.GetSyncBlockAsync();
        public async Task<List<ConsolidationBlock>> GetConsolidationBlocksAsync(long startHeight) => await StopWatcher.Track(_store.GetConsolidationBlocksAsync(startHeight), StopWatcher.GetCurrentMethod());
        internal async Task<ServiceBlock> GetLastServiceBlockAsync() => await StopWatcher.Track(_store.GetLastServiceBlockAsync(), StopWatcher.GetCurrentMethod());//_store.GetLastServiceBlockAsync();

        // forward api. should have more control here.
        public async Task<bool> AddBlockAsync(Block block) => await StopWatcher.Track(AddBlockImplAsync(block), StopWatcher.GetCurrentMethod());
        public async Task RemoveBlockAsync(string hash) => await _store.RemoveBlockAsync(hash);
        //public async Task AddBlockAsync(ServiceBlock serviceBlock) => await StopWatcher.Track(_store.AddBlockAsync(serviceBlock), StopWatcher.GetCurrentMethod());//_store.AddBlockAsync(serviceBlock);

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
        //public async Task<Vote> GetVotesForAccountAsync(string accountId) => await _store.GetVotesForAccountAsync(accountId);
        //public async Task UpdateVotesForAccountAsync(Vote vote) => await _store.UpdateVotesForAccountAsync(vote);
        public List<Vote> FindVotes(IEnumerable<string> posAccountIds) => _store.FindVotes(posAccountIds);
        #endregion

        public LyraRestClient GetClientForSeed0()
        {
            if (_seed0Client == null)
            {
                var addr = ProtocolSettings.Default.SeedList[0].Split(':')[0];
                var apiUrl = $"http://{addr}:4505/api/Node/";
                _log.LogInformation("Platform {1} Use seed node of {0}", apiUrl, Environment.OSVersion.Platform);
                _seed0Client = LyraRestClient.Create(NetworkID, Environment.OSVersion.Platform.ToString(), "LyraNode2", "1.0", apiUrl);

            }
            return _seed0Client;
        }

        public ConsolidationBlock GetConsolidationGenesisBlock(ServiceBlock svcGen, LyraTokenGenesisBlock lyraGen)
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
            consBlock.ServiceHash = svcGen.Hash;
            consBlock.totalFees = lyraGen.Fee.ToBalanceLong();
            consBlock.InitializeBlock(null, _sys.PosWallet.PrivateKey,
                _sys.PosWallet.AccountId);

            return consBlock;
        }

        public LyraTokenGenesisBlock GetLyraTokenGenesisBlock(ServiceBlock svcGen)
        {
            var openTokenGenesisBlock = new LyraTokenGenesisBlock
            {
                Height = 1,
                AccountType = AccountTypes.Standard,
                Ticker = LyraGlobal.OFFICIALTICKERCODE,
                DomainName = LyraGlobal.OFFICIALDOMAIN,
                ContractType = ContractTypes.Cryptocurrency,
                Description = LyraGlobal.PRODUCTNAME + " Gas Token",
                Precision = LyraGlobal.OFFICIALTICKERPRECISION,
                IsFinalSupply = true,
                AccountID = _sys.PosWallet.AccountId,
                Balances = new Dictionary<string, long>(),
                PreviousHash = svcGen.Hash,
                ServiceHash = svcGen.Hash,
                Fee = svcGen.TokenGenerationFee,
                FeeType = AuthorizationFeeTypes.Regular,
                RenewalDate = DateTime.Now.AddYears(1000)
            };
            var transaction = new TransactionInfo() { TokenCode = openTokenGenesisBlock.Ticker, Amount = LyraGlobal.OFFICIALGENESISAMOUNT };
            openTokenGenesisBlock.Balances.Add(transaction.TokenCode, transaction.Amount.ToBalanceLong()); // This is current supply in atomic units (1,000,000.00)
            openTokenGenesisBlock.InitializeBlock(null, _sys.PosWallet.PrivateKey, AccountId: _sys.PosWallet.AccountId);

            return openTokenGenesisBlock;
        }

        public ServiceBlock GetServiceGenesisBlock()
        {
            var svcGenesis = new ServiceBlock
            {
                NetworkId = NetworkID,
                Height = 1,
                FeeTicker = LyraGlobal.OFFICIALTICKERCODE,
                TransferFee = 1,           //zero for genesis. back to normal when genesis done
                TokenGenerationFee = 100,
                TradeFee = 0.1m,
                FeesGenerated = 0
            };

            svcGenesis.Authorizers = new List<PosNode>();
            var board = _sys.Consensus.Ask<BillBoard>(new AskForBillboard()).Result;
            foreach (var pn in board.AllNodes.Values.Where(a => ProtocolSettings.Default.StandbyValidators.Contains(a.AccountID)))
            {
                svcGenesis.Authorizers.Add(pn);
            }
            svcGenesis.InitializeBlock(null, _sys.PosWallet.PrivateKey,
                _sys.PosWallet.AccountId);
            return svcGenesis;
        }

        private async Task SendBlockToConsensusAsync(Block block)
        {
            AuthorizingMsg msg = new AuthorizingMsg
            {
                From = _sys.PosWallet.AccountId,
                Block = block,
                MsgType = ChatMessageType.AuthorizerPrePrepare
            };

            var state = new AuthState(true);
            state.SetView(await GetLastServiceBlockAsync());
            state.InputMsg = msg;

            _sys.Consensus.Tell(state);

            await state.Done.AsTask();
            state.Done.Close();
            state.Done = null;
        }

        private async Task<bool> VerifyConsolidationBlock(ConsolidationBlock consBlock, long latestHeight = -1)
        {
            _log.LogInformation($"VerifyConsolidationBlock: {consBlock.Height}/{latestHeight}");

            var myConsBlock = await FindBlockByHashAsync(consBlock.Hash) as ConsolidationBlock;
            if (myConsBlock == null)
                return false;

            var mt = new MerkleTree();
            foreach (var hash in myConsBlock.blockHashes)
            {
                var myBlock = await FindBlockByHashAsync(hash);
                if (myBlock == null)
                    return false;

                mt.AppendLeaf(MerkleHash.Create(hash));
            }

            var merkelTreeHash = mt.BuildTree().ToString();

            return consBlock.MerkelTreeHash == merkelTreeHash;
        }

        private async Task SyncManyBlocksAsync(LyraClientForNode client, ConsolidationBlock consBlock)
        {
            _log.LogInformation($"Syncing Consolidations {consBlock.Height} / {consBlock.Hash.Shorten()} ");

            var blocksResult = await client.GetBlocksByConsolidation(consBlock.Hash);
            if(blocksResult.ResultCode == APIResultCodes.Success)
            {
                foreach(var block in blocksResult.GetBlocks())
                {
                    var localBlock = await FindBlockByHashAsync(block.Hash);
                    if (localBlock != null)
                        await RemoveBlockAsync(block.Hash);

                    await AddBlockAsync(block);
                }
            }
        }

        private async Task SyncManyBlocksAsync(LyraClientForNode client, List<string> hashes)
        {
            _log.LogInformation($"Syncing {hashes.Count()} blocks...");

            foreach (var hash in hashes)
            {
                var blockResult = await client.GetBlockByHash(hash);
                if (blockResult.ResultCode == APIResultCodes.Success)
                {
                    var localBlock = await FindBlockByHashAsync(hash);
                    if (localBlock != null)
                        await RemoveBlockAsync(hash);

                    await AddBlockAsync(blockResult.GetBlock());
                }
            }
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
                } while (_sys.PosWallet.AccountId == ProtocolSettings.Default.StandbyValidators[ndx]);

                var addr = ProtocolSettings.Default.SeedList[ndx].Split(':')[0];
                var apiUrl = $"http://{addr}:4505/api/Node/";
                _log.LogInformation("Platform {1} Use seed node of {0}", apiUrl, Environment.OSVersion.Platform);
                var client = LyraRestClient.Create(NetworkID, Environment.OSVersion.Platform.ToString(), "LyraNode2", "1.0", apiUrl);
                var mode = await client.GetSyncState();
                if (mode.ResultCode == APIResultCodes.Success)
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

            Task.Run(async () =>
            {

                while (true)
                {
                    _log.LogInformation("BlockChain Doing Sync...");
                    string syncWithUrl = null;
                    LyraRestClient client = null;
                    long syncToUIndex = ToUIndex;


                    if (syncWithUrl == null)
                    {
                        // no node to sync.
                        if (_sys.PosWallet.AccountId == ProtocolSettings.Default.StandbyValidators[0])
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
                        _sys.Consensus.Tell(board);

                        // do sync with node
                        long startUIndex = 0;//await _store.GetNewestBlockUIndexAsync() + 1;

                        // seed0 not rollback. seed0 rollback manually if necessary.
                        if (startUIndex - 1 > syncToUIndex && _sys.PosWallet.AccountId != ProtocolSettings.Default.StandbyValidators[0])
                        {
                            // detect blockchain rollback
                            _log.LogCritical($"BlockChain roll back detected!!! Roll back from {startUIndex} to {syncToUIndex}.");// Confirm? [Y/n]");
                            string answer = "y";// Console.ReadLine();
                            if (string.IsNullOrEmpty(answer) || answer.ToLower() == "y" || answer.ToLower() == "yes")
                            {
                                for (var i = syncToUIndex + 1; i <= startUIndex - 1; i++)
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
                        if (copyOK)
                        {
                            //// check missing block
                            //for(long k = 1; k <= startUIndex; k++)
                            //{
                            //    if(await DagSystem.Singleton.Storage.GetBlockByUIndex(k) == null)
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

                _sys.Consensus.Tell(new ConsensusService.BlockChainSynced());
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
