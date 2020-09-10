using Clifton.Blockchain;
using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Shared;
using Microsoft.Extensions.Logging;
using Neo;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize
{
    public partial class ConsensusService
    {
        public async Task<NodeStatus> GetNodeStatusAsync()
        {
            var lastCons = await _sys.Storage.GetLastConsolidationBlockAsync();
            var unCons = lastCons == null ? null : (await _sys.Storage.GetBlockHashesByTimeRange(lastCons.TimeStamp, DateTime.UtcNow)).ToList();
            var status = new NodeStatus
            {
                accountId = _sys.PosWallet.AccountId,
                version = LyraGlobal.NodeAppName,
                state = _stateMachine.State,
                totalBlockCount = lastCons == null ? 0 : lastCons.totalBlockCount + unCons.Count(),
                lastConsolidationHash = lastCons?.Hash,
                lastUnSolidationHash = GetUnConsolidatedHash(unCons),
                activePeers = Board.ActiveNodes.Count,
                connectedPeers = Neo.Network.P2P.LocalNode.Singleton.ConnectedCount
            };
            return status;
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

        private async Task<LyraClientForNode> GetOptimizedSyncClientAsync()
        {
            while (true)
            {
                var q = from ns in _nodeStatus
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

                    _log.LogInformation($"GetOptimizedSyncClientAsync major height {majorHeight.Height} count {majorHeight.Count} ");

                    if (majorHeight.Height >= 2 && majorHeight.Count >= 3)
                    {
                        var validNodeList = _nodeStatus
                            .Where(a => a.totalBlockCount == majorHeight.Height)
                            .Select(a => a.accountId);

                        var validNodeIps = Board.NodeAddresses.Where(a => validNodeList.Contains(a.Key))
                            .ToList();
                        return new LyraClientForNode(_sys, validNodeIps);
                    }
                }

                _log.LogInformation($"GetOptimizedSyncClientAsync Querying Lyra Network Status... ");

                _nodeStatus.Clear();
                var inq = new ChatMsg("", ChatMessageType.NodeStatusInquiry);
                inq.From = _sys.PosWallet.AccountId;
                Send2P2pNetwork(inq);

                await Task.Delay(10000);
            }
        }

        private async Task<bool> SyncDatabase(long height = 0)
        {
            var seedClient = new LyraClientForNode(_sys);

            LyraClientForNode client = await GetOptimizedSyncClientAsync();
 
            var seedSvcGen = await seedClient.GetServiceGenesisBlock();
            var localDbState = await GetNodeStatusAsync();
            if (localDbState.totalBlockCount == 0)
            {
                LocalDbSyncState.Remove();
            }                
            else
            {
                var oldState = LocalDbSyncState.Load();

                if (oldState.svcGenHash != seedSvcGen.GetBlock().Hash)
                    LocalDbSyncState.Remove();
            }

            var localState = LocalDbSyncState.Load();
            if(localState.svcGenHash == null)
            {
                localState.svcGenHash = seedSvcGen.GetBlock().Hash;
            }

            var lastCons = (await client.GetLastConsolidationBlockAsync()).GetBlock() as ConsolidationBlock;
            bool IsSuccess = true;
            while (true)
            {
                var seedCons = (await client.GetConsolidationBlocks(localState.lastVerifiedConsHeight + 1)).GetBlocks();

                if(seedCons.Any())
                {
                    foreach (var block in seedCons)
                    {
                        var consTarget = block as ConsolidationBlock;
                        _log.LogInformation($"SyncDatabase: Sync consolidation block {consTarget.Height} of total {lastCons.Height}.");
                        if (await SyncAndVerifyConsolidationBlock(client, consTarget))
                        {
                            _log.LogInformation($"Consolidation block {consTarget.Height} is OK.");

                            localState.lastVerifiedConsHeight = consTarget.Height;
                            LocalDbSyncState.Save(localState);
                        }
                        else
                        {
                            _log.LogError($"Consolidation block {consTarget.Height} is failure.");
                            IsSuccess = false;
                            break;
                        }
                    }
                }
                else
                {
                    break;
                }

                if (!IsSuccess)
                    break;
            }

            return IsSuccess;
        }

        private async Task EngagingSyncAsync(long height)
        {
            // most db is synced. 
            // so make sure Last Float Hash equal to seed.
            LyraClientForNode client = await GetOptimizedSyncClientAsync();
            while (true)
            {
                _log.LogInformation("Engaging Sync...");
                var lastConsOfSeed = await client.GetLastConsolidationBlockAsync();
                var myLastCons = await _sys.Storage.GetLastConsolidationBlockAsync();
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
                        await _sys.Storage.AddBlockAsync(blockResult.GetBlock());
                    }
                }

                var remoteState = await client.GetSyncState();
                var localState = await GetNodeStatusAsync();
                if (remoteState.Status.lastConsolidationHash == localState.lastConsolidationHash &&
                    remoteState.Status.lastUnSolidationHash == localState.lastUnSolidationHash)
                    break;
                else
                {
                    // we need to know why
                    _log.LogWarning($"Engaging sync local vs remote: lastcons {localState.lastConsolidationHash.Shorten()} {remoteState.Status.lastConsolidationHash.Shorten()}, last uncons: {localState.lastUnSolidationHash.Shorten()} {remoteState.Status.lastUnSolidationHash.Shorten()}");
                }

                _log.LogInformation("Engaging Sync partial success. continue...");
                await Task.Delay(1000);
            }
        }

        private async Task<bool> SyncAndVerifyConsolidationBlock(LyraClientForNode client, ConsolidationBlock consBlock)
        {
            _log.LogInformation($"Sync and verify consolidation block height {consBlock.Height}");

            var myConsBlock = await _sys.Storage.FindBlockByHashAsync(consBlock.Hash) as ConsolidationBlock;
            if (myConsBlock == null)
            {
                await _sys.Storage.AddBlockAsync(consBlock);
                myConsBlock = consBlock;
            }
            else
            {
                if (!myConsBlock.VerifyHash())
                {
                    await _sys.Storage.RemoveBlockAsync(myConsBlock.Hash);
                    await _sys.Storage.AddBlockAsync(consBlock);
                    myConsBlock = consBlock;
                }
            }

            var mt = new MerkleTree();
            foreach (var hash in myConsBlock.blockHashes)
            {
                var myBlock = await _sys.Storage.FindBlockByHashAsync(hash);
                if (myBlock == null)
                {
                    if (!await SyncOneBlockAsync(client, hash, false))
                        return false;
                    else
                        myBlock = await _sys.Storage.FindBlockByHashAsync(hash);
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
                    await _sys.Storage.RemoveBlockAsync(extraBlock);
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
                    var localBlock = await _sys.Storage.FindBlockByHashAsync(block.Hash);
                    if (localBlock != null)
                        await _sys.Storage.RemoveBlockAsync(block.Hash);

                    await _sys.Storage.AddBlockAsync(block);
                }

                // save cons block itself
                var localCons = await _sys.Storage.FindBlockByHashAsync(consBlock.Hash);
                if (localCons != null)
                    await _sys.Storage.RemoveBlockAsync(consBlock.Hash);

                await _sys.Storage.AddBlockAsync(consBlock);
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
                    var localBlock = await _sys.Storage.FindBlockByHashAsync(hash);
                    if (localBlock != null)
                        await _sys.Storage.RemoveBlockAsync(hash);

                    await _sys.Storage.AddBlockAsync(blockResult.GetBlock());
                }
            }
        }

        private async Task<bool> SyncOneBlockAsync(LyraClientForNode client, string hash, bool removeLocal)
        {
            if (removeLocal)
                await _sys.Storage.RemoveBlockAsync(hash);

            var remoteBlock = await client.GetBlockByHash(hash);
            if (remoteBlock.ResultCode == APIResultCodes.Success)
                return await _sys.Storage.AddBlockAsync(remoteBlock.GetBlock());
            else
                return false;
        }

        private async Task GenesisAsync()
        {
            // genesis
            _log.LogInformation("all seed nodes are ready. do genesis.");

            var svcGen = await CreateServiceGenesisBlockAsync();
            await SendBlockToConsensusAsync(svcGen, ProtocolSettings.Default.StandbyValidators.ToList());

            await Task.Delay(10000);

            var tokenGen = CreateLyraTokenGenesisBlock(svcGen);
            // DEBUG
            //_log.LogInformation("genesis block string:\n" + tokenGen.GetHashInput());
            await SendBlockToConsensusAsync(tokenGen);

            await Task.Delay(15000);        // because cons block has a time shift.

            var consGen = CreateConsolidationGenesisBlock(svcGen, tokenGen);
            await SendBlockToConsensusAsync(consGen);

            await Task.Delay(1000);

            _log.LogInformation("svc genesis is done.");

            await Task.Delay(30000);

            // distribute staking coin to pre-defined authorizers
            var memStore = new AccountInMemoryStorage();
            Wallet.Create(memStore, "tmp", "", Settings.Default.LyraNode.Lyra.NetworkId, _sys.PosWallet.PrivateKey);
            var gensWallet = Wallet.Open(memStore, "tmp", "");
            foreach (var accId in ProtocolSettings.Default.StandbyValidators.Skip(1).Concat(ProtocolSettings.Default.StartupValidators))
            {
                var client = await new LyraClientForNode(_sys).FindValidSeedForSyncAsync(_sys);
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

            await CreateConsolidationBlock();

            await Task.Delay(3000);
        }

        private bool _creatingSvcBlock;
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
                        var prevSvcBlock = await _sys.Storage.GetLastServiceBlockAsync();

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

                        _log.LogInformation($"Adding {_board.AllVoters.Count()} voters...");

                        svcBlock.Authorizers = new Dictionary<string, string>();
                        // me as the first one
                        var meNode = _board.ActiveNodes.First(a => a.AccountID == _sys.PosWallet.AccountId);
                        svcBlock.Authorizers.Add(meNode.AccountID, meNode.AuthorizerSignature);
                        foreach (var voter in _board.AllVoters)
                        {
                            if (voter == _sys.PosWallet.AccountId)
                                continue;

                            if (_board.ActiveNodes.Any(a => a.AccountID == voter))
                            {
                                var node = _board.ActiveNodes.First(a => a.AccountID == voter);
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

                        _log.LogInformation($"New View was created. send to network...");
                        await SendBlockToConsensusAsync(svcBlock, _board.AllVoters);
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

        private LyraRestClient _seed0Client;
        public LyraRestClient GetClientForSeed0()
        {
            if (_seed0Client == null)
            {
                var addr = ProtocolSettings.Default.SeedList[0].Split(':')[0];
                var apiUrl = $"http://{addr}:4505/api/Node/";
                _log.LogInformation("Platform {1} Use seed node of {0}", apiUrl, Environment.OSVersion.Platform);
                _seed0Client = LyraRestClient.Create(Settings.Default.LyraNode.Lyra.NetworkId, Environment.OSVersion.Platform.ToString(), "LyraNode2", "1.0", apiUrl);

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
                NetworkId = Settings.Default.LyraNode.Lyra.NetworkId,
                Leader = ProtocolSettings.Default.StandbyValidators[0],
                Height = 1,
                FeeTicker = LyraGlobal.OFFICIALTICKERCODE,
                TransferFee = 1,           //zero for genesis. back to normal when genesis done
                TokenGenerationFee = 100,
                TradeFee = 0.1m,
                FeesGenerated = 0
            };

            // wait for all nodes ready
            while (true)
            {
                svcGenesis.Authorizers = new Dictionary<string, string>();
                foreach (var pn in ProtocolSettings.Default.StandbyValidators)
                {
                    if (!_board.ActiveNodes.Any(a => a.AccountID == pn))
                        break;

                    svcGenesis.Authorizers.Add(pn, _board.ActiveNodes.First(a => a.AccountID == pn).AuthorizerSignature);
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
                msg.IsServiceBlock = true;
                state.SetView(Board.AllVoters);                
            }
            else
            {
                state = new AuthState(true);
                msg.IsServiceBlock = false;
                state.SetView(Board.PrimaryAuthorizers);
            }
            state.InputMsg = msg;

            await SubmitToConsensusAsync(state);

            await state.Done.AsTask();
            state.Done.Close();
            state.Done = null;
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

    public enum BlockChainState
    {
        NULL,
        Initializing,
        StaticSync,    // the default mode. app started. wait for p2p stack up.
        Engaging,   // storing new commit while syncing blocks
        Almighty,   // fullly synced and working
        Genesis
    }

    public enum BlockChainTrigger
    {
        // initializing
        LocalNodeStartup,

        // basic sync
        DatabaseSync,

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
}
