using Lyra.Authorizer.Services;
using Lyra.Core.Accounts.Node;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Orleans;
using Orleans.Concurrency;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lyra.Core.Utils;

namespace Lyra.Authorizer.Decentralize
{
    //[StatelessWorker(100)]
    public class NodeAPI : Grain, INodeAPI
    {
        IAccountCollection _accountCollection;
        ServiceAccount _serviceAccount;
        private LyraNodeConfig _config;

        public NodeAPI(ServiceAccount serviceAccount,
            IAccountCollection accountCollection,
            IOptions<LyraNodeConfig> config)
        {
            _accountCollection = accountCollection;
            _serviceAccount = serviceAccount;
            _config = config.Value;
        }
        public Task<GetVersionAPIResult> GetVersion(int apiVersion, string appName, string appVersion)
        {
            var result = new GetVersionAPIResult()
            {
                ResultCode = APIResultCodes.Success,
                ApiVersion = LyraGlobal.APIVERSION,
                NodeVersion = LyraGlobal.NodeVersion,
                UpgradeNeeded = false,
                MustUpgradeToConnect = apiVersion < LyraGlobal.APIVERSION
            };
            return Task.FromResult(result);
        }

        public Task<AccountHeightAPIResult> GetSyncHeight()
        {
            var result = new AccountHeightAPIResult();
            try
            {
                var last_sync_block = _serviceAccount.GetLatestBlock();
                if(last_sync_block == null)
                {
                    // empty database. 
                    throw new Exception("Database empty.");
                }
                result.Height = last_sync_block.Index;
                result.SyncHash = last_sync_block.Hash;
                result.NetworkId = _config.Lyra.NetworkId;
                result.ResultCode = APIResultCodes.Success;
            }
            catch (Exception e)
            {
                result.ResultCode = APIResultCodes.UnknownError;
            }
            return Task.FromResult(result);
        }

        public Task<GetTokenNamesAPIResult> GetTokenNames(string AccountId, string Signature, string keyword)
        {
            var result = new GetTokenNamesAPIResult();

            try
            {
                //if (!_accountCollection.AccountExists(AccountId))
                //    result.ResultCode = APIResultCodes.AccountDoesNotExist;

                var blocks = _accountCollection.FindTokenGenesisBlocks(keyword == "(null)" ? null : keyword);
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

            return Task.FromResult(result);
        }

        public Task<AccountHeightAPIResult> GetAccountHeight(string AccountId, string Signature)
        {
            var result = new AccountHeightAPIResult();
            try
            {
                if (_accountCollection.AccountExists(AccountId))
                {
                    result.Height = _accountCollection.FindLatestBlock(AccountId).Index;
                    result.NetworkId = _config.Lyra.NetworkId;
                    result.SyncHash = _serviceAccount.GetLatestBlock().Hash;
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                {
                    result.ResultCode = APIResultCodes.AccountDoesNotExist;
                }
            }
            catch (Exception e)
            {
                result.ResultCode = APIResultCodes.UnknownError;
            }
            return Task.FromResult(result);
        }

        public Task<BlockAPIResult> GetBlockByIndex(string AccountId, long Index, string Signature)
        {
            var result = new BlockAPIResult();

            try
            {
                if (_accountCollection.AccountExists(AccountId))
                {
                    var block = _accountCollection.FindBlockByIndex(AccountId, Index);
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

            return Task.FromResult(result);
        }

        public Task<BlockAPIResult> GetBlockByHash(string AccountId, string Hash, string Signature)
        {
            var result = new BlockAPIResult();

            try
            {
                if (!_accountCollection.AccountExists(AccountId))
                    result.ResultCode = APIResultCodes.AccountDoesNotExist;

                var block = _accountCollection.FindBlockByHash(AccountId, Hash);
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

            return Task.FromResult(result);
        }

        public Task<NonFungibleListAPIResult> GetNonFungibleTokens(string AccountId, string Signature)
        {
            var result = new NonFungibleListAPIResult();

            try
            {
                if (!_accountCollection.AccountExists(AccountId))
                    result.ResultCode = APIResultCodes.AccountDoesNotExist;

                var list = _accountCollection.GetNonFungibleTokens(AccountId);
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

            return Task.FromResult(result);
        }

        public Task<BlockAPIResult> GetTokenGenesisBlock(string AccountId, string TokenTicker, string Signature)
        {
            var result = new BlockAPIResult();

            try
            {
                //if (!_accountCollection.AccountExists(AccountId))
                //    result.ResultCode = APIResultCodes.AccountDoesNotExist;

                var block = _accountCollection.FindTokenGenesisBlock(null, TokenTicker);
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

            return Task.FromResult(result);
        }

        public Task<BlockAPIResult> GetLastServiceBlock(string AccountId, string Signature)
        {
            var result = new BlockAPIResult();

            try
            {
                if (!_accountCollection.AccountExists(AccountId))
                    result.ResultCode = APIResultCodes.AccountDoesNotExist;

                var block = _serviceAccount.GetLastServiceBlock();
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

            return Task.FromResult(result);
        }

        public Task<NewTransferAPIResult> LookForNewTransfer(string AccountId, string Signature)
        {
            NewTransferAPIResult transfer_info = new NewTransferAPIResult();
            try
            {
                SendTransferBlock sendBlock = _accountCollection.FindUnsettledSendBlock(AccountId);

                if (sendBlock != null)
                {
                    TransactionBlock previousBlock = _accountCollection.FindBlockByHash(sendBlock.PreviousHash);
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
                transfer_info.ResultCode = APIResultCodes.UnknownError;
            }
            return Task.FromResult(transfer_info);
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
