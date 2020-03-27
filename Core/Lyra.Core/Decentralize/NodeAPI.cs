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

namespace Lyra.Core.Decentralize
{
    public class NodeAPI : INodeAPI
    {
        LyraRestClient _seed0Client;
        public NodeAPI()
        {
        }

        public async Task<LyraRestClient> GetClientForSeed0()
        {
            if (_seed0Client == null)
            {
                var addr = ProtocolSettings.Default.SeedList[0].Split(':')[0];
                var apiUrl = $"https://{addr}:4505/api/LyraNode/";
                _seed0Client = await LyraRestClient.CreateAsync(BlockChain.Singleton.NetworkID, Environment.OSVersion.Platform.ToString(), "LyraNode2", "1.0", apiUrl);

            }
            return _seed0Client;
        }

        private async Task<bool> VerifyClientAsync(string accountId, string signature)
        {
            // seeds accountid not exists.
            //if (!await BlockChain.Singleton.AccountExistsAsync(accountId))
            //    return false;

            var lastSvcBlock = await BlockChain.Singleton.GetLastServiceBlockAsync();
            if (lastSvcBlock == null)
                return false;

            return Signatures.VerifyAccountSignature(lastSvcBlock.Hash, accountId, signature);
        }

        public async Task<GetSyncStateAPIResult> GetSyncState()
        {
            var consBlock = await BlockChain.Singleton.GetLastConsolidationBlockAsync();
            var result = new GetSyncStateAPIResult
            {
                ResultCode = APIResultCodes.Success,
                NetworkID = BlockChain.Singleton.NetworkID,
                SyncState = BlockChain.Singleton.CurrentState == BlockChainState.Almighty ? ConsensusWorkingMode.Normal : ConsensusWorkingMode.OutofSyncWaiting,
                LastConsolidationHash = consBlock == null ? null : consBlock.Hash,
                //NewestBlockUIndex = await BlockChain.Singleton.GetNewestBlockUIndexAsync(),
                Status = await BlockChain.Singleton.GetNodeStatusAsync()
            };
            return result;
        }

        //public async Task<BlockAPIResult> GetBlockByUIndex(long uindex)
        //{
        //    BlockAPIResult result;
        //    var block = await BlockChain.Singleton.GetBlockByUIndexAsync(uindex);
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

        public async Task<AccountHeightAPIResult> GetSyncHeight()
        {
            var result = new AccountHeightAPIResult();
            try
            {
                var last_svc_block = await BlockChain.Singleton.GetLastServiceBlockAsync();
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
                //if (!BlockChain.Singleton.AccountExists(AccountId))
                //    result.ResultCode = APIResultCodes.AccountDoesNotExist;

                var blocks = await BlockChain.Singleton.FindTokenGenesisBlocksAsync(keyword == "(null)" ? null : keyword);
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

        public async Task<AccountHeightAPIResult> GetAccountHeight(string AccountId, string Signature)
        {
            var result = new AccountHeightAPIResult();
            if (!await VerifyClientAsync(AccountId, Signature))
            {
                result.ResultCode = APIResultCodes.APISignatureValidationFailed;
                return result;
            }
            try
            {
                if (await BlockChain.Singleton.AccountExistsAsync(AccountId))
                {
                    result.Height = (await BlockChain.Singleton.FindLatestBlockAsync(AccountId)).Height;
                    result.NetworkId = Neo.Settings.Default.LyraNode.Lyra.NetworkId;
                    result.SyncHash = (await BlockChain.Singleton.GetLastConsolidationBlockAsync()).Hash;
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

        public async Task<BlockAPIResult> GetBlockByIndex(string AccountId, long Index, string Signature)
        {
            var result = new BlockAPIResult();
            if (!await VerifyClientAsync(AccountId, Signature))
            {
                result.ResultCode = APIResultCodes.APISignatureValidationFailed;
                return result;
            }

            try
            {
                if (await BlockChain.Singleton.AccountExistsAsync(AccountId))
                {
                    var block = await BlockChain.Singleton.FindBlockByIndexAsync(AccountId, Index);
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
                if (!await BlockChain.Singleton.AccountExistsAsync(AccountId))
                    result.ResultCode = APIResultCodes.AccountDoesNotExist;

                var block = await BlockChain.Singleton.FindBlockByHashAsync(Hash);
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
                if (!await BlockChain.Singleton.AccountExistsAsync(AccountId))
                    result.ResultCode = APIResultCodes.AccountDoesNotExist;

                var list = await BlockChain.Singleton.GetNonFungibleTokensAsync(AccountId);
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
                //if (!BlockChain.Singleton.AccountExists(AccountId))
                //    result.ResultCode = APIResultCodes.AccountDoesNotExist;

                var block = await BlockChain.Singleton.FindTokenGenesisBlockAsync(null, TokenTicker);
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

        public async Task<BlockAPIResult> GetLastServiceBlock(string AccountId, string Signature)
        {
            var result = new BlockAPIResult();
            if (!await VerifyClientAsync(AccountId, Signature))
            {
                result.ResultCode = APIResultCodes.APISignatureValidationFailed;
                return result;
            }

            try
            {
                if (!await BlockChain.Singleton.AccountExistsAsync(AccountId))
                    result.ResultCode = APIResultCodes.AccountDoesNotExist;

                var block = await BlockChain.Singleton.GetLastServiceBlockAsync();
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

        public async Task<BlockAPIResult> GetLastConsolidationBlock(string AccountId, string Signature)
        {
            var result = new BlockAPIResult();
            if (!await VerifyClientAsync(AccountId, Signature))
            {
                result.ResultCode = APIResultCodes.APISignatureValidationFailed;
                return result;
            }

            try
            {
                var block = await BlockChain.Singleton.GetLastConsolidationBlockAsync();
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

            var consBlock = (await BlockChain.Singleton.FindBlockByHashAsync(consolidationHash)) as ConsolidationBlock;
            if(consBlock == null)
            {
                result.ResultCode = APIResultCodes.BlockNotFound;
                return result;
            }

            var mt = new MerkleTree();
            var blocks = new Block[consBlock.blockHashes.Count];
            for (int i = 0; i < blocks.Length; i++)
            {
                blocks[i] = await BlockChain.Singleton.FindBlockByHashAsync(consBlock.blockHashes[i]);
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
                var blocks = await BlockChain.Singleton.GetConsolidationBlocksAsync(startHeight);
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

        public async Task<GetListStringAPIResult> GetUnConsolidatedBlocks(string AccountId, string Signature)
        {
            var result = new GetListStringAPIResult();
            if (!await VerifyClientAsync(AccountId, Signature))
            {
                result.ResultCode = APIResultCodes.APISignatureValidationFailed;
                return result;
            }

            try
            {
                var blocks = await BlockChain.Singleton.GetAllUnConsolidatedBlocksAsync();
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
                Console.WriteLine("Exception in GetUnConsolidatedBlocks: " + e.Message);
                result.ResultCode = APIResultCodes.UnknownError;
            }

            return result;
        }

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
                SendTransferBlock sendBlock = await BlockChain.Singleton.FindUnsettledSendBlockAsync(AccountId);

                if (sendBlock != null)
                {
                    TransactionBlock previousBlock = await BlockChain.Singleton.FindBlockByHashAsync(sendBlock.PreviousHash) as TransactionBlock;
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

        // util 
        private T FromJson<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
        private string Json(object o)
        {
            return JsonConvert.SerializeObject(o);
        }
    }
}
