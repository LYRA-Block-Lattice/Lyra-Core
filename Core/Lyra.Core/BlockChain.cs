using Akka.Actor;
using Akka.Configuration;
using Akka.Streams.Util;
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
        ConsensusNodesInitSynced,

        // engage
        LocalNodeFullySynced,

        // almighty
        LocalNodeOutOfSync,

        // genesis
        GenesisDone
    }

    public class BlockChain : ReceiveActor
    {
        public class QueryState { }
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
            public Block NewBlock { get; set; }
        }

        public class NewLeaderCreateView { }

        private LyraRestClient _seed0Client;

        public uint Height;
        public string NetworkID { get; private set; }

        private readonly StateMachine<BlockChainState, BlockChainTrigger> _stateMachine;
        private readonly StateMachine<BlockChainState, BlockChainTrigger>.TriggerWithParameters<long> _engageTriggerStart;
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
            _engageTriggerStart = _stateMachine.SetTriggerParameters<long>(BlockChainTrigger.ConsensusNodesInitSynced);
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
            Receive<QueryState>(x => Sender.Tell(CurrentState));

            Receive<NeedSync>(cmd => SyncBlocksFromSeeds(cmd.ToUIndex));
            Receive<Startup>(_ => _stateMachine.Fire(BlockChainTrigger.LocalNodeStartup));
            Receive<NodeStatus>(nodeStatus =>
            {
                // only accept status from seeds.
                //_log.LogInformation($"NodeStatus from {nodeStatus.accountId.Shorten()}");
                if (_nodeStatus != null)
                {
                    if (!_nodeStatus.Any(a => a.accountId == nodeStatus.accountId))
                        _nodeStatus.Add(nodeStatus);
                }
            });
            Receive<Idle>(_ => { });
            Receive<ConsolidateFailed>(x =>
            {
                ConsolidationBlockFailed(x.consolidationBlockHash);
            });
            Receive<NewLeaderCreateView>(x => CreateNewViewAsNewLeader());
        }

        public static Props Props(DagSystem system, IAccountCollectionAsync store)
        {
            return Akka.Actor.Props.Create(() => new BlockChain(system, store)).WithMailbox("blockchain-mailbox");
        }

        private void CreateStateMachine()
        {
            _stateMachine.Configure(BlockChainState.Initializing)
                .OnEntry(() =>
                {
                    _sys.Consensus.Tell(new ConsensusService.BlockChainStatuChanged { CurrentState = _stateMachine.State });

                    _log.LogInformation($"Blockchain Startup... ");

                })
                .Permit(BlockChainTrigger.LocalNodeStartup, BlockChainState.Startup);

            _stateMachine.Configure(BlockChainState.Startup)
                .PermitReentry(BlockChainTrigger.QueryingConsensusNode)
                .OnEntry(() => Task.Run(async () =>
                {
                    _sys.Consensus.Tell(new ConsensusService.BlockChainStatuChanged { CurrentState = _stateMachine.State });
                    while (true)
                    {
                        try
                        {
                            _log.LogInformation($"Querying Lyra Network Status... ");

                            while (Neo.Network.P2P.LocalNode.Singleton.ConnectedCount < 2)
                            {
                                await Task.Delay(1000);
                            }

                            _sys.Consensus.Tell(new ConsensusService.Startup());

                            await Task.Delay(10000);

                            _nodeStatus = new List<NodeStatus>();
                            _sys.Consensus.Tell(new ConsensusService.NodeInquiry());

                            await Task.Delay(10000);

                            _log.LogInformation($"Querying Billboard... ");
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
                                    //_stateMachine.Fire(_engageTriggerStartupSync, majorHeight.Height);
                                    _stateMachine.Fire(BlockChainTrigger.ConsensusBlockChainEmpty);
                                }
                                else if (majorHeight.Height >= 2 && majorHeight.Count >= 2)
                                {
                                    // verify local database
                                    while (!await SyncDatabase())
                                    {
                                        //fatal error. should not run program
                                        _log.LogCritical($"Unable to sync blockchain database. Will retry in 1 minute.");
                                        await Task.Delay(60000);
                                    }
                                    _stateMachine.Fire(_engageTriggerStart, majorHeight.Height);
                                }
                                else
                                {
                                    continue;
                                }
                                break;
                            }
                            else
                            {
                                _log.LogInformation($"Unable to get Lyra network status.");
                            }
                        }
                        catch (Exception ex)
                        {
                            _log.LogCritical("In BlockChainState.Startup: " + ex.ToString());
                        }
                    }
                }))
                .Permit(BlockChainTrigger.ConsensusBlockChainEmpty, BlockChainState.Genesis)
                .Permit(BlockChainTrigger.ConsensusNodesInitSynced, BlockChainState.Engaging);

            _stateMachine.Configure(BlockChainState.Genesis)
                .OnEntry(() => Task.Run(async () =>
                {
                    _sys.Consensus.Tell(new ConsensusService.BlockChainStatuChanged { CurrentState = _stateMachine.State });

                    var IsSeed0 = await _sys.Consensus.Ask<ConsensusService.AskIfSeed0>(new ConsensusService.AskIfSeed0());
                    if (await FindLatestBlockAsync() == null && IsSeed0.IsSeed0)
                    {
                        await GenesisAsync();
                    }
                    else
                    {
                        // wait for genesis to finished.
                        await Task.Delay(360000);
                    }

                    _stateMachine.Fire(BlockChainTrigger.GenesisDone);
                }))
                .Permit(BlockChainTrigger.GenesisDone, BlockChainState.Startup);

            _stateMachine.Configure(BlockChainState.Engaging)
                .OnEntryFrom(_engageTriggerStart, (blockCount) => Task.Run(async () =>
                {
                    _sys.Consensus.Tell(new ConsensusService.BlockChainStatuChanged { CurrentState = _stateMachine.State });

                    try
                    {
                        if (blockCount > 2)
                        {
                            // sync cons and uncons
                            await EngagingSyncAsync();
                        }
                    }
                    catch (Exception e)
                    {
                        _log.LogError($"In Engaging: {e}");
                    }

                    _stateMachine.Fire(BlockChainTrigger.LocalNodeFullySynced);
                }))
                .Permit(BlockChainTrigger.LocalNodeFullySynced, BlockChainState.Almighty);

            _stateMachine.Configure(BlockChainState.Almighty)
                .OnEntry(() => Task.Run(() =>
                {
                    _sys.Consensus.Tell(new ConsensusService.BlockChainStatuChanged { CurrentState = _stateMachine.State });
                }))
                .Permit(BlockChainTrigger.LocalNodeOutOfSync, BlockChainState.Startup);

            _stateMachine.OnTransitioned(t => _log.LogWarning($"OnTransitioned: {t.Source} -> {t.Destination} via {t.Trigger}({string.Join(", ", t.Parameters)})"));
        }

        private async Task<bool> SyncDatabase()
        {
            var client = new LyraClientForNode(_sys);

            var svcGen = await _sys.Storage.GetServiceGenesisBlock();
            var localDbState = await GetNodeStatusAsync();
            if (localDbState.totalBlockCount == 0)
                LocalDbSyncState.Remove();
            else
            {
                var oldState = LocalDbSyncState.Load();
                
                if (oldState.svcGenHash != svcGen.Hash)
                    LocalDbSyncState.Remove();
            }

            var localState = LocalDbSyncState.Load();

            bool IsSuccess = true;
            while (true)
            {
                var seedCons = (await client.GetLastConsolidationBlockAsync()).GetBlock() as ConsolidationBlock;

                _log.LogInformation($"SyncDatabase: Latest consolidation block height is {seedCons.Height}. My local height is {localState.lastVerifiedConsHeight}.");

                if (localState.lastVerifiedConsHeight == seedCons.Height)
                    break;

                var latestHeight = seedCons.Height;
                while (localState.lastVerifiedConsHeight < seedCons.Height)
                {
                    if (await SyncAndVerifyConsolidationBlock(client, seedCons))
                    {
                        _log.LogInformation($"Consolidation block {seedCons.Height} is OK.");
                    }
                    else
                    {
                        _log.LogError($"Consolidation block {seedCons.Height} is failure.");
                        IsSuccess = false;
                        break;
                    }

                    if (seedCons.Height == 1)
                        break;

                    seedCons = (await client.GetBlockByHash(seedCons.blockHashes.First())).GetBlock() as ConsolidationBlock;
                }
                if (IsSuccess)
                {
                    localState.lastVerifiedConsHeight = latestHeight;
                    if (string.IsNullOrWhiteSpace(localState.svcGenHash))
                        localState.svcGenHash = svcGen.Hash;
                }                    
                else
                    break;
            }

            LocalDbSyncState.Save(localState);

            return IsSuccess;
        }

        private async Task EngagingSyncAsync()
        {
            // most db is synced. 
            // so make sure Last Float Hash equal to seed.

            var client = new LyraClientForNode(_sys);
            while (true)
            {
                _log.LogInformation("Engaging Sync...");
                var lastConsOfSeed = await client.GetLastConsolidationBlockAsync();
                var myLastCons = await GetLastConsolidationBlockAsync();
                if (myLastCons == null || myLastCons.Height < lastConsOfSeed.GetBlock().Height)
                {
                    if (!await SyncDatabase())
                    {
                        _log.LogError($"Error sync database. wait 5 minutes and retry...");
                        await Task.Delay(5 * 60 * 1000);
                    }
                    continue;
                }

                // sync unconsolidated blocks
                var endTime = DateTime.UtcNow.AddSeconds(2);
                var unConsHashResult = await client.GetBlockHashesByTimeRange(myLastCons.TimeStamp, endTime);
                if (unConsHashResult.ResultCode == APIResultCodes.Success)
                {
                    foreach (var hash in unConsHashResult.Entities)  // the first one is previous consolidation block
                    {
                        if (hash == myLastCons.Hash)
                            continue;       // already synced by previous steps
                        var blockResult = await client.GetBlockByHash(hash);
                        await AddBlockAsync(blockResult.GetBlock());
                    }
                }

                var remoteState = await client.GetSyncState();
                var localState = await GetNodeStatusAsync();
                if (remoteState.Status.lastConsolidationHash == localState.lastConsolidationHash &&
                    remoteState.Status.lastUnSolidationHash == localState.lastUnSolidationHash)
                    break;

                _log.LogInformation("Engaging Sync partial success. continue...");
            }
        }

        private async Task GenesisAsync()
        {
            // genesis
            _log.LogInformation("all seed nodes are ready. do genesis.");

            var svcGen = await CreateServiceGenesisBlockAsync();
            await SendBlockToConsensusAsync(svcGen, ProtocolSettings.Default.StandbyValidators.ToList());

            await Task.Delay(1000);

            var tokenGen = CreateLyraTokenGenesisBlock(svcGen);
            // DEBUG
            //_log.LogInformation("genesis block string:\n" + tokenGen.GetHashInput());
            await SendBlockToConsensusAsync(tokenGen);

            await Task.Delay(15000);        // because cons block has a time shift.

            var consGen = CreateConsolidationGenesisBlock(svcGen, tokenGen);
            await SendBlockToConsensusAsync(consGen);

            await Task.Delay(1000);

            _log.LogInformation("svc genesis is done.");

            await Task.Delay(3000);

            // distribute staking coin to pre-defined authorizers
            var memStore = new AccountInMemoryStorage();
            Wallet.Create(memStore, "tmp", "", NetworkID, _sys.PosWallet.PrivateKey);
            var gensWallet = Wallet.Open(memStore, "tmp", "");
            foreach (var accId in ProtocolSettings.Default.StandbyValidators.Skip(1).Concat(ProtocolSettings.Default.StartupValidators))
            {
                var client = await LyraClientForNode.FindValidSeedForSyncAsync(_sys);
                await gensWallet.Sync(client);
                var amount = LyraGlobal.MinimalAuthorizerBalance + 100000;
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

            await Task.Delay(15000);

            _sys.Consensus.Tell(new ConsensusService.Consolidate());

            await Task.Delay(3000);
        }

        public void ConsolidationBlockFailed(string hash)
        {
            _log.LogError($"ConsolidationBlockFailed for {hash.Shorten()}");

            if (_stateMachine.State == BlockChainState.Almighty)
                _stateMachine.Fire(_engageTriggerConsolidateFailed, hash);
            else
                _log.LogCritical("Current state not Almighty. something error.");

            //var client = new LyraClientForNode(_sys, await FindValidSeedForSyncAsync());
            //var consBlockReq = await client.GetBlockByHash(hash);
            //if(consBlockReq.ResultCode == APIResultCodes.Success)
            //{
            //    var consBlock = consBlockReq.GetBlock() as ConsolidationBlock;

            //    if (!await VerifyConsolidationBlock(consBlock))
            //    {
            //        await SyncManyBlocksAsync(client, consBlock.blockHashes);
            //    }
            //}
            //else
            //{

            //}
        }

        public void CreateNewViewAsNewLeader()
        {
            // look for changes. if necessary create a new svc block.
            _ = Task.Run(async () =>
              {
                  if (!_creatingSvcBlock)
                  {
                      _creatingSvcBlock = true;

                      _log.LogInformation($"Me was elected new leader. Creating New View...");

                      try
                      {
                          var board = await _sys.Consensus.Ask<BillBoard>(new AskForBillboard());

                          var allVoters = _sys.Storage.FindVotes(board.ActiveNodes.Select(a => a.AccountID));
                          var prevSvcBlock = await GetLastServiceBlockAsync();

                          var svcBlock = new ServiceBlock
                          {
                              NetworkId = prevSvcBlock.NetworkId,
                              Height = prevSvcBlock.Height + 1,
                              FeeTicker = LyraGlobal.OFFICIALTICKERCODE,
                              ServiceHash = prevSvcBlock.Hash,
                              Leader = _sys.PosWallet.AccountId,
                              TransferFee = 1,           //zero for genesis. back to normal when genesis done
                              TokenGenerationFee = 10000,
                              TradeFee = 0.1m
                          };

                          _log.LogInformation($"Adding {allVoters.Count()} voters...");

                          svcBlock.Authorizers = new Dictionary<string, string>();
                          foreach (var voter in allVoters)
                          {
                              if (board.ActiveNodes.Any(a => a.AccountID == voter.AccountId))
                              {
                                  var node = board.ActiveNodes.First(a => a.AccountID == voter.AccountId);
                                  svcBlock.Authorizers.Add(node.AccountID, node.AuthorizerSignature);
                              }
                              else
                              {
                                  // impossible. viewchangehandler has already filterd all none active messages.
                                  // or just bypass it?
                              }

                              if (svcBlock.Authorizers.Count() >= LyraGlobal.MAXIMUM_AUTHORIZERS)
                                  break;
                          }

                          // fees aggregation
                          _log.LogInformation($"Fee aggregating...");
                          var allConsBlocks = await _sys.Storage.GetConsolidationBlocksAsync(prevSvcBlock.Hash);
                          svcBlock.FeesGenerated = allConsBlocks.Sum(a => a.totalFees);

                          svcBlock.InitializeBlock(prevSvcBlock, _sys.PosWallet.PrivateKey, _sys.PosWallet.AccountId);

                          await SendBlockToConsensusAsync(svcBlock, board.AllVoters);

                          _log.LogInformation($"New View was created. send to network...");
                      }
                      catch (Exception e)
                      {
                          _log.LogCritical($"CreateNewViewAsNewLeader: {e}");
                      }
                      finally
                      {
                          await Task.Delay(10000);
                          _creatingSvcBlock = false;
                      }
                  }
              });
        }

        public string GetUnConsolidatedHash(List<string> unCons)
        {
            if (unCons == null)
                return "";

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
            var unCons = lastCons == null ? null : (await _store.GetBlockHashesByTimeRange(lastCons.TimeStamp, DateTime.UtcNow)).ToList();
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
                _sys.Consensus.Tell(new BlockAdded { NewBlock = block });
            }
            return result;
        }

        //public async Task<IEnumerable<Block>> GetAllUnConsolidatedBlocksAsync() => await StopWatcher.Track(_store.GetAllUnConsolidatedBlocksAsync(), StopWatcher.GetCurrentMethod());
        //public async Task<IEnumerable<string>> GetAllUnConsolidatedBlockHashesAsync() => await StopWatcher.Track(_store.GetAllUnConsolidatedBlockHashesAsync(), StopWatcher.GetCurrentMethod());
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

        public ConsolidationBlock CreateConsolidationGenesisBlock(ServiceBlock svcGen, LyraTokenGenesisBlock lyraGen)
        {
            var consBlock = new ConsolidationBlock
            {
                createdBy = ProtocolSettings.Default.StandbyValidators[0],
                blockHashes = new List<string>()
                {
                    svcGen.Hash, lyraGen.Hash
                },
                totalBlockCount = 2     // not including self
            };
            consBlock.TimeStamp = DateTime.UtcNow.AddSeconds(-10);

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

        public LyraTokenGenesisBlock CreateLyraTokenGenesisBlock(ServiceBlock svcGen)
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

        public async Task<ServiceBlock> CreateServiceGenesisBlockAsync()
        {
            var svcGenesis = new ServiceBlock
            {
                NetworkId = NetworkID,
                Leader = ProtocolSettings.Default.StandbyValidators[0],
                Height = 1,
                FeeTicker = LyraGlobal.OFFICIALTICKERCODE,
                TransferFee = 1,           //zero for genesis. back to normal when genesis done
                TokenGenerationFee = 100,
                TradeFee = 0.1m,
                FeesGenerated = 0
            };

            // wait for all nodes ready
            while(true)
            {
                svcGenesis.Authorizers = new Dictionary<string, string>();
                var board = _sys.Consensus.Ask<BillBoard>(new AskForBillboard()).Result;
                foreach (var pn in ProtocolSettings.Default.StandbyValidators)
                {
                    svcGenesis.Authorizers.Add(pn, board.ActiveNodes.First(a => a.AccountID == pn).AuthorizerSignature);
                }
                if (svcGenesis.Authorizers.Count >= LyraGlobal.MINIMUM_AUTHORIZERS)
                    break;
                else
                {
                    _log.LogInformation($"Waiting for seed nodes to up. Now we have {svcGenesis.Authorizers.Count} of {LyraGlobal.MINIMUM_AUTHORIZERS}");
                    await Task.Delay(1000);
                }
            }

            svcGenesis.TimeStamp = DateTime.UtcNow;

            svcGenesis.InitializeBlock(null, _sys.PosWallet.PrivateKey,
                _sys.PosWallet.AccountId);
            return svcGenesis;
        }

        private async Task SendBlockToConsensusAsync(Block block, List<string> voters = null)        // default is genesus, 4 default
        {
            AuthorizingMsg msg = new AuthorizingMsg
            {
                From = _sys.PosWallet.AccountId,
                Block = block,
                BlockHash = block.Hash,
                MsgType = ChatMessageType.AuthorizerPrePrepare
            };

            AuthState state;
            if (block is ServiceBlock sb)
            {
                _log.LogInformation($"AllVoters: {voters.Count}");
                state = new ServiceBlockAuthState(voters, true);
            }
            else
            {
                state = new AuthState(true);
            }
            state.SetView(await GetLastServiceBlockAsync());
            state.InputMsg = msg;

            _sys.Consensus.Tell(state);

            await state.Done.AsTask();
            state.Done.Close();
            state.Done = null;
        }

        private async Task<bool> SyncAndVerifyConsolidationBlock(LyraClientForNode client, ConsolidationBlock consBlock)
        {
            _log.LogInformation($"Sync and verify consolidation block height {consBlock.Height}");

            var myConsBlock = await FindBlockByHashAsync(consBlock.Hash) as ConsolidationBlock;
            if (myConsBlock == null)
            {
                await AddBlockAsync(consBlock);
                myConsBlock = consBlock;
            }
            else
            {
                if (!myConsBlock.VerifyHash())
                {
                    await RemoveBlockAsync(myConsBlock.Hash);
                    await AddBlockAsync(consBlock);
                    myConsBlock = consBlock;
                }
            }

            var mt = new MerkleTree();
            foreach (var hash in myConsBlock.blockHashes)
            {
                var myBlock = await FindBlockByHashAsync(hash);
                if (myBlock == null)
                {
                    if(!await SyncOneBlockAsync(client, hash, false))
                        return false;
                    else
                        myBlock = await FindBlockByHashAsync(hash);
                }

                if (!myBlock.VerifyHash() && !await SyncOneBlockAsync(client, hash, true))
                    return false;

                mt.AppendLeaf(MerkleHash.Create(hash));
            }

            var merkelTreeHash = mt.BuildTree().ToString();

            if (consBlock.MerkelTreeHash != merkelTreeHash)
                return false;

            // make sure no extra blocks here
            if (consBlock.Height > 1)
            {
                var prevConsHash = consBlock.blockHashes.First();
                var prevConsResult = await client.GetBlockByHash(prevConsHash);
                if (prevConsResult.ResultCode != APIResultCodes.Success)
                    return false;

                var prevConsBlock = prevConsResult.GetBlock() as ConsolidationBlock;
                if (prevConsBlock == null)
                    return false;

                var blocksInTimeRange = await _sys.Storage.GetBlockHashesByTimeRange(prevConsBlock.TimeStamp, consBlock.TimeStamp);
                var q = blocksInTimeRange.Where(a => !consBlock.blockHashes.Contains(a));
                foreach (var extraBlock in q)
                {
                    await RemoveBlockAsync(extraBlock);
                }
            }

            return true;
        }

        private async Task SyncManyBlocksAsync(LyraClientForNode client, ConsolidationBlock consBlock)
        {
            _log.LogInformation($"Syncing Consolidations {consBlock.Height} / {consBlock.Hash.Shorten()} ");

            var blocksResult = await client.GetBlocksByConsolidation(consBlock.Hash);
            if (blocksResult.ResultCode == APIResultCodes.Success)
            {
                foreach (var block in blocksResult.GetBlocks())
                {
                    var localBlock = await FindBlockByHashAsync(block.Hash);
                    if (localBlock != null)
                        await RemoveBlockAsync(block.Hash);

                    await AddBlockAsync(block);
                }

                // save cons block itself
                var localCons = await FindBlockByHashAsync(consBlock.Hash);
                if (localCons != null)
                    await RemoveBlockAsync(consBlock.Hash);

                await AddBlockAsync(consBlock);
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

        private async Task<bool> SyncOneBlockAsync(LyraClientForNode client, string hash, bool removeLocal)
        {
            if (removeLocal)
                await RemoveBlockAsync(hash);

            var remoteBlock = await client.GetBlockByHash(hash);
            if (remoteBlock.ResultCode == APIResultCodes.Success)
                return await AddBlockAsync(remoteBlock.GetBlock());
            else
                return false;
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

                //_sys.Consensus.Tell(new ConsensusService.BlockChainSynced());
                _log.LogInformation("BlockChain Sync Completed.");
            });
        }

        private class LocalDbSyncState
        {
            public string svcGenHash { get; set; }      // make sure not mix with other dbs
            public long lastVerifiedConsHeight { get; set; }

            public static LocalDbSyncState Load()
            {
                try
                {
                    var fn = $"{Utilities.GetLyraDataDir(Neo.Settings.Default.LyraNode.Lyra.NetworkId, LyraGlobal.OFFICIALDOMAIN)}{Utilities.PathSeperator}syncState.json";
                    if (File.Exists(fn))
                        return JsonConvert.DeserializeObject<LocalDbSyncState>(File.ReadAllText(fn));
                }
                catch (Exception)
                {

                }
                return new LocalDbSyncState();
            }

            public static void Save(LocalDbSyncState state)
            {
                var fn = $"{Utilities.GetLyraDataDir(Neo.Settings.Default.LyraNode.Lyra.NetworkId, LyraGlobal.OFFICIALDOMAIN)}{Utilities.PathSeperator}syncState.json";
                var str = JsonConvert.SerializeObject(state);
                File.WriteAllText(fn, str);
            }

            internal static void Remove()
            {
                var fn = $"{Utilities.GetLyraDataDir(Neo.Settings.Default.LyraNode.Lyra.NetworkId, LyraGlobal.OFFICIALDOMAIN)}{Utilities.PathSeperator}syncState.json";
                if (File.Exists(fn))
                    File.Delete(fn);
            }
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
