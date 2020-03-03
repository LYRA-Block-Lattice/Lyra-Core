using Lyra.Core.API;
using Lyra.Core.Blocks;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;
using Lyra.Core.Utils;
using Lyra.Core.Accounts;

namespace Lyra.Core.Decentralize
{
    public class NodeAPI : INodeAPI
    {
        public NodeAPI()
        {
        }

        public async Task<GetSyncStateAPIResult> GetSyncState()
        {
            var result = new GetSyncStateAPIResult
            {
                ResultCode = APIResultCodes.Success,
                NetworkID = BlockChain.Singleton.NetworkID,
                SyncState = BlockChain.Singleton.InSyncing ? ConsensusWorkingMode.OutofSyncWaiting : ConsensusWorkingMode.Normal,
                NewestBlockUIndex = await BlockChain.Singleton.GetNewestBlockUIndexAsync(),
                Status = await BlockChain.Singleton.GetNodeStatusAsync()
            };
            return result;
        }

        public async Task<BlockAPIResult> GetBlockByUIndex(long uindex)
        {
            BlockAPIResult result;
            var block = await BlockChain.Singleton.GetBlockByUIndexAsync(uindex);
            if(block == null)
            {
                result = new BlockAPIResult { ResultCode = APIResultCodes.BlockNotFound };
            }
            else
            {
                result = new BlockAPIResult
                {
                    BlockData = Json(block),
                    ResultBlockType = block.BlockType,
                    ResultCode = APIResultCodes.Success
                };
            }

            return result;
        }

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
                var last_sync_block = await BlockChain.Singleton.GetLastConsolidationBlockAsync();
                if(last_sync_block == null)
                {
                    // empty database. 
                    throw new Exception("Database empty.");
                }
                result.Height = last_sync_block.Index;
                result.SyncHash = last_sync_block.Hash;
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

        public async Task<GetTokenNamesAPIResult> GetTokenNames(string AccountId, string Signature, string keyword)
        {
            var result = new GetTokenNamesAPIResult();

            try
            {
                //if (!BlockChain.Singleton.AccountExists(AccountId))
                //    result.ResultCode = APIResultCodes.AccountDoesNotExist;

                var blocks = await BlockChain.Singleton.FindTokenGenesisBlocksAsync(keyword == "(null)" ? null : keyword);
                if (blocks != null)
                {
                    result.TokenNames = blocks.Select(a => a.Ticker).ToList();
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
            try
            {
                if (await BlockChain.Singleton.AccountExistsAsync(AccountId))
                {
                    result.Height = (await BlockChain.Singleton.FindLatestBlockAsync(AccountId)).Index;
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

            try
            {
                if (!await BlockChain.Singleton.AccountExistsAsync(AccountId))
                    result.ResultCode = APIResultCodes.AccountDoesNotExist;

                var block = await BlockChain.Singleton.FindBlockByHashAsync(AccountId, Hash);
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

        public async Task<NewTransferAPIResult> LookForNewTransfer(string AccountId, string Signature)
        {
            NewTransferAPIResult transfer_info = new NewTransferAPIResult();
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
