using Clifton.Blockchain;
using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data;
using Lyra.Data.API;
using Lyra.Data.Crypto;
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
            var unCons = lastCons == null ? null : (await _sys.Storage.GetBlockHashesByTimeRange(lastCons.TimeStamp, DateTime.MaxValue)).ToList();
            var status = new NodeStatus
            {
                accountId = _sys.PosWallet.AccountId,
                version = LyraGlobal.NodeAppName,
                state = _stateMachine.State,
                totalBlockCount = lastCons == null ? 0 : lastCons.totalBlockCount + unCons.Count(),
                lastConsolidationHash = lastCons?.Hash,
                lastUnSolidationHash = GetUnConsolidatedHash(unCons),
                activePeers = Board.ActiveNodes.Count,
                connectedPeers = Neo.Network.P2P.LocalNode.Singleton.ConnectedCount,
                now = DateTime.UtcNow
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

        private async Task<bool> SyncDatabase()
        {
            var consensusClient = _networkClient;

            BlockAPIResult seedSvcGen;
            while (true)
            {
                seedSvcGen = await consensusClient.GetServiceGenesisBlock();
                if (seedSvcGen.ResultCode == APIResultCodes.Success)
                    break;

                await Task.Delay(10 * 1000);

                _log.LogInformation("Recreate aggregated client...");
                _networkClient = new LyraClientForNode(_sys);
                _networkClient.Client = await _networkClient.FindValidSeedForSyncAsync();
            }
            

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

                if(oldState.databaseVersion > 0 && oldState.databaseVersion < LyraGlobal.DatabaseVersion)
                {
                    // should upgrade database or resync completely
                    _sys.Storage.Delete();
                    LocalDbSyncState.Remove();
                    localDbState = await GetNodeStatusAsync();
                }
            }

            var localState = LocalDbSyncState.Load();
            if(localState.svcGenHash == null)
            {
                localState.svcGenHash = seedSvcGen.GetBlock().Hash;
                localState.databaseVersion = LyraGlobal.DatabaseVersion;
            }

            var lastCons = (await consensusClient.GetLastConsolidationBlockAsync()).GetBlock() as ConsolidationBlock;
            if (lastCons == null)
                return false;

            bool IsSuccess = true;
            var _authorizers = new AuthorizersFactory();
            while (true)
            {
                try
                {
                    var remoteConsQuery = await consensusClient.GetConsolidationBlocks(localState.lastVerifiedConsHeight + 1);
                    if(remoteConsQuery.ResultCode == APIResultCodes.Success)
                    {
                        var remoteConsBlocks = remoteConsQuery.GetBlocks();
                        if(remoteConsBlocks.Any())
                        {
                            foreach (var block in remoteConsBlocks)
                            {
                                var consTarget = block as ConsolidationBlock;
                                _log.LogInformation($"SyncDatabase: Sync consolidation block {consTarget.Height} of total {lastCons.Height}.");
                                if (await SyncAndVerifyConsolidationBlock(_authorizers, consensusClient, consTarget))
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
                            IsSuccess = true;
                            break;
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

        private async Task EngagingSyncAsync()
        {
            // most db is synced. 
            // so make sure Last Float Hash equal to seed.
            var emptySyncTimes = 0;
            LyraClientForNode client = _networkClient;
            while (true)
            {
                try
                {
                    _log.LogInformation("Engaging Sync...");

                    var someBlockSynced = false;

                    var myLastCons = await _sys.Storage.GetLastConsolidationBlockAsync();

                    var lastConsOfSeed = await client.GetLastConsolidationBlockAsync();
                    if (lastConsOfSeed.ResultCode == APIResultCodes.Success)
                    {
                        var lastConsBlockOfSeed = lastConsOfSeed.GetBlock();
                        if (myLastCons == null || myLastCons.Height < lastConsBlockOfSeed.Height)
                        {
                            _log.LogInformation($"Engaging: new consolidation block {lastConsBlockOfSeed.Height}");
                            if (!await SyncDatabase())
                            {
                                _log.LogError($"Error sync database. wait 5 minutes and retry...");
                                await Task.Delay(5 * 60 * 1000);
                            }
                            someBlockSynced = true;
                            continue;
                        }
                    }
                    else
                    {
                        _log.LogError($"Error get database status. Please wait for retry...");

                        await Task.Delay(10 * 1000);

                        _log.LogInformation("Recreate aggregated client...");
                        _networkClient = new LyraClientForNode(_sys);
                        _networkClient.Client = await _networkClient.FindValidSeedForSyncAsync();
                        
                        continue;
                    }

                    _log.LogInformation($"Engaging: Sync all unconsolidated blocks");
                    // sync unconsolidated blocks
                    var endTime = DateTime.MaxValue;
                    var unConsHashResult = await client.GetBlockHashesByTimeRange(myLastCons.TimeStamp, endTime);
                    if (unConsHashResult.ResultCode == APIResultCodes.Success)
                    {
                        _log.LogInformation($"Engaging: total unconsolidated blocks {unConsHashResult.Entities.Count}");
                        var myUnConsHashes = await _sys.Storage.GetBlockHashesByTimeRange(myLastCons.TimeStamp, endTime);
                        foreach (var h in myUnConsHashes)
                        {
                            if (!unConsHashResult.Entities.Contains(h))
                            {
                                await _sys.Storage.RemoveBlockAsync(h);
                                someBlockSynced = true;
                            }
                        }

                        int count = 0;
                        foreach (var hash in unConsHashResult.Entities)  // the first one is previous consolidation block
                        {
                            _log.LogInformation($"Engaging: Syncunconsolidated block {count++}/{unConsHashResult.Entities.Count}");
                            if (hash == myLastCons.Hash)
                                continue;       // already synced by previous steps

                            var localBlock = await _sys.Storage.FindBlockByHashAsync(hash);
                            if (localBlock != null)
                                continue;

                            var blockResult = await client.GetBlockByHash(hash);
                            if (blockResult.ResultCode == APIResultCodes.Success)
                            {
                                await _sys.Storage.AddBlockAsync(blockResult.GetBlock());
                                someBlockSynced = true;
                            }
                            else if(blockResult.ResultCode == APIResultCodes.APISignatureValidationFailed)
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
                    else if (unConsHashResult.ResultCode == APIResultCodes.APIRouteFailed)
                    {
                        _log.LogInformation("Recreate aggregated client...");
                        _networkClient = new LyraClientForNode(_sys);
                        _networkClient.Client = await _networkClient.FindValidSeedForSyncAsync();
                        continue;
                    }
                    else
                    {
                        continue;
                    }

                    // update billboard to latest
                    var lastServiceBlock = await _sys.Storage.GetLastServiceBlockAsync();
                    ServiceBlockCreated(lastServiceBlock);

                    _log.LogInformation($"Engaging: finalizing...");

                    var remoteState = await client.GetSyncState();
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
                                await Task.Delay(20000);                                
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

        private async Task<bool> SyncAndVerifyConsolidationBlock(AuthorizersFactory factory, LyraClientForNode client, ConsolidationBlock consBlock)
        {
            _log.LogInformation($"Sync and verify consolidation block height {consBlock.Height}");

            foreach(var hash in consBlock.blockHashes)
            {
                if (!await SyncOneBlockAsync(factory, client, hash))
                    return false;
            }

            if (null != await _sys.Storage.FindBlockByHashAsync(consBlock.Hash))
                await _sys.Storage.RemoveBlockAsync(consBlock.Hash);

            var mt = new MerkleTree();
            foreach (var hash1 in consBlock.blockHashes)
            {
                mt.AppendLeaf(MerkleHash.Create(hash1));
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

            return await _sys.Storage.AddBlockAsync(consBlock);
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

        private async Task<bool> SyncOneBlockAsync(AuthorizersFactory factory, LyraClientForNode client, string hash)
        {
            if(null != await _sys.Storage.FindBlockByHashAsync(hash))
                await _sys.Storage.RemoveBlockAsync(hash);

            var remoteBlock = await client.GetBlockByHash(hash);
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
                    return false;
            }
            else
                return false;
        }

        private async Task GenesisAsync()
        {
            // genesis
            _log.LogInformation("all seed nodes are ready. do genesis.");

            var svcGen = await CreateServiceGenesisBlockAsync();
            await SendBlockToConsensusAndWaitResultAsync(svcGen, ProtocolSettings.Default.StandbyValidators.ToList());

            await Task.Delay(10000);

            var tokenGen = CreateLyraTokenGenesisBlock(svcGen);
            // DEBUG
            //_log.LogInformation("genesis block string:\n" + tokenGen.GetHashInput());
            await SendBlockToConsensusAndWaitResultAsync(tokenGen);

            await Task.Delay(25000);        // because cons block has a time shift.

            var consGen = CreateConsolidationGenesisBlock(svcGen, tokenGen);
            await SendBlockToConsensusAndWaitResultAsync(consGen);

            await Task.Delay(1000);

            _log.LogInformation("svc genesis is done.");

            await Task.Delay(30000);

            // distribute staking coin to pre-defined authorizers
            var memStore = new AccountInMemoryStorage();
            Wallet.Create(memStore, "tmp", "", Settings.Default.LyraNode.Lyra.NetworkId, _sys.PosWallet.PrivateKey);
            var gensWallet = Wallet.Open(memStore, "tmp", "");
            foreach (var accId in ProtocolSettings.Default.StandbyValidators.Skip(1).Concat(ProtocolSettings.Default.StartupValidators))
            {
                await gensWallet.Sync(_networkClient.Client.SeedClient);
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
        public async Task CreateNewViewAsNewLeaderAsync()
        {
            // look for changes. if necessary create a new svc block.
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
                    var signAgainst = prevSvcBlock?.Hash ?? ProtocolSettings.Default.StandbyValidators[0];
                    var myAuthSignr = Signatures.GetSignature(_sys.PosWallet.PrivateKey,
                            signAgainst, _sys.PosWallet.AccountId);

                    // check GetValidVoters in ServiceAuthorizer.cs
                    svcBlock.Authorizers.Add(_sys.PosWallet.AccountId, myAuthSignr);
                    foreach (var voter in _board.AllVoters)
                    {
                        if (voter == _sys.PosWallet.AccountId)
                            continue;

                        if (_board.ActiveNodes.Any(a => a.AccountID == voter))
                        {
                            var node = _board.ActiveNodes.First(a => a.AccountID == voter);

                            if (Signatures.VerifyAccountSignature(prevSvcBlock.Hash, node.AccountID, node.AuthorizerSignature))
                            {
                                svcBlock.Authorizers.Add(node.AccountID, node.AuthorizerSignature);
                            }
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
                    await SendBlockToConsensusAndWaitResultAsync(svcBlock, _board.AllVoters);
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
            consBlock.TimeStamp = DateTime.UtcNow.AddSeconds(-18);

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

        private async void CreatePoolFactory()
        {
            var factory = await _sys.Storage.GetPoolFactoryAsync();
            if (factory != null)
                return;

            if (Board.CurrentLeader == _sys.PosWallet.AccountId)
            {
                // current leader need to create the pool factory
                var sb = await _sys.Storage.GetLastServiceBlockAsync();
                var pf = new PoolFactoryBlock
                {
                    Height = 1,
                    AccountType = AccountTypes.Pool,
                    AccountID = PoolFactoryBlock.FactoryAccount,        // in fact we not use this account.
                    Balances = new Dictionary<string, long>(),
                    PreviousHash = sb.Hash,
                    ServiceHash = sb.Hash,
                    Fee = 0,
                    FeeType = AuthorizationFeeTypes.NoFee
                };

                pf.AddTag(Block.MANAGEDTAG, "");        // no othere variables.

                // pool blocks are service block so all service block signed by leader node
                pf.InitializeBlock(null, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
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
                    var client = LyraRestClient.Create(networkId, platform, appName, appVer, $"https://{Board.NodeAddresses[Board.CurrentLeader]}:{peerPort}/api/Node/");
                    _ = client.GetPool("a", "b");
                }     
            }
        }

        private async Task<(ConsensusResult?, APIResultCodes errorCode)> SendBlockToConsensusAndWaitResultAsync(Block block, List<string> voters = null)        // default is genesus, 4 default
        {
            if (block == null)
                throw new ArgumentNullException();

            AuthorizingMsg msg = new AuthorizingMsg
            {
                From = _sys.PosWallet.AccountId,
                Block = block,
                BlockHash = block.Hash,
                MsgType = ChatMessageType.AuthorizerPrePrepare
            };

            var state = CreateAuthringState(msg, true);

            await SubmitToConsensusAsync(state);

            await state.WaitForClose();

            return (state.CommitConsensus, state.GetMajorErrorCode());
        }

        public AuthState CreateAuthringState(AuthorizingMsg msg, bool sourceValid)
        {
            _log.LogInformation($"Consensus: CreateAuthringState Called: BlockIndex: {msg.Block.Height}");

            AuthState state;
            if (msg.Block is ServiceBlock sb)
            {
                _log.LogInformation($"AllVoters: {Board.AllVoters.Count}");
                state = new ServiceBlockAuthState(Board.AllVoters);
                msg.IsServiceBlock = true;
                state.SetView(Board.AllVoters);
            }
            else
            {
                state = new AuthState();
                msg.IsServiceBlock = false;
                state.SetView(Board.PrimaryAuthorizers);
            }
            state.InputMsg = msg;

            state.IsSourceValid = sourceValid;
            state.OnConsensusSuccess += Worker_OnConsensusSuccess;

            return state;
        }

        private class LocalDbSyncState
        {
            public int databaseVersion { get; set; }
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
