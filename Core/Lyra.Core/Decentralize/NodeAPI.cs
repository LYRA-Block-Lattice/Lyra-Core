using Lyra.Core.API;
using Lyra.Core.Blocks;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;
using Lyra.Core.Utils;
using Lyra.Core.Accounts;
using Neo;
using System.Collections.Generic;
using Lyra.Core.Authorizers;
using Clifton.Blockchain;
using Akka.Actor;
using System.Collections.Concurrent;
using Akka.Routing;
using Lyra.Data.API;
using Lyra.Data.Utils;
using Lyra.Data.Crypto;
using Lyra.Data.Blocks;
using Lyra.Data.API.WorkFlow;

namespace Lyra.Core.Decentralize
{
    public class NodeAPI : INodeAPI
    {
        public NodeAPI()
        {
        }

        //// smart routing based on last consensus
        //private async IAsyncEnumerable<LyraRestClient> GetRoutesAsync()
        //{
        //    if (ApiService.LastState == null)
        //        yield break;

        //    var networkId = LyraNodeConfig.GetNetworkId();
        //    int port = networkId.Equals("mainnet") ? 5505 : 4505;
        //    int sampleCount = 3;
        //    var bb = await NodeService.Dag.Consensus.Ask<BillBoard>(new ConsensusService.AskForBillboard());
        //    var majority = from commit in ApiService.LastState.CommitMsgs
        //                   join ipkvp in bb.NodeAddresses on commit.From equals ipkvp.Key
        //                   where commit.Consensus == ApiService.LastState.CommitConsensus
        //                   select new
        //                   {
        //                       commit.From,
        //                       IP = ipkvp.Value
        //                   };

        //    var samples = majority.OrderBy(x => _rnd.Next()).Take(sampleCount);
        //    foreach (var liveNode in samples)
        //    {
        //        if (_clients.ContainsKey(liveNode.From) && _clients[liveNode.From].Host == liveNode.IP)
        //            yield return _clients[liveNode.From];
        //        else
        //        {
        //            var client = LyraRestClient.Create(networkId, Environment.OSVersion.Platform.ToString(),
        //                "API Router", "1.0", $"http://{liveNode.IP}:{port}/api/Node/");
        //            yield return _clients.AddOrUpdate(liveNode.From, client, (k, v) => client);
        //        }
        //    }
        //}

        private async Task<bool> VerifyClientAsync(string accountId, string signature)
        {
            // seeds accountid not exists.
            //if (!await NodeService.Dag.Storage.AccountExistsAsync(accountId))
            //    return false;

            var lastSvcBlock = await NodeService.Dag.Storage.GetLastServiceBlockAsync();
            if (lastSvcBlock == null)
                return false;

            return Signatures.VerifyAccountSignature(lastSvcBlock.Hash, accountId, signature);
        }

        public async Task<GetSyncStateAPIResult> GetSyncStateAsync()
        {
            if (NodeService.Dag == null || NodeService.Dag.Storage == null || !NodeService.Dag.FullStarted)
            {
                return new GetSyncStateAPIResult
                {
                    ResultCode = APIResultCodes.SystemNotReadyToServe
                };
            }

            var consBlock = await NodeService.Dag.Storage.GetLastConsolidationBlockAsync();
            var chainStatus = await NodeService.Dag.Consensus.Ask<NodeStatus>(new ConsensusService.QueryBlockchainStatus());
            var result = new GetSyncStateAPIResult
            {
                ResultCode = APIResultCodes.Success,
                NetworkID = LyraNodeConfig.GetNetworkId(),
                SyncState = chainStatus.state == BlockChainState.Almighty ? ConsensusWorkingMode.Normal : ConsensusWorkingMode.OutofSyncWaiting,
                LastConsolidationHash = consBlock == null ? null : consBlock.Hash,
                Status = chainStatus
            };
            return result;
        }

        public Task<GetVersionAPIResult> GetVersionAsync(int apiVersion, string appName, string appVersion)
        {
            var result = new GetVersionAPIResult()
            {
                ResultCode = APIResultCodes.Success,
                ApiVersion = LyraGlobal.ProtocolVersion,
                NodeVersion = LyraGlobal.NodeAppName,
                UpgradeNeeded = false,
                MustUpgradeToConnect = apiVersion < LyraGlobal.ProtocolVersion,
                PosAccountId = NodeService.Dag.PosWallet.AccountId
            };
            return Task.FromResult(result);
        }

