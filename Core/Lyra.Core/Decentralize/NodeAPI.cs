using Lyra.Core.API;
using Lyra.Core.Blocks;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;
using Lyra.Core.Utils;
using Lyra.Core.Accounts;
using Lyra.Core.Cryptography;
using Neo;
using System.Collections.Generic;
using Lyra.Core.Authorizers;
using Clifton.Blockchain;
using Akka.Actor;

namespace Lyra.Core.Decentralize
{
    public class NodeAPI : INodeAPI
    {
        LyraRestClient _seed0Client;
        public NodeAPI()
        {
        }

        public LyraRestClient GetClientForSeed0()
        {
            if (_seed0Client == null)
            {
                var addr = ProtocolSettings.Default.SeedList[0].Split(':')[0];
                var apiUrl = $"http://{addr}:4505/api/Node/";
                _seed0Client = LyraRestClient.Create(LyraNodeConfig.GetNetworkId(), Environment.OSVersion.Platform.ToString(), "LyraNode2", "1.0", apiUrl);

            }
            return _seed0Client;
        }

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

        public async Task<GetSyncStateAPIResult> GetSyncState()
        {
            if(NodeService.Dag == null || NodeService.Dag.Storage == null)
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
                //NewestBlockUIndex = await NodeService.Dag.Storage.GetNewestBlockUIndexAsync(),
                Status = chainStatus
            };
            return result;
        }

        //public async Task<BlockAPIResult> GetBlockByUIndex(long uindex)
        //{
        //    BlockAPIResult result;
        //    var block = await NodeService.Dag.Storage.GetBlockByUIndexAsync(uindex);
        //    if(block == null)
        //    {
        //        result = new BlockAPIResult { ResultCode = APIResultCodes.BlockNotFound };
        //    }
        //    else
        //    {
        //        result = new BlockAPIResult
        //        {
        //            BlockData = Json(block),
        //            ResultBlockType = block.BlockType,
        //            ResultCode = APIResultCodes.Success
        //        };
        //    }

        //    return result;
        //}

        public Task<GetVersionAPIResult> GetVersion(int apiVersion, string appName, string appVersion)
        {
            var result = new GetVersionAPIResult()
            {
                ResultCode = APIResultCodes.Success,
                ApiVersion = LyraGlobal.ProtocolVersion,
                NodeVersion = LyraGlobal.NodeAppName,
                UpgradeNeeded = false,
                MustUpgradeToConnect = apiVersion < LyraGlobal.ProtocolVersion
            };
            return Task.FromResult(result);
        }

        public async Task<BlockAPIResult> GetServiceGenesisBlock()
        {
            var result = new BlockAPIResult();

            try
            {
                var block = await NodeService.Dag.Storage.GetServiceGenesisBlock();
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
                result.ResultCode = APIResultCodes.UnknownError;
            }

            return result;
        }
        public async Task<BlockAPIResult> GetLyraTokenGenesisBlock()
        {
            var result = new BlockAPIResult();

            try
            {
                var block = await NodeService.Dag.Storage.GetLyraTokenGenesisBlock();
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
                result.ResultCode = APIResultCodes.UnknownError;
            }

            return result;
        }

        public async Task<AccountHeightAPIResult> GetSyncHeight()
        {
            var result = new AccountHeightAPIResult();
            try
            {
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
                result.ResultCode = APIResultCodes.UnknownError;
                result.ResultMessage = ex.Message;
            }
            return result;
        }

        public async Task<GetListStringAPIResult> GetTokenNames(string AccountId, string Signature, string keyword)
        {
            var result = new GetListStringAPIResult();
            if(!await VerifyClientAsync(AccountId, Signature))
            {
                result.ResultCode = APIResultCodes.APISignatureValidationFailed;
                return result;
            }

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
                result.ResultCode = APIResultCodes.UnknownError;
            }

