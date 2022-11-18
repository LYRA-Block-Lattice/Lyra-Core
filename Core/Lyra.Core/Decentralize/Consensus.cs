using Clifton.Blockchain;
using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.WorkFlow;
using Lyra.Data;
using Lyra.Data.API;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using Lyra.Data.Shared;
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
            var unCons = lastCons == null ? null : (await _sys.Storage.GetBlockHashesByTimeRangeAsync(lastCons.TimeStamp, DateTime.MaxValue)).ToList();
            var status = new NodeStatus
            {
                accountId = _sys.PosWallet.AccountId,
                version = LyraGlobal.NodeAppName,
                state = _stateMachine.State,
                totalBlockCount = await _sys.Storage.GetBlockCountAsync(), //lastCons == null ? 0 : lastCons.totalBlockCount + unCons.Count(),
                lastConsolidationHash = lastCons?.Hash,
                lastUnSolidationHash = GetUnConsolidatedHash(unCons),
                activePeers = Board.ActiveNodes.Count,
                connectedPeers = Neo.Network.P2P.LocalNode.Singleton.ConnectedCount,
                now = DateTime.UtcNow
            };
            return status;
        }

        public string GetUnConsolidatedHash(List<string>? unCons)
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

        private async Task<bool> SyncDatabaseAsync(ILyraAPI client, int rollBackCount)
        {
            _log.LogInformation("In SyncDatabaseAsync");
            var consensusClient = client;
            var fastClient = await CreateFastClientAsync();

            //BlockAPIResult seedSvcGen = null;
            //for (int i = 0; i < 10; i++)
            //{
            //    seedSvcGen = await consensusClient.GetServiceGenesisBlockAsync();
            //    if (seedSvcGen.ResultCode == APIResultCodes.Success)
            //        break;

            //    await Task.Delay(10 * 1000);

            //    _log.LogInformation("Recreate aggregated client...");
            //    //await client.InitAsync();
            //}

            //_log.LogInformation("In SyncDatabaseAsync, await GetNodeStatusAsync");

            var localDbState = await GetNodeStatusAsync();
            if (localDbState.totalBlockCount == 0)
            {
                LocalDbSyncState.Remove();
            }                
            else
            {
                var oldState = LocalDbSyncState.Load();

                var seedSvcGen = await consensusClient.GetServiceGenesisBlockAsync();
                if (seedSvcGen.ResultCode != APIResultCodes.Success)
                    return false;

                if (oldState.svcGenHash != seedSvcGen.GetBlock()?.Hash)
                    LocalDbSyncState.Remove();

                //if(oldState.databaseVersion > 0 && oldState.databaseVersion < LyraGlobal.DatabaseVersion)
                //{
                //    // should upgrade database or resync completely
                //    _sys.Storage.Delete(true);
                //    LocalDbSyncState.Remove();
                //    localDbState = await GetNodeStatusAsync();
                //}
            }

            var localState = LocalDbSyncState.Load();
            //if(localState.svcGenHash == null)
            //{
            //    localState.svcGenHash = seedSvcGen.GetBlock().Hash;
            //    localState.databaseVersion = LyraGlobal.DatabaseVersion;
            //}
            if (localState.lastVerifiedConsHeight > rollBackCount)
                localState.lastVerifiedConsHeight -= rollBackCount; // always do it to make sure db is good.
            else
                localState.lastVerifiedConsHeight = 0;

            var lastCons = (await consensusClient.GetLastConsolidationBlockAsync()).GetBlock() as ConsolidationBlock;
            if (lastCons == null)
                return false;

            bool IsSuccess;
            while (true)
            {
                _log.LogInformation("while true in SyncDatabaseAsync");
                try
                {
                    var remoteConsQuery = await consensusClient.GetConsolidationBlocksAsync(_sys.PosWallet.AccountId, null, localState.lastVerifiedConsHeight + 1, 1);
                    if(remoteConsQuery.ResultCode == APIResultCodes.Success)
                    {
                        var remoteConsBlocks = remoteConsQuery.GetBlocks();
                        if(remoteConsBlocks.Any())
                        {
                            foreach (var block in remoteConsBlocks)
                            {
                                var consTarget = block as ConsolidationBlock;
                                _log.LogInformation($"SyncDatabase: Sync consolidation block {consTarget?.Height} of total {lastCons.Height}.");
                                if (await SyncAndVerifyConsolidationBlockAsync(consensusClient, fastClient, consTarget))
                                {
                                    _log.LogInformation($"Consolidation block {consTarget.Height} is OK.");

                                    localState.lastVerifiedConsHeight = consTarget.Height;
                                    LocalDbSyncState.Save(localState);
                                }
                                else
                                {
                                    throw new Exception($"Consolidation block {consTarget.Height} is failure.");
                                }
                            }
                        }
                        else
                        {
                            _log.LogInformation($"sync unconsolidated blocks by lastCons");
                            // here need to sync unconsolidated blocks.
                            var lastConsToSyncQuery = await consensusClient.GetLastConsolidationBlockAsync();
                            if (lastConsToSyncQuery.Successful())
                            {
                                var lastConsToSync = lastConsToSyncQuery.GetBlock() as ConsolidationBlock;
                                await SyncAllUnConsolidatedBlocks(lastConsToSync, consensusClient);

                                IsSuccess = true;
                                break;
                            }
                            else
                            {
                                throw new Exception("Failed to sync uncons blocks. reason: " + remoteConsQuery.ResultCode);
                            }
                        }
                    }
                    else if(remoteConsQuery.ResultCode == APIResultCodes.APIRouteFailed)
                    {
                        _log.LogWarning("Got inconsistant result from network. retry later.");
                        throw new Exception("Failed to sync. reason: " + remoteConsQuery.ResultCode);
                    }
                    else
                    {
                        _log.LogWarning($"Unexpected error {remoteConsQuery.ResultCode}: {remoteConsQuery.ResultMessage}. retry later.");
                        throw new Exception($"Failed to sync. reason: {remoteConsQuery.ResultCode}: {remoteConsQuery.ResultMessage}");
                    }
                }
                catch (Exception ex)
                {
                    _log.LogWarning("SyncDatabase Exception: " + ex.Message);
                    await Task.Delay(30000);
                    IsSuccess = false;
                    break;
                }
            }

            return IsSuccess;
        }

        private async Task<bool> SyncAllUnConsolidatedBlocks(ConsolidationBlock myLastCons, ILyraAPI client)
        {
            _log.LogInformation($"Syncing unconsolidated block after last cons {myLastCons.Height}");

            bool someBlockSynced = false;
            // sync unconsolidated blocks
            var endTime = DateTime.MaxValue;
            var unConsHashResult = await client.GetBlockHashesByTimeRangeAsync(myLastCons.TimeStamp.Ticks, endTime.Ticks);
            if (unConsHashResult.ResultCode == APIResultCodes.Success)
            {
                _log.LogInformation($"Total unconsolidated blocks: {unConsHashResult.Entities.Count}");
                var myUnConsHashes = await _sys.Storage.GetBlockHashesByTimeRangeAsync(myLastCons.TimeStamp, endTime);

                // first hash is previous consblock

                foreach (var h in myUnConsHashes)
                {
                    if (h != myLastCons.Hash && !unConsHashResult.Entities.Contains(h))
                    {
                        await _sys.Storage.RemoveBlockAsync(h);
                        someBlockSynced = true;
                    }
                }

                foreach (var hash in unConsHashResult.Entities)  // the first one is previous consolidation block
                {
                    //_log.LogInformation($"Engaging: Syncunconsolidated block {count++}/{unConsHashResult.Entities.Count}");
                    if (hash == myLastCons.Hash)
                        continue;       // already synced by previous steps

                    var localBlock = await _sys.Storage.FindBlockByHashAsync(hash);
                    if (localBlock != null)
                        continue;

                    var blockResult = await client.GetBlockByHashAsync(_sys.PosWallet.AccountId, hash, null);
                    if (blockResult.ResultCode == APIResultCodes.Success)
                    {
                        await _sys.Storage.AddBlockAsync(blockResult.GetBlock());
                        someBlockSynced = true;
                    }
                    else if (blockResult.ResultCode == APIResultCodes.APISignatureValidationFailed)
                    {
                        throw new Exception("Desynced by new service block.");
                    }
                    else
                    {
                        someBlockSynced = true;
                        break;
                    }
                }
            }
            _log.LogInformation($"Synced unconsolidated block. New one? {someBlockSynced}");
            return someBlockSynced;
        }

        private async Task EngagingSyncAsync()
        {
            // most db is synced. 
            // so make sure Last Float Hash equal to seed.
            var emptySyncTimes = 0;
            var client = CreateSafeClient();

            // first make sure db is synced, especially when trans from almighty
            await SyncDatabaseAsync(client, 1);

            for (int ii = 0; ii < 15; ii++)
            {
                try
                {
                    _log.LogInformation($"No. {ii} Engaging Sync...");

                    var someBlockSynced = false;

                    var myLastCons = await _sys.Storage.GetLastConsolidationBlockAsync();

                    var lastConsOfSeed = await client.GetLastConsolidationBlockAsync();
                    if (lastConsOfSeed.ResultCode == APIResultCodes.Success)
                    {
                        var lastConsBlockOfSeed = lastConsOfSeed.GetBlock();
                        if (myLastCons == null || myLastCons.Height < lastConsBlockOfSeed.Height)
                        {
                            _log.LogInformation($"Engaging: new consolidation block {lastConsBlockOfSeed.Height}");
                            if (!await SyncDatabaseAsync(client, 0))
                            {
                                _log.LogError($"Error sync database. retry...");
                                await Task.Delay(5 * 1000);
                            }
                            someBlockSynced = true;
                            continue;
                        }
                    }
                    else
                    {
                        _log.LogError($"Error get database status: {lastConsOfSeed.ResultCode}. Please wait for retry...");

                        await Task.Delay(10 * 1000);

                        //if(client is LyraAggregatedClient agg)
                        //{
                        //    _log.LogInformation("Recreate aggregated client...");
                        //    if (lastConsOfSeed.ResultCode == APIResultCodes.APIRouteFailed)
                        //        agg.ReBase(true);
                        //}
                        
                        continue;
                    }

                    someBlockSynced = await SyncAllUnConsolidatedBlocks(myLastCons, client);

                    // update billboard to latest
                    var lastServiceBlock = await _sys.Storage.GetLastServiceBlockAsync();
                    ServiceBlockCreated(lastServiceBlock);

                    //_log.LogInformation($"Engaging: finalizing...");

                    var remoteState = await client.GetSyncStateAsync();
                    if (remoteState.ResultCode != APIResultCodes.Success)
                        continue;

                    var localState = await GetNodeStatusAsync();
                    if (remoteState.Status.lastConsolidationHash == localState.lastConsolidationHash
                        && remoteState.Status.lastUnSolidationHash == localState.lastUnSolidationHash
                        )
                    {
                        if (someBlockSynced)
                        {
                            emptySyncTimes = 0;
                            continue;
                        }                            
                        else
                        {
                            emptySyncTimes++;

                            if (emptySyncTimes >= 3)
                                break;
                            else
                            {
                                _log.LogInformation("Waiting for any new changes ...");
                                await Task.Delay(5000);                                
                            }
                        }
                    }
                    else
                    {
                        // we need to know why
                        _log.LogWarning($"Engaging sync local vs remote: lastcons {localState.lastConsolidationHash.Shorten()} {remoteState.Status.lastConsolidationHash.Shorten()}, last uncons: {localState.lastUnSolidationHash.Shorten()} {remoteState.Status.lastUnSolidationHash.Shorten()}");
                    }

                    _log.LogInformation("Engaging Sync partial success. continue...");
                    await Task.Delay(1000);
                }
                catch(Exception ex)
                {
                    _log.LogInformation($"Engaging Sync failed with error \"{ex.Message}\". continue...");
                    await Task.Delay(1000);
                }
            }
        }

        /// <summary>
        /// sync consblock and all it's contained blocks. (previous cons -> this cons.)
        /// </summary>
        /// <param name="client"></param>
        /// <param name="consBlock"></param>
        /// <returns></returns>
        private async Task<bool> SyncAndVerifyConsolidationBlockAsync(ILyraAPI safeClient, ILyraAPI fastClient, ConsolidationBlock consBlock)
        {
            _log.LogInformation($"Sync and verify consolidation block height {consBlock.Height}");

            foreach(var hash in consBlock.blockHashes)
            {
                if (!await SyncOneBlockAsync(fastClient, hash))
                    return false;
            }

            var mt = new MerkleTree();
            foreach (var hash1 in consBlock.blockHashes)
            {
                mt.AppendLeaf(MerkleHash.Create(hash1));
            }
            var merkelTreeHash = mt.BuildTree().ToString();

            if (consBlock.MerkelTreeHash != merkelTreeHash)
            {
                _log.LogWarning($"SyncAndVerifyConsolidationBlock: consMerkelTree: {consBlock.MerkelTreeHash} mine: {merkelTreeHash}");
                return false;
            }                

            // make sure no extra blocks here
            if (consBlock.Height > 1)
            {
                var prevConsHash = consBlock.blockHashes.First();
                var prevConsResult = await fastClient.GetBlockByHashAsync(_sys.PosWallet.AccountId, prevConsHash, null);
                if (prevConsResult.ResultCode != APIResultCodes.Success)
                {
                    _log.LogWarning($"SyncAndVerifyConsolidationBlock: prevConsResult.ResultCode: {prevConsResult.ResultCode}");
                    return false;
                }                    

                var prevConsBlock = prevConsResult.GetBlock() as ConsolidationBlock;
                if (prevConsBlock == null)
                {
                    _log.LogWarning($"SyncAndVerifyConsolidationBlock: prevConsBlock: null");
                    return false;
                }

                var blocksInTimeRange = await _sys.Storage.GetBlockHashesByTimeRangeAsync(prevConsBlock.TimeStamp, consBlock.TimeStamp);
                var q = blocksInTimeRange.Where(a => !consBlock.blockHashes.Contains(a));
                foreach (var extraBlock in q)
                {
                    await _sys.Storage.RemoveBlockAsync(extraBlock);
                }
            }

            if (null == await _sys.Storage.FindBlockByHashAsync(consBlock.Hash))
                await _sys.Storage.AddBlockAsync(consBlock);

            return true;
        }

        //private async Task SyncManyBlocksAsync(LyraClientForNode client, ConsolidationBlock consBlock)
        //{
        //    _log.LogInformation($"Syncing Consolidations {consBlock.Height} / {consBlock.Hash.Shorten()} ");

        //    var blocksResult = await client.GetBlocksByConsolidation(consBlock.Hash);
        //    if (blocksResult.ResultCode == APIResultCodes.Success)
        //    {
        //        foreach (var block in blocksResult.GetBlocks())
        //        {
        //            var localBlock = await _sys.Storage.FindBlockByHashAsync(block.Hash);
        //            if (localBlock != null)
        //                await _sys.Storage.RemoveBlockAsync(block.Hash);

        //            await _sys.Storage.AddBlockAsync(block);
        //        }

        //        // save cons block itself
        //        var localCons = await _sys.Storage.FindBlockByHashAsync(consBlock.Hash);
        //        if (localCons != null)
        //            await _sys.Storage.RemoveBlockAsync(consBlock.Hash);

        //        await _sys.Storage.AddBlockAsync(consBlock);
        //    }
        //}

        //private async Task SyncManyBlocksAsync(LyraClientForNode client, List<string> hashes)
        //{
        //    _log.LogInformation($"Syncing {hashes.Count()} blocks...");

        //    foreach (var hash in hashes)
        //    {
        //        var blockResult = await client.GetBlockByHash(hash);
        //        if (blockResult.ResultCode == APIResultCodes.Success)
        //        {
        //            var localBlock = await _sys.Storage.FindBlockByHashAsync(hash);
        //            if (localBlock != null)
        //                await _sys.Storage.RemoveBlockAsync(hash);

        //            await _sys.Storage.AddBlockAsync(blockResult.GetBlock());
        //        }
        //    }
        //}

        private async Task<bool> SyncOneBlockAsync(ILyraAPI client, string hash)
        {
            if(null != await _sys.Storage.FindBlockByHashAsync(hash))
            {
                return true;
            }

            var remoteBlock = await client.GetBlockByHashAsync(_sys.PosWallet.AccountId, hash, null);
            if (remoteBlock.ResultCode == APIResultCodes.Success)
            {
                var block = remoteBlock.GetBlock();

                // when block stored into database, they lost their order. so it's hard to do verification style of authorizer.
                // and mixed with code change/logic upgrade, so just use hash verify to keep database integraty.
                //if(block is TransactionBlock tb)
                //{
                //    var authorizer = factory.Create(tb.BlockType);
                //    var authResult = await authorizer.AuthorizeAsync(GetDagSystem(), tb);
                //    if (authResult.Item1 != APIResultCodes.Success)
                //    {
                //        _log.LogWarning($"SyncOneBlockAsync: TX block {tb.Hash.Shorten()} failed to verify for {authResult.Item1}");
                //    }                        
                //}

                // non tx block just verify hash
                if (block != null && block.VerifyHash())
                    return await _sys.Storage.AddBlockAsync(block);
                else
                {
                    _log.LogWarning($"Error SyncOneBlockAsync: block null? {block is null}");
                    if(!(block is null))
                        _log.LogWarning($"Error SyncOneBlockAsync: block VerifyHash? {block.VerifyHash()}");
                    return false;
                }                    
            }
            else
            {
                _log.LogWarning($"Error SyncOneBlockAsync: remote return: {remoteBlock.ResultCode}");
                return false;
            }                
        }

        public async Task GenesisAsync()
        {
            // genesis
            _log.LogInformation("all seed nodes are ready. do genesis.");

            var svcGen = await CreateServiceGenesisBlockAsync();
            await SendBlockToConsensusAndWaitResultAsync(svcGen, ProtocolSettings.Default.StandbyValidators.ToList());

            await Task.Delay(5000);

            var tokenGen = CreateLyraTokenGenesisBlock(svcGen);
            // DEBUG
            //_log.LogInformation("genesis block string:\n" + tokenGen.GetHashInput());
            await SendBlockToConsensusAndWaitResultAsync(tokenGen);

            await Task.Delay(2000);
            var pf = await CreatePoolFactoryBlockAsync();
            await SendBlockToConsensusAndWaitResultAsync(pf);

            await Task.Delay(25000);        // because cons block has a time shift.

            var consGen = CreateConsolidationGenesisBlock(svcGen, tokenGen, pf);
            await SendBlockToConsensusAndWaitResultAsync(consGen);

            await Task.Delay(1000);

            _log.LogInformation("Genesis is done.");

            await Task.Delay(30000);

            // distribute staking coin to pre-defined authorizers
            var memStore = new AccountInMemoryStorage();
            Wallet.Create(memStore, "tmp", "", Settings.Default.LyraNode.Lyra.NetworkId, _sys.PosWallet.PrivateKey);
            var gensWallet = Wallet.Open(memStore, "tmp", "");
            gensWallet.SetVoteFor(_sys.PosWallet.AccountId);
            foreach (var accId in ProtocolSettings.Default.StandbyValidators.Skip(1).Concat(ProtocolSettings.Default.StartupValidators))
            {
                var client = CreateSafeClient();
                await gensWallet.SyncAsync(client);
                var amount = LyraGlobal.MinimalAuthorizerBalance + 100000;
                var sendResult = await gensWallet.SendAsync(amount, accId);
                if (sendResult.ResultCode == APIResultCodes.Success)
                {
                    _log.LogInformation($"Genesis send {amount} successfull to accountId: {accId}");
                }
                else
                {
                    _log.LogError($"Genesis send {amount} failed to accountId: {accId}");
                }
            }
        }

        private bool _creatingSvcBlock;
        public async Task<ServiceBlock> CreateNewViewAsNewLeaderAsync()
        {
            var prevSvcBlock = await _sys.Storage.GetLastServiceBlockAsync();

            var svcBlock = new ServiceBlock
            {
                NetworkId = prevSvcBlock.NetworkId,
                Height = prevSvcBlock.Height + 1,
                FeeTicker = LyraGlobal.OFFICIALTICKERCODE,
                ServiceHash = prevSvcBlock.Hash,
                Leader = _board.LeaderCandidate,
                TransferFee = 1,           //zero for genesis. back to normal when genesis done
                TokenGenerationFee = 10000,
                TradeFee = 0.1m
            };

            _log.LogInformation($"Adding {_board.AllVoters.Count()} voters...");

            svcBlock.Authorizers = new Dictionary<string, string>();
            var signAgainst = prevSvcBlock?.Hash ?? ProtocolSettings.Default.StandbyValidators[0];
            foreach (var voter in _board.AllVoters)
            {
                var node = _board.ActiveNodes.FirstOrDefault(a => a.AccountID == voter);
                if (node != null && Signatures.VerifyAccountSignature(signAgainst, node.AccountID, node.AuthorizerSignature))
                    svcBlock.Authorizers.Add(node.AccountID, node.AuthorizerSignature);
            }

            // unite test only
            if (Settings.Default.LyraNode.Lyra.NetworkId == "xtest")
                svcBlock.Authorizers.Add(_board.CurrentLeader, null);

            // fees aggregation
            _log.LogInformation($"Fee aggregating...");
            var allConsBlocks = await _sys.Storage.GetConsolidationBlocksAsync(prevSvcBlock.Hash);
            svcBlock.FeesGenerated = allConsBlocks.Sum(a => a.totalFees);

            svcBlock.InitializeBlock(prevSvcBlock, _sys.PosWallet.PrivateKey, _sys.PosWallet.AccountId);

            return svcBlock;
        }

        public ConsolidationBlock CreateConsolidationGenesisBlock(ServiceBlock svcGen, LyraTokenGenesisBlock lyraGen, PoolFactoryBlock pf)
        {
            var consBlock = new ConsolidationBlock
            {
                createdBy = ProtocolSettings.Default.StandbyValidators[0],
                blockHashes = new List<string>()
                {
                    svcGen.Hash, lyraGen.Hash, pf.Hash
                },
                totalBlockCount = 3     // not including self
            };
            consBlock.TimeStamp = DateTime.UtcNow.AddSeconds(-18);

            var mt = new MerkleTree();
            mt.AppendLeaf(MerkleHash.Create(svcGen.Hash));
            mt.AppendLeaf(MerkleHash.Create(lyraGen.Hash));
            mt.AppendLeaf(MerkleHash.Create(pf.Hash));

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
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.Regular,
                RenewalDate = DateTime.UtcNow.AddYears(100)
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
                TokenGenerationFee = 10000,
                TradeFee = 0.1m,
                FeesGenerated = 0
            };

            // wait for all nodes ready
            // for unit test
            if(_localNode == null)      // unit test code
            {
                svcGenesis.Authorizers = new Dictionary<string, string>();
                foreach (var pn in ProtocolSettings.Default.StandbyValidators)
                {
                    svcGenesis.Authorizers.Add(pn, pn);
                }
            }
            else
            {
                while (true)
                {
                    _log.LogInformation("while true in CreateServiceGenesisBlockAsync");
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
            }

            svcGenesis.TimeStamp = DateTime.UtcNow;

            svcGenesis.InitializeBlock(null, _sys.PosWallet.PrivateKey,
                _sys.PosWallet.AccountId);
            return svcGenesis;
        }

        public async Task<PoolFactoryBlock> CreatePoolFactoryBlockAsync()
        {
            // current leader need to create the pool factory
            var sb = await _sys.Storage.GetLastServiceBlockAsync();
            var pf = new PoolFactoryBlock
            {
                Height = 1,
                AccountType = AccountTypes.PoolFactory,
                AccountID = PoolFactoryBlock.FactoryAccount,        // in fact we not use this account.
                Balances = new Dictionary<string, long>(),
                PreviousHash = sb.Hash,
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeType = AuthorizationFeeTypes.NoFee,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
            };

            pf.AddTag(Block.MANAGEDTAG, WFState.Finished.ToString());        // no othere variables.

            // pool blocks are service block so all service block signed by leader node
            pf.InitializeBlock(null, _sys.PosWallet.PrivateKey, AccountId: _sys.PosWallet.AccountId);
            return pf;
        }

        public async Task CreatePoolFactoryAsync()
        {
            var factory = await _sys.Storage.GetPoolFactoryAsync();
            if (factory != null)
                return;

            if (Board.CurrentLeader == _sys.PosWallet.AccountId)
            {
                var pf = await CreatePoolFactoryBlockAsync();
                await SendBlockToConsensusAndWaitResultAsync(pf);
            }
            else
            {
                // just send it to the leader
                var platform = Environment.OSVersion.Platform.ToString();
                var appName = "LyraNode";
                var appVer = "1.0";
                var networkId = Settings.Default.LyraNode.Lyra.NetworkId;
                ushort peerPort = 4504;
                if (networkId == "mainnet")
                    peerPort = 5504;

                if (Board.NodeAddresses.ContainsKey(Board.CurrentLeader))
                {
                    var hostAddrStr = Board.NodeAddresses[Board.CurrentLeader];
                    var hoststr = hostAddrStr.Contains(":") ? hostAddrStr : $"{hostAddrStr}:{peerPort}";
                    var client = LyraRestClient.Create(networkId, platform, appName, appVer, $"https://{hoststr}/api/Node/");
                    _ = client.GetPoolAsync("a", "b");
                }     
            }
        }

        public async Task LeaderSendBlockToConsensusAndForgetAsync(Block block)
        {
            if (block == null)
                throw new ArgumentNullException();

            if (Settings.Default.LyraNode.Lyra.NetworkId == "xtest")
            {
                _ = Task.Run(async () => { await OnNewBlock(block); }).ConfigureAwait(false);
            }
            else
            {
                await Task.Delay(1000);

                AuthorizingMsg msg = new AuthorizingMsg
                {
                    From = _sys.PosWallet.AccountId,
                    Block = block,
                    BlockHash = block.Hash!,
                    MsgType = ChatMessageType.AuthorizerPrePrepare
                };

                var statex = await CreateAuthringStateAsync(msg, true);

                if(statex.result != APIResultCodes.Success || statex.state == null)
                {
                    _log.LogWarning($"Failed to CreateAuthringStateAsync: {statex.result}");
                }

                var submitret = await SubmitToConsensusAsync(statex.state);
                if(!submitret)
                {
                    _log.LogWarning($"Failed to SubmitToConsensusAsync.");
                }
            }
        }

        private async Task<(ConsensusResult?, APIResultCodes errorCode)> SendBlockToConsensusAndWaitResultAsync(Block block, List<string>? voters = null)        // default is genesus, 4 default
        {
            if (block == null)
                throw new ArgumentNullException();

            AuthorizingMsg msg = new AuthorizingMsg
            {
                From = _sys.PosWallet.AccountId,
                Block = block,
                BlockHash = block.Hash!,
                MsgType = ChatMessageType.AuthorizerPrePrepare
            };

            var statex = await CreateAuthringStateAsync(msg, true);
            if(statex.result == APIResultCodes.Success)
            {
                var state = statex.state;
                var sent = await SubmitToConsensusAsync(statex.state);
                if (sent)
                {
                    await state.WaitForCloseAsync();

                    return (state.CommitConsensus, state.GetMajorErrorCode());
                }
                else
                {
                    return (null, APIResultCodes.DoubleSpentDetected);
                }
            }
            else
            {
                return (null, statex.result);
            }
        }

        public async Task<(APIResultCodes result, AuthState? state)> CreateAuthringStateAsync(AuthorizingMsg msg, bool sourceValid)
        {
            //_log.LogInformation($"Consensus: CreateAuthringState Called: BlockIndex: {msg.Block.Height}");

/*            if (msg.Block is TransactionBlock trans)
            {
                // check if a block is generated from workflow which has locked several chains.
                bool InWFOK = false;
                if (trans is IBrokerAccount brkr && IsRequestLocked(brkr.RelatedTx))
                {
                    if(IsRequestLocked(brkr.RelatedTx))
                    {
                        InWFOK = true;

                        // add the new block to locker dto
                        var lockdto = GetLockerDTOFromReq(brkr.RelatedTx);
                        lockdto.seqhashes.Add(trans.Hash);

                        if (lockdto.lockedups.Contains(trans.AccountID))
                        {
                            // then should be ok
                            
                            
                            // cascading lock? no.
                            //var lockdto = await WorkFlowBase.GetLocketDTOAsync(_sys, brkr.RelatedTx);
                        }
                        else
                        {
                            lockdto.lockedups.Add(trans.AccountID);  // prevent race condition. lock all blocks generated in WF.
                        }
                    }
                }
                else if(trans is IPool pool)
                {
                    if (IsRequestLocked(pool.RelatedTx))
                    {
                        InWFOK = true;

                        // add the new block to locker dto
                        var lockdto = GetLockerDTOFromReq(pool.RelatedTx);
                        lockdto.seqhashes.Add(trans.Hash);

                        if (lockdto.lockedups.Contains(trans.AccountID))
                        {
                            // then should be ok


                            // cascading lock? no.
                            //var lockdto = await WorkFlowBase.GetLocketDTOAsync(_sys, brkr.RelatedTx);
                        }
                        else
                        {
                            lockdto.lockedups.Add(trans.AccountID);  // prevent race condition. lock all blocks generated in WF.
                        }
                    }
                }

                if (!InWFOK)
                {
                    // check locker here
                    var lockdto = await WorkFlowBase.GetLocketDTOAsync(_sys, trans);
                    foreach (var str in lockdto.lockedups)
                    {
                        if (IsAccountLocked(str))
                        {
                            // some account was locked!
                            _log.LogWarning($"Resource is locked: {str}");
                            return (APIResultCodes.ResourceIsBusy, null);
                        }
                    }

                    //Console.WriteLine($"Try add a lockup for msg: {msg.BlockHash} accountid: {lockdto.reqhash}");
                    AddLockerDTO(lockdto);
                }
            }*/

            AuthState state;
            if (msg.Block is ServiceBlock sb)
            {
                _log.LogInformation($"AllVoters: {Board.AllVoters.Count}");
                state = new ServiceBlockAuthState(Worker_OnConsensusSuccessAsync, Board.AllVoters);
                msg.IsServiceBlock = true;
                state.SetView(Board.AllVoters);
            }
            else
            {
                state = new AuthState(Worker_OnConsensusSuccessAsync);
                msg.IsServiceBlock = false;
                state.SetView(Board.PrimaryAuthorizers);
            }
            state.InputMsg = msg;

            state.IsSourceValid = sourceValid;

            return (APIResultCodes.Success, state);
        }

        private class LocalDbSyncState
        {
            public int databaseVersion { get; set; }
            public string? svcGenHash { get; set; }      // make sure not mix with other dbs
            public long lastVerifiedConsHeight { get; set; }

            public static LocalDbSyncState Load()
            {
                try
                {
                    var fn = $"{Utilities.GetLyraDataDir(Neo.Settings.Default.LyraNode.Lyra.NetworkId, LyraGlobal.OFFICIALDOMAIN)}{Utilities.PathSeperator}syncState.json";
                    if (File.Exists(fn))
                        return JsonConvert.DeserializeObject<LocalDbSyncState>(File.ReadAllText(fn)) ?? new LocalDbSyncState();
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
        LocalNodeMissingBlock,

        // genesis
        GenesisDone
    }
}