        private async Task<BlockAPIResult> InternalGetServiceGenesisBlockAsync()
        {
            var result = new BlockAPIResult();

            try
            {
                var block = await NodeService.Dag.Storage.GetServiceGenesisBlockAsync();
                if (block != null)
                {
                    result.BlockData = Json(block);
                    result.ResultBlockType = block.BlockType;
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                    result.ResultCode = APIResultCodes.ServiceBlockNotFound;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetServiceGenesisBlock: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public async Task<BlockAPIResult> GetServiceGenesisBlockAsync()
        {
            return await InternalGetServiceGenesisBlockAsync();
        }
        public async Task<BlockAPIResult> GetLyraTokenGenesisBlockAsync()
        {
            var result = new BlockAPIResult();

            try
            {
                var block = await NodeService.Dag.Storage.GetLyraTokenGenesisBlockAsync();
                if (block != null)
                {
                    result.BlockData = Json(block);
                    result.ResultBlockType = block.BlockType;
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                    result.ResultCode = APIResultCodes.ServiceBlockNotFound;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetLyraTokenGenesisBlock: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public async Task<AccountHeightAPIResult> GetSyncHeightAsync()
        {
            var result = new AccountHeightAPIResult();
            try
            {
                if (NodeService.Dag == null)
                    throw new Exception();

                var last_svc_block = await NodeService.Dag.Storage.GetLastServiceBlockAsync();
                //if(last_svc_block == null)
                //{
                //    // empty database. 
                //    throw new Exception("Database empty.");
                //}
                result.Height = last_svc_block == null ? 0 : last_svc_block.Height;
                result.SyncHash = last_svc_block == null ? "" : last_svc_block.Hash;
                result.NetworkId = Neo.Settings.Default.LyraNode.Lyra.NetworkId;
                result.ResultCode = APIResultCodes.Success;
            }
            catch (Exception ex)
            {
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = ex.Message;
            }
            return result;
        }

        public async Task<GetListStringAPIResult> GetTokenNamesAsync(string? AccountId, string? Signature, string keyword)
        {
            var result = new GetListStringAPIResult();
            //if(!await VerifyClientAsync(AccountId, Signature))
            //{
            //    result.ResultCode = APIResultCodes.APISignatureValidationFailed;
            //    return result;
            //}

            try
            {
                //if (!NodeService.Dag.Storage.AccountExists(AccountId))
                //    result.ResultCode = APIResultCodes.AccountDoesNotExist;

                var blocks = await NodeService.Dag.Storage.FindTokenGenesisBlocksAsync(keyword == "(null)" ? null : keyword);
                if (blocks != null)
                {
                    result.Entities = blocks.Select(a => a.Ticker).ToList();
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                    result.ResultCode = APIResultCodes.TokenGenesisBlockNotFound;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetTokenNames: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public async Task<AccountHeightAPIResult> GetAccountHeightAsync(string AccountId)
        {
            var result = new AccountHeightAPIResult();

            try
            {
                if (await NodeService.Dag.Storage.AccountExistsAsync(AccountId))
                {
                    result.Height = (await NodeService.Dag.Storage.FindLatestBlockAsync(AccountId)).Height;
                    result.NetworkId = Neo.Settings.Default.LyraNode.Lyra.NetworkId;
                    result.SyncHash = (await NodeService.Dag.Storage.GetLastConsolidationBlockAsync()).Hash;
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                {
                    result.ResultCode = APIResultCodes.AccountDoesNotExist;
                }
            }
            catch (Exception e)
            {
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }
            return result;
        }

        public async Task<BlockAPIResult> GetLastBlockAsync(string AccountId)
        {
            var result = new BlockAPIResult();

            try
            {
                var block = await NodeService.Dag.Storage.FindLatestBlockAsync(AccountId);
                if (block != null)
                {
                    result.BlockData = Json(block);
                    result.ResultBlockType = block.BlockType;
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                    result.ResultCode = APIResultCodes.BlockNotFound;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetLastBlock: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        //public async Task<T?> GetLastBlockAsAsync<T>(string AccountId) where T : Block, IBrokerAccount;
        //{
        //    var ret = await GetLastBlockAsync(AccountId);
        //    if (ret.Successful())
        //    {
        //        var blk = ret.GetBlock();
        //        return blk as T;
        //    }
        //    return null;
        //}

        public async Task<BlockAPIResult> GetBlockByIndexAsync(string AccountId, long Index)
        {
            var result = new BlockAPIResult();

            try
            {
                if (await NodeService.Dag.Storage.AccountExistsAsync(AccountId))
                {
                    var block = await NodeService.Dag.Storage.FindBlockByIndexAsync(AccountId, Index);
                    if (block != null)
                    {
                        result.BlockData = Json(block);
                        result.ResultBlockType = block.BlockType;
                        result.ResultCode = APIResultCodes.Success;
                    }
                    else
                        result.ResultCode = APIResultCodes.BlockNotFound;
                }
                else
                    result.ResultCode = APIResultCodes.AccountDoesNotExist;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetBlock: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public async Task<BlockAPIResult> GetServiceBlockByIndexAsync(string blockType, long Index)
        {
            var result = new BlockAPIResult();

            try
            {
                Block block;
                if (blockType == "Service")
                    block = await NodeService.Dag.Storage.FindServiceBlockByIndexAsync(Index);
                else if(blockType == "Consolidation")
                {
                    var cons = await NodeService.Dag.Storage.GetConsolidationBlocksAsync(Index, 1);
                    block = cons.First();
                }
                else
                {
                    throw new Exception("Unsupported service block type.");
                }

                if (block != null)
                {
                    result.BlockData = Json(block);
                    result.ResultBlockType = block.BlockType;
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                    result.ResultCode = APIResultCodes.BlockNotFound;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetServiceBlockByIndex: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public async Task<BlockAPIResult> GetBlockByHashAsync(string AccountId, string Hash, string? Signature)
        {
            var result = new BlockAPIResult();

            // engaging sync need to access this api without service block sync.
            //if (!await VerifyClientAsync(AccountId, Signature))
            //{
            //    result.ResultCode = APIResultCodes.APISignatureValidationFailed;
            //    return result;
            //}

            try
            {
                if (!await NodeService.Dag.Storage.AccountExistsAsync(AccountId))
                    result.ResultCode = APIResultCodes.AccountDoesNotExist;

                var block = await NodeService.Dag.Storage.FindBlockByHashAsync(Hash);
                if (block != null)
                {
                    result.BlockData = Json(block);
                    result.ResultBlockType = block.BlockType;
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                    result.ResultCode = APIResultCodes.BlockNotFound;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetBlock(Hash): " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public async Task<BlockAPIResult> GetBlockAsync(string Hash)
        {
            var result = new BlockAPIResult();

            try
            {
                var block = await NodeService.Dag.Storage.FindBlockByHashAsync(Hash);
                if (block != null)
                {
                    result.BlockData = Json(block);
                    result.ResultBlockType = block.BlockType;
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                    result.ResultCode = APIResultCodes.BlockNotFound;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetBlock(Hash): " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public async Task<BlockAPIResult> GetBlockBySourceHashAsync(string Hash)
        {
            var result = new BlockAPIResult();

            try
            {
                var block = await NodeService.Dag.Storage.FindBlockBySourceHashAsync(Hash);
                if (block != null)
                {
                    result.BlockData = Json(block);
                    result.ResultBlockType = block.BlockType;
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                    result.ResultCode = APIResultCodes.BlockNotFound;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetBlockBySourceHash(Hash): " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public async Task<MultiBlockAPIResult> GetBlocksByRelatedTxAsync(string Hash)
        {
            var result = new MultiBlockAPIResult();

            try
            {
                var blocks = await NodeService.Dag.Storage.FindBlocksByRelatedTxAsync(Hash);
                if (blocks == null)
                {
                    result.ResultCode = APIResultCodes.BlockNotFound;
                    return result;
                }

                result.SetBlocks(blocks.ToArray());
                result.ResultCode = APIResultCodes.Success;
                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetBlocksByRelatedTxAsync(Hash): " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public async Task<NonFungibleListAPIResult> GetNonFungibleTokensAsync(string AccountId, string Signature)
        {
            var result = new NonFungibleListAPIResult();
            if (!await VerifyClientAsync(AccountId, Signature))
            {
                result.ResultCode = APIResultCodes.APISignatureValidationFailed;
                return result;
            }

            try
            {
                if (!await NodeService.Dag.Storage.AccountExistsAsync(AccountId))
                    result.ResultCode = APIResultCodes.AccountDoesNotExist;

                var list = await NodeService.Dag.Storage.GetNonFungibleTokensAsync(AccountId);
                if (list != null)
                {
                    result.ListDataSerialized = Json(list);
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                    result.ResultCode = APIResultCodes.NoNonFungibleTokensFound;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetNonFungibleTokens: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public async Task<BlockAPIResult> GetTokenGenesisBlockAsync(string AccountId, string TokenTicker, string Signature)
        {
            var result = new BlockAPIResult();
            //if (!await VerifyClientAsync(AccountId, Signature))
            //{
            //    result.ResultCode = APIResultCodes.APISignatureValidationFailed;
            //    return result;
            //}

            try
            {
                //if (!NodeService.Dag.Storage.AccountExists(AccountId))
                //    result.ResultCode = APIResultCodes.AccountDoesNotExist;

                var block = await NodeService.Dag.Storage.FindTokenGenesisBlockAsync(null, TokenTicker);
                if (block != null)
                {
                    result.BlockData = Json(block);
                    result.ResultBlockType = block.BlockType;
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                    result.ResultCode = APIResultCodes.TokenGenesisBlockNotFound;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetTokenTokenGenesisBlock: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public async Task<BlockAPIResult> GetLastServiceBlockAsync()
        {
            var result = new BlockAPIResult();

            try
            {
                if (NodeService.Dag == null)
                    throw new Exception();

                var block = await NodeService.Dag?.Storage?.GetLastServiceBlockAsync();
                if (block != null)
                {
                    result.BlockData = Json(block);
                    result.ResultBlockType = block.BlockType;
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                    result.ResultCode = APIResultCodes.ServiceBlockNotFound;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetLastServiceBlock: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public async Task<BlockAPIResult> GetLastConsolidationBlockAsync()
        {
            var result = new BlockAPIResult();

            try
            {
                var block = await NodeService.Dag?.Storage.GetLastConsolidationBlockAsync();
                if (block != null)
                {
                    result.BlockData = Json(block);
                    result.ResultBlockType = block.BlockType;
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                    result.ResultCode = APIResultCodes.BlockNotFound;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetLastConsolidationBlock: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public async Task<MultiBlockAPIResult> GetBlocksByConsolidationAsync(string AccountId, string Signature, string consolidationHash)
        {
            var result = new MultiBlockAPIResult();
            //if (!await VerifyClientAsync(AccountId, Signature))
            //{
            //    result.ResultCode = APIResultCodes.APISignatureValidationFailed;
            //    return result;
            //}

            var consBlock = (await NodeService.Dag.Storage.FindBlockByHashAsync(consolidationHash)) as ConsolidationBlock;
            if(consBlock == null)
            {
                result.ResultCode = APIResultCodes.BlockNotFound;
                return result;
            }

            var mt = new MerkleTree();
            var blocks = new Block[consBlock.blockHashes.Count];
            for (int i = 0; i < blocks.Length; i++)
            {
                blocks[i] = await NodeService.Dag.Storage.FindBlockByHashAsync(consBlock.blockHashes[i]);
                mt.AppendLeaf(MerkleHash.Create(blocks[i].Hash));
            }
            var merkelTreeHash = mt.BuildTree().ToString();

            if(consBlock.MerkelTreeHash == merkelTreeHash)
            {
                result.SetBlocks(blocks);
                result.ResultCode = APIResultCodes.Success;
                return result;
            }
            else
            {
                // never replicate error data
                result.ResultCode = APIResultCodes.BlockValidationFailed;
                return result;
            }            
        }

        public async Task<MultiBlockAPIResult> GetBlocksByTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            var result = new MultiBlockAPIResult();
            try
            {
                var blocks = await NodeService.Dag.Storage.GetBlocksByTimeRangeAsync(startTime, endTime);
                if (blocks != null)
                {
                    result.SetBlocks(blocks.ToArray());
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                    result.ResultCode = APIResultCodes.BlockNotFound;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetBlocksByTimeRange: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public async Task<GetListStringAPIResult> GetBlockHashesByTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            var result = new GetListStringAPIResult();
            try
            {
                var blocks = await NodeService.Dag.Storage.GetBlockHashesByTimeRangeAsync(startTime, endTime);
                if (blocks != null)
                {
                    result.Entities = blocks.ToList();
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                    result.ResultCode = APIResultCodes.BlockNotFound;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetBlockHashesByTimeRange: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public async Task<TransactionsAPIResult> SearchTransactionsAsync(string accountId, long startTimeTicks, long endTimeTicks, int count)
        {
            var result = new TransactionsAPIResult();
            try
            {
                var blocks = await NodeService.Dag.Storage.SearchTransactionsAsync(accountId, new DateTime(startTimeTicks, DateTimeKind.Utc), new DateTime(endTimeTicks, DateTimeKind.Utc), count);
                if (blocks != null)
                {
                    result.Transactions = blocks;
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                    result.ResultCode = APIResultCodes.BlockNotFound;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in SearchTransactions: " + e.ToString());
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public async Task<MultiBlockAPIResult> GetBlocksByTimeRangeAsync(long startTimeTicks, long endTimeTicks)
        {
            var result = new MultiBlockAPIResult();
            try
            {
                var blocks = await NodeService.Dag.Storage.GetBlocksByTimeRangeAsync(new DateTime(startTimeTicks, DateTimeKind.Utc), new DateTime(endTimeTicks, DateTimeKind.Utc));
                if (blocks != null)
                {
                    result.SetBlocks(blocks.ToArray());
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                    result.ResultCode = APIResultCodes.BlockNotFound;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetBlocksByTimeRange: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public async Task<GetListStringAPIResult> GetBlockHashesByTimeRangeAsync(long startTimeTicks, long endTimeTicks)
        {
            var result = new GetListStringAPIResult();
            try
            {
                var blocks = await NodeService.Dag.Storage.GetBlockHashesByTimeRangeAsync(new DateTime(startTimeTicks, DateTimeKind.Utc), new DateTime(endTimeTicks, DateTimeKind.Utc));
                if (blocks != null)
                {
                    result.Entities = blocks.ToList();
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                    result.ResultCode = APIResultCodes.BlockNotFound;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetBlockHashesByTimeRange: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public async Task<MultiBlockAPIResult> GetConsolidationBlocksAsync(string AccountId, string? Signature, long startHeight, int count)
        {
            var result = new MultiBlockAPIResult();
            //if (!await VerifyClientAsync(AccountId, Signature))
            //{
            //    result.ResultCode = APIResultCodes.APISignatureValidationFailed;
            //    return result;
            //}

            try
            {
                var blocks = await NodeService.Dag.Storage.GetConsolidationBlocksAsync(startHeight, count);
                if (blocks != null)
                {
                    result.SetBlocks(blocks.ToArray());
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                    result.ResultCode = APIResultCodes.BlockNotFound;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetConsolidationBlocks: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        //public async Task<GetListStringAPIResult> GetUnConsolidatedBlocks(string AccountId, string Signature)
        //{
        //    var result = new GetListStringAPIResult();
        //    if (!await VerifyClientAsync(AccountId, Signature))
        //    {
        //        result.ResultCode = APIResultCodes.APISignatureValidationFailed;
        //        return result;
        //    }

        //    try
        //    {
        //        var blocks = await NodeService.Dag.Storage.GetAllUnConsolidatedBlockHashesAsync();
        //        if (blocks != null)
        //        {
        //            result.Entities = blocks.ToList();
        //            result.ResultCode = APIResultCodes.Success;
        //        }
        //        else
        //            result.ResultCode = APIResultCodes.BlockNotFound;
        //    }
        //    catch (Exception e)
        //    {
        //        Console.WriteLine("Exception in GetUnConsolidatedBlocks: " + e.Message);
        //        result.ResultCode = APIResultCodes.StorageAPIFailure;
        //    }

        //    return result;
        //}

        public async Task<NewTransferAPIResult> LookForNewTransferAsync(string AccountId, string Signature)
        {
            NewTransferAPIResult transfer_info = new NewTransferAPIResult();
            //if (!await VerifyClientAsync(AccountId, Signature))
            //{
            //    transfer_info.ResultCode = APIResultCodes.APISignatureValidationFailed;
            //    return transfer_info;
            //}
            try
            {
                if (await NodeService.Dag.Storage.WasAccountImportedAsync(AccountId))
                {
                    transfer_info.ResultCode = APIResultCodes.AccountAlreadyImported;
                    return transfer_info;
                }

                SendTransferBlock sendBlock = await NodeService.Dag.Storage.FindUnsettledSendBlockAsync(AccountId);

                if (sendBlock != null)
                {
                    TransactionBlock previousBlock = await NodeService.Dag.Storage.FindBlockByHashAsync(sendBlock.PreviousHash) as TransactionBlock;
                    if (previousBlock == null)
                        transfer_info.ResultCode = APIResultCodes.CouldNotTraceSendBlockChain;
                    else
                    {
                        transfer_info.Transfer = sendBlock.GetTransaction(previousBlock); //CalculateTransaction(sendBlock, previousSendBlock);
                        transfer_info.SourceHash = sendBlock.Hash;
                        transfer_info.NonFungibleToken = sendBlock.NonFungibleToken;
                        transfer_info.ResultCode = APIResultCodes.Success;
                    }
                }
                else
                    transfer_info.ResultCode = APIResultCodes.NoNewTransferFound;
            }
            catch (Exception e)
            {
                transfer_info.ResultCode = APIResultCodes.StorageAPIFailure;
                transfer_info.ResultMessage = e.ToString();
            }
            return transfer_info;
        }

        public async Task<NewTransferAPIResult2> LookForNewTransfer2Async(string AccountId, string Signature)
        {
            NewTransferAPIResult2 transfer_info = new NewTransferAPIResult2();
            //if (!await VerifyClientAsync(AccountId, Signature))
            //{
            //    transfer_info.ResultCode = APIResultCodes.APISignatureValidationFailed;
            //    return transfer_info;
            //}
            try
            {
                if (await NodeService.Dag.Storage.WasAccountImportedAsync(AccountId))
                {
                    transfer_info.ResultCode = APIResultCodes.AccountAlreadyImported;
                    return transfer_info;
                }

                SendTransferBlock sendBlock = await NodeService.Dag.Storage.FindUnsettledSendBlockAsync(AccountId);

                if (sendBlock != null)
                {
                    TransactionBlock previousBlock = await NodeService.Dag.Storage.FindBlockByHashAsync(sendBlock.PreviousHash) as TransactionBlock;
                    if (previousBlock == null)
                        transfer_info.ResultCode = APIResultCodes.CouldNotTraceSendBlockChain;
                    else
                    {
                        transfer_info.Transfer = sendBlock.GetBalanceChanges(previousBlock); //CalculateTransaction(sendBlock, previousSendBlock);
                        transfer_info.SourceHash = sendBlock.Hash;
                        transfer_info.NonFungibleToken = sendBlock.NonFungibleToken;
                        transfer_info.ResultCode = APIResultCodes.Success;
                    }
                }
                else
                    transfer_info.ResultCode = APIResultCodes.NoNewTransferFound;
            }
            catch (Exception e)
            {
                transfer_info.ResultCode = APIResultCodes.StorageAPIFailure;
                transfer_info.ResultMessage = e.ToString();
            }
            return transfer_info;
        }

        public async Task<NewFeesAPIResult> LookForNewFeesAsync(string AccountId, string Signature)
        {
            NewFeesAPIResult fbs = new NewFeesAPIResult();
            if (!await VerifyClientAsync(AccountId, Signature))
            {
                fbs.ResultCode = APIResultCodes.APISignatureValidationFailed;
                return fbs;
            }

            try
            {
                var pfts = await NodeService.Dag.Storage.FindAllProfitingAccountForOwnerAsync(AccountId);
                var pft = pfts.First();
                fbs.pendingFees = await NodeService.Dag.Storage.FindUnsettledFeesAsync(AccountId, pft.AccountID);
                fbs.ResultCode = APIResultCodes.Success;
                return fbs;
            }
            catch (Exception ex)
            {
                fbs.ResultCode = APIResultCodes.StorageAPIFailure;
                fbs.ResultMessage = ex.Message;
                return fbs;
            }
        }


        public List<Voter> GetVoters(VoteQueryModel model)
        {
            return NodeService.Dag.Storage.GetVoters(model.posAccountIds, model.endTime);
        }

        public List<Vote> FindVotes(VoteQueryModel model)
        {
            return NodeService.Dag.Storage.FindVotes(model.posAccountIds, model.endTime);
        }

        public Task<FeeStats> GetFeeStatsAsync()
        {
            return NodeService.Dag.Storage.GetFeeStatsAsync();
        }

        // util 
        private T FromJson<T>(string json)
        {
                return JsonConvert.DeserializeObject<T>(json);
        }
        private string Json(object o)
        {
            return JsonConvert.SerializeObject(o);
        }

        #region Reward trade methods
        /*
        public async Task<ActiveTradeOrdersAPIResult> GetActiveTradeOrdersAsync(string AccountId, string SellToken, string BuyToken, TradeOrderListTypes OrderType, string Signature)
        {
            var result = new ActiveTradeOrdersAPIResult();

            try
            {
                if (! await NodeService.Dag.Storage.AccountExistsAsync(AccountId))
                    result.ResultCode = APIResultCodes.AccountDoesNotExist;

                if (!await VerifyClientAsync(AccountId, Signature))
                {
                    result.ResultCode = APIResultCodes.APISignatureValidationFailed;
                    return result;
                }

                var list = await NodeService.Dag.TradeEngine.GetActiveTradeOrdersAsync(SellToken, BuyToken, OrderType);
                if (list != null && list.Count > 0)
                {
                    result.SetList(list);
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                    result.ResultCode = APIResultCodes.NoTradesFound;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetActiveTradeOrders: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public async Task<TradeAPIResult> LookForNewTradeAsync(string AccountId, string BuyTokenCode, string SellTokenCode, string Signature)
        {
            var result = new TradeAPIResult();
            try
            {
                if (!await NodeService.Dag.Storage.AccountExistsAsync(AccountId))
                    result.ResultCode = APIResultCodes.AccountDoesNotExist;

                if (!await VerifyClientAsync(AccountId, Signature))
                {
                    result.ResultCode = APIResultCodes.APISignatureValidationFailed;
                    return result;
                }

                var trade = NodeService.Dag.Storage.FindUnexecutedTrade(AccountId, BuyTokenCode, SellTokenCode);

                if (trade != null)
                {
                    result.SetBlock(trade);
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                    result.ResultCode = APIResultCodes.NoTradesFound;
            }
            catch (Exception e)
            {
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }
            return result;
        }*/
        #endregion

        #region pool
        public async Task<PoolInfoAPIResult> GetPoolAsync(string token0, string token1)
        {
            var result = new PoolInfoAPIResult();
            try
            {
                var factory = await NodeService.Dag.Storage.GetPoolFactoryAsync();
                if (factory == null)
                {
                    NodeService.Dag.Consensus.Tell(new ConsensusService.ReqCreatePoolFactory());
                    throw new Exception("pool factory not ready.");
                }
                else
                {
                    result.PoolFactoryAccountId = factory.AccountID;                    
                    var poolGenesis = await NodeService.Dag.Storage.GetPoolAsync(token0, token1);                    

                    if (poolGenesis != null)
                    {
                        result.PoolAccountId = poolGenesis.AccountID;
                        result.Token0 = poolGenesis.Token0;
                        result.Token1 = poolGenesis.Token1;

                        var latestPoolBlock = await NodeService.Dag.Storage.FindLatestBlockAsync(poolGenesis.AccountID) as TransactionBlock;
                        result.SetBlock(latestPoolBlock);
                        //if(latestPoolBlock.Balances?.Any() == true && latestPoolBlock.Balances[poolGenesis.Token1] > 0)
                        //{
                        //    result.SwapRito = (latestPoolBlock.Balances[poolGenesis.Token0].ToBalanceDecimal() / latestPoolBlock.Balances[poolGenesis.Token1].ToBalanceDecimal()).ToBalanceLong();
                        //}
                        //else
                        //{
                        //    result.SwapRito = 0;
                        //}
                    }

                    result.ResultCode = APIResultCodes.Success;
                    return result;
                }                    
            }
            catch (Exception e)
            {
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }
            return result;
        }

        #endregion

        public async Task<MultiBlockAPIResult> GetAllBrokerAccountsForOwnerAsync(string ownerAccount)
        {
            var result = new MultiBlockAPIResult();
            try
            {
                var blocks = await NodeService.Dag.Storage.GetAllBrokerAccountsForOwnerAsync(ownerAccount);
                if (blocks != null)
                {
                    result.SetBlocks(blocks.ToArray());
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                    result.ResultCode = APIResultCodes.BlockNotFound;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetAllBrokerAccountsForOwner: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public Task<List<Profiting>> FindAllProfitingAccountsAsync(DateTime begin, DateTime end)
        {
            return NodeService.Dag.Storage.FindAllProfitingAccountsAsync(begin, end);
        }
        public Task<ProfitingGenesis> FindProfitingAccountsByNameAsync(string Name)
        {
            return Task.FromResult(NodeService.Dag.Storage.FindProfitingAccountsByName(Name));
        }
        public List<Staker> FindAllStakings(string pftid, DateTime timeBefore)
        {
            return NodeService.Dag.Storage.FindAllStakings(pftid, timeBefore);
        }

        public async Task<SimpleJsonAPIResult> FindAllStakingsAsync(string pftid, DateTime timeBefore)
        {
            var result = new SimpleJsonAPIResult();

            try
            {
                var blocks = NodeService.Dag.Storage.FindAllStakings(pftid, timeBefore);
                if (blocks == null)
                {
                    result.ResultCode = APIResultCodes.BlockNotFound;
                }
                else
                {
                    result.JsonString = Json(blocks);
                    result.ResultCode = APIResultCodes.Success;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in FindAllStakings: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public Task<ProfitingStats> GetAccountStatsAsync(string accountId, DateTime begin, DateTime end)
        {
            return NodeService.Dag.Storage.GetAccountStatsAsync(accountId, begin, end);
        }

        public Task<ProfitingStats> GetBenefitStatsAsync(string pftid, string stkid, DateTime begin, DateTime end)
        {
            return NodeService.Dag.Storage.GetBenefitStatsAsync(pftid, stkid, begin, end);
        }
        public Task<PendingStats> GetPendingStatsAsync(string accountId)
        {
            return NodeService.Dag.Storage.GetPendingStatsAsync(accountId);
        }

        public async Task<MultiBlockAPIResult> GetAllDexWalletsAsync(string owner)
        {
            var result = new MultiBlockAPIResult();

            try
            {
                var blocks = await NodeService.Dag.Storage.GetAllDexWalletsAsync(owner);
                if (blocks == null)
                {
                    result.ResultCode = APIResultCodes.BlockNotFound;
                    return result;
                }

                result.SetBlocks(blocks.ToArray());
                result.ResultCode = APIResultCodes.Success;
                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetAllDexWalletsAsync(Hash): " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }
        public async Task<BlockAPIResult> FindDexWalletAsync(string owner, string symbol, string provider)
        {
            var result = new BlockAPIResult();

            try
            {
                var block = await NodeService.Dag?.Storage.FindDexWalletAsync(owner, symbol, provider);
                if (block != null)
                {
                    result.BlockData = Json(block);
                    result.ResultBlockType = block.BlockType;
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                    result.ResultCode = APIResultCodes.BlockNotFound;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in FindDexWalletAsync: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public async Task<MultiBlockAPIResult> GetAllDaosAsync(int page, int pageSize)
        {
            var result = new MultiBlockAPIResult();

            try
            {
                var blocks = await NodeService.Dag.Storage.GetAllDaosAsync(page, pageSize);
                if (blocks == null)
                {
                    result.ResultCode = APIResultCodes.BlockNotFound;
                }
                else
                {
                    result.SetBlocks(blocks.Cast<Block>().ToArray());
                    result.ResultCode = APIResultCodes.Success;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetAllDaosAsync: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public Task<BlockAPIResult> GetDaoByNameAsync(string name)
        {
            var result = new BlockAPIResult();

            try
            {
                var block = NodeService.Dag?.Storage.GetDaoByName(name);
                if (block != null)
                {
                    result.BlockData = Json(block);
                    result.ResultBlockType = block.BlockType;
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                    result.ResultCode = APIResultCodes.BlockNotFound;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetDaoByNameAsync: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return Task.FromResult(result);
        }

        public async Task<MultiBlockAPIResult> GetOtcOrdersByOwnerAsync(string accountId)
        {
            var result = new MultiBlockAPIResult();

            try
            {
                var blocks = await NodeService.Dag.Storage.GetOtcOrdersByOwnerAsync(accountId);
                if (blocks == null)
                {
                    result.ResultCode = APIResultCodes.BlockNotFound;
                }
                else
                {
                    result.SetBlocks(blocks.Cast<Block>().ToArray());
                    result.ResultCode = APIResultCodes.Success;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetDaoByNameAsync: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public async Task<ContainerAPIResult> FindTradableOtcAsync()
        {
            var result = new ContainerAPIResult();

            try
            {
                var dict = await NodeService.Dag.Storage.FindTradableOtcAsync();
                foreach (var kvp in dict)
                    result.AddBlocks(kvp.Key, kvp.Value.Cast<Block>().ToArray());

                result.ResultCode = APIResultCodes.Success;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in FindTradableOtcAsync: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public async Task<MultiBlockAPIResult> FindOtcTradeAsync(string accountId, bool onlyOpenTrade, int page, int pageSize)
        {
            var result = new MultiBlockAPIResult();

            try
            {
                var blocks = await NodeService.Dag.Storage.FindOtcTradeAsync(accountId, onlyOpenTrade, page, pageSize);
                if (blocks == null)
                {
                    result.ResultCode = APIResultCodes.BlockNotFound;
                }
                else
                {
                    result.SetBlocks(blocks.Cast<Block>().ToArray());
                    result.ResultCode = APIResultCodes.Success;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in FindOtcTradeAsync: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public async Task<MultiBlockAPIResult> FindOtcTradeByStatusAsync(string daoid, OTCTradeStatus status, int page, int pageSize)
        {
            var result = new MultiBlockAPIResult();

            try
            {
                var blocks = await NodeService.Dag.Storage.FindOtcTradeByStatusAsync(daoid, status, page, pageSize);
                if (blocks == null)
                {
                    result.ResultCode = APIResultCodes.BlockNotFound;
                }
                else
                {
                    result.SetBlocks(blocks.Cast<Block>().ToArray());
                    result.ResultCode = APIResultCodes.Success;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in FindOtcTradeByStatusAsync: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public async Task<SimpleJsonAPIResult> GetOtcTradeStatsForUsersAsync(TradeStatsReq req)
        {
            var result = new SimpleJsonAPIResult();

            try
            {
                var stats = await NodeService.Dag.Storage.GetOtcTradeStatsForUsersAsync(req.AccountIDs);

                result.JsonString = Json(stats);
                result.ResultCode = APIResultCodes.Success;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetOtcTradeStatsForUsersAsync: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public async Task<MultiBlockAPIResult> FindAllVotesByDaoAsync(string daoid, bool openOnly)
        {
            var result = new MultiBlockAPIResult();

            try
            {
                var blocks = await NodeService.Dag.Storage.FindAllVotesByDaoAsync(daoid, openOnly);
                if (blocks == null)
                {
                    result.ResultCode = APIResultCodes.BlockNotFound;
                }
                else
                {
                    result.SetBlocks(blocks.ToArray());
                    result.ResultCode = APIResultCodes.Success;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in FindAllVotesByDaoAsync: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public async Task<MultiBlockAPIResult> FindAllVoteForTradeAsync(string tradeid)
        {
            var result = new MultiBlockAPIResult();

            try
            {
                var blocks = await NodeService.Dag.Storage.FindAllVoteForTradeAsync(tradeid);
                if (blocks == null)
                {
                    result.ResultCode = APIResultCodes.BlockNotFound;
                }
                else
                {
                    result.SetBlocks(blocks.ToArray());
                    result.ResultCode = APIResultCodes.Success;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in FindAllVoteForTradeAsync: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public async Task<SimpleJsonAPIResult> GetVoteSummaryAsync(string voteid)
        {
            var result = new SimpleJsonAPIResult();

            try
            {
                var blocks = await NodeService.Dag.Storage.GetVoteSummaryAsync(voteid);
                if (blocks == null)
                {
                    result.ResultCode = APIResultCodes.BlockNotFound;
                }
                else
                {
                    result.JsonString = Json(blocks);
                    result.ResultCode = APIResultCodes.Success;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetVoteSummaryAsync: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public async Task<BlockAPIResult> FindExecForVoteAsync(string voteid)
        {
            var result = new BlockAPIResult();

            try
            {
                var block = await NodeService.Dag?.Storage.FindExecForVoteAsync(voteid);
                if (block != null)
                {
                    result.SetBlock(block);
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                    result.ResultCode = APIResultCodes.BlockNotFound;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in FindExecForVoteAsync: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return result;
        }

        public Task<BlockAPIResult> GetDealerByAccountIdAsync(string accountId)
        {
            var result = new BlockAPIResult();

            try
            {
                var block = NodeService.Dag?.Storage.GetDealerByAccountId(accountId);
                if (block != null)
                {
                    result.BlockData = Json(block);
                    result.ResultBlockType = block.BlockType;
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                    result.ResultCode = APIResultCodes.BlockNotFound;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetDealerByAccountIdAsync: " + e.Message);
                result.ResultCode = APIResultCodes.StorageAPIFailure;
                result.ResultMessage = e.ToString();
            }

            return Task.FromResult(result);
        }
    }
}