            return result;
        }

        public async Task<AccountHeightAPIResult> GetAccountHeight(string AccountId)
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
            catch (Exception)
            {
                result.ResultCode = APIResultCodes.UnknownError;
            }
            return result;
        }

        public async Task<BlockAPIResult> GetLastBlock(string AccountId)
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
                result.ResultCode = APIResultCodes.UnknownError;
            }

            return result;
        }

        public async Task<BlockAPIResult> GetBlockByIndex(string AccountId, long Index)
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
                result.ResultCode = APIResultCodes.UnknownError;
            }

            return result;
        }

        public async Task<BlockAPIResult> GetServiceBlockByIndex(string blockType, long Index)
        {
            var result = new BlockAPIResult();

            try
            {
                var block = await NodeService.Dag.Storage.FindServiceBlockByIndexAsync(blockType, Index);

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
                result.ResultCode = APIResultCodes.UnknownError;
            }

            return result;
        }

        public async Task<BlockAPIResult> GetBlockByHash(string AccountId, string Hash, string Signature)
        {
            var result = new BlockAPIResult();
            if (!await VerifyClientAsync(AccountId, Signature))
            {
                result.ResultCode = APIResultCodes.APISignatureValidationFailed;
                return result;
            }

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
                result.ResultCode = APIResultCodes.UnknownError;
            }

            return result;
        }

        public async Task<BlockAPIResult> GetBlock(string Hash)
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
                result.ResultCode = APIResultCodes.UnknownError;
            }

            return result;
        }


        public async Task<NonFungibleListAPIResult> GetNonFungibleTokens(string AccountId, string Signature)
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
                result.ResultCode = APIResultCodes.UnknownError;
            }

            return result;
        }

        public async Task<BlockAPIResult> GetTokenGenesisBlock(string AccountId, string TokenTicker, string Signature)
        {
            var result = new BlockAPIResult();
            if (!await VerifyClientAsync(AccountId, Signature))
            {
                result.ResultCode = APIResultCodes.APISignatureValidationFailed;
                return result;
            }

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
                result.ResultCode = APIResultCodes.UnknownError;
            }

            return result;
        }

        public async Task<BlockAPIResult> GetLastServiceBlock()
        {
            var result = new BlockAPIResult();

            try
            {
                var block = await NodeService.Dag.Storage.GetLastServiceBlockAsync();
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
                result.ResultCode = APIResultCodes.UnknownError;
            }

            return result;
        }

        public async Task<BlockAPIResult> GetLastConsolidationBlock()
        {
            var result = new BlockAPIResult();

            try
            {
                var block = await NodeService.Dag.Storage.GetLastConsolidationBlockAsync();
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
                result.ResultCode = APIResultCodes.UnknownError;
            }

            return result;
        }

        public async Task<MultiBlockAPIResult> GetBlocksByConsolidation(string AccountId, string Signature, string consolidationHash)
        {
            var result = new MultiBlockAPIResult();
            if (!await VerifyClientAsync(AccountId, Signature))
            {
                result.ResultCode = APIResultCodes.APISignatureValidationFailed;
                return result;
            }

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

        public async Task<GetListStringAPIResult> GetBlockHashesByTimeRange(DateTime startTime, DateTime endTime)
        {
            var result = new GetListStringAPIResult();
            try
            {
                var blocks = await NodeService.Dag.Storage.GetBlockHashesByTimeRange(startTime, endTime);
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
                Console.WriteLine("Exception in GetBlocksByTimeRange: " + e.Message);
                result.ResultCode = APIResultCodes.UnknownError;
            }

            return result;
        }

        public async Task<MultiBlockAPIResult> GetConsolidationBlocks(string AccountId, string Signature, long startHeight)
        {
            var result = new MultiBlockAPIResult();
            if (!await VerifyClientAsync(AccountId, Signature))
            {
                result.ResultCode = APIResultCodes.APISignatureValidationFailed;
                return result;
            }

            try
            {
                var blocks = await NodeService.Dag.Storage.GetConsolidationBlocksAsync(startHeight);
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
                result.ResultCode = APIResultCodes.UnknownError;
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
        //        result.ResultCode = APIResultCodes.UnknownError;
        //    }

        //    return result;
        //}

        public async Task<NewTransferAPIResult> LookForNewTransfer(string AccountId, string Signature)
        {
            NewTransferAPIResult transfer_info = new NewTransferAPIResult();
            if (!await VerifyClientAsync(AccountId, Signature))
            {
                transfer_info.ResultCode = APIResultCodes.APISignatureValidationFailed;
                return transfer_info;
            }
            try
            {
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
            catch (Exception)
            {
                transfer_info.ResultCode = APIResultCodes.UnknownError;
            }
            return transfer_info;
        }

        public async Task<NewFeesAPIResult> LookForNewFees(string AccountId, string Signature)
        {
            NewFeesAPIResult fbs = new NewFeesAPIResult();
            if (!await VerifyClientAsync(AccountId, Signature))
            {
                fbs.pendingFeeBlocks = Enumerable.Empty<ServiceBlock>();
                fbs.ResultCode = APIResultCodes.APISignatureValidationFailed;
                return fbs;
            }

            try
            {
                fbs.pendingFeeBlocks = await NodeService.Dag.Storage.FindUnsettledFeeBlockAsync(AccountId);
                fbs.ResultCode = APIResultCodes.Success;
                return fbs;
            }
            catch (Exception ex)
            {
                fbs.ResultCode = APIResultCodes.UnknownError;
                fbs.ResultMessage = ex.Message;
                return fbs;
            }
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

        public async Task<ActiveTradeOrdersAPIResult> GetActiveTradeOrders(string AccountId, string SellToken, string BuyToken, TradeOrderListTypes OrderType, string Signature)
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

                var list = await NodeService.Dag.TradeEngine.GetActiveTradeOrders(SellToken, BuyToken, OrderType);
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
                result.ResultCode = APIResultCodes.UnknownError;
                result.ResultMessage = e.Message;
            }

            return result;
        }

        public async Task<TradeAPIResult> LookForNewTrade(string AccountId, string BuyTokenCode, string SellTokenCode, string Signature)
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
                result.ResultCode = APIResultCodes.UnknownError;
                result.ResultMessage = e.Message;
            }
            return result;
        }
        #endregion
    }
}
