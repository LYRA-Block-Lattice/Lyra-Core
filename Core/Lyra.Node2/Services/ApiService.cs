using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Lyra.Core.Accounts.Node;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Fees;
using Lyra.Core.Blocks.Transactions;
using Lyra.Node2.Authorizers;
using Lyra.Core.Protos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Lyra.Core.API;

namespace Lyra.Node2.Services
{
    public class LyraConfig
    {
        public string DBConnect { get; set; }
        public string NetworkId { get; set; }
    }

    public class ApiService : LyraApi.LyraApiBase, INodeAPI
    {
        private readonly ILogger<ApiService> _logger;
        static ServiceAccount _serviceAccount;
        static IAccountCollection _accountCollection;
        private LyraConfig _config;

        public ApiService(ILogger<ApiService> logger, Microsoft.Extensions.Options.IOptions<LyraConfig> config)
        {
            _logger = logger;
            _config = config.Value;

            if(_serviceAccount == null)
                InitializeNode();
        }

        public Task<AccountHeightAPIResult> GetSyncHeight()
        {
            var result = new AccountHeightAPIResult();
            try
            {
                var last_sync_block = _serviceAccount.GetLatestBlock();
                result.Height = last_sync_block.Index;
                result.SyncHash = last_sync_block.Hash;
                result.NetworkId = NodeGlobalParameters.Network_Id;
                result.ResultCode = APIResultCodes.Success;
            }
            catch (Exception e)
            {
                result.ResultCode = APIResultCodes.UnknownError;
            }
            return Task.FromResult(result);
        }

        public override async Task<AccountHeightReply> GetSyncHeight(SyncHeightRequest request, ServerCallContext context)
        {
            var cr = await GetSyncHeight();
            var result = new AccountHeightReply()
            {
                ResultCode = cr.ResultCode,
                NetworkId = cr.NetworkId,
                Height = cr.Height,
                SyncHash = cr.SyncHash
            };            
            return result;
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

        public override async Task<GetTokenNamesReply> GetTokenNames(GetTokenNamesRequest request, ServerCallContext context)
        {
            var cr = await GetTokenNames(request.AccountId, request.Signature, request.Keyword);
            var result = new GetTokenNamesReply()
            {
                ResultCode = cr.ResultCode                
            };           
            result.TokenNames.AddRange(cr.TokenNames);

            return result;
        }

        public Task<AccountHeightAPIResult> GetAccountHeight(string AccountId, string Signature)
        {
            var result = new AccountHeightAPIResult();
            try
            {
                if (_accountCollection.AccountExists(AccountId))
                {
                    result.Height = _accountCollection.FindLatestBlock(AccountId).Index;
                    result.NetworkId = NodeGlobalParameters.Network_Id;
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

        public override async Task<GetAccountHeightReply> GetAccountHeight(GetAccountHeightRequest request, ServerCallContext context)
        {
            var cr = await GetAccountHeight(request.AccountId, request.Signature);
            var result = new GetAccountHeightReply()
            {
                ResultCode = cr.ResultCode,
                Height = cr.Height,
                SyncHash = cr.SyncHash,
                NetworkId = cr.NetworkId
            };
            return result;
        }

        public override Task<GetBlockReply> GetBlockByIndex(GetBlockByIndexRequest request, ServerCallContext context)
        {
            var result = new GetBlockReply();

            try
            {
                if (_accountCollection.AccountExists(request.AccountId))
                {
                    var block = _accountCollection.FindBlockByIndex(request.AccountId, request.Index);
                    if (block != null)
                    {
                        result.BlockData = Json(block);
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

        public override Task<GetBlockReply> GetBlockByHash(GetBlockByHashRequest request, ServerCallContext context)
        {
            var result = new GetBlockReply();

            try
            {
                if (!_accountCollection.AccountExists(request.AccountId))
                    result.ResultCode = APIResultCodes.AccountDoesNotExist;

                var block = _accountCollection.FindBlockByHash(request.AccountId, request.Hash);
                if (block != null)
                {
                    result.BlockData = Json(block);
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

        public override Task<GetNonFungibleTokensReply> GetNonFungibleTokens(GetNonFungibleTokensRequest request, ServerCallContext context)
        {
            var result = new GetNonFungibleTokensReply();

            try
            {
                if (!_accountCollection.AccountExists(request.AccountId))
                    result.ResultCode = APIResultCodes.AccountDoesNotExist;

                var list = _accountCollection.GetNonFungibleTokens(request.AccountId);
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

        public override Task<GetBlockReply> GetTokenGenesisBlock(GetTokenGenesisBlockRequest request, ServerCallContext context)
        {
            var result = new GetBlockReply();

            try
            {
                //if (!_accountCollection.AccountExists(AccountId))
                //    result.ResultCode = APIResultCodes.AccountDoesNotExist;

                var block = _accountCollection.FindTokenGenesisBlock(null, request.TokenTicker);
                if (block != null)
                {
                    result.BlockData = Json(block);
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

        public override Task<GetBlockReply> GetLastServiceBlock(GetLastServiceBlockRequest request, ServerCallContext context)
        {
            var result = new GetBlockReply();

            try
            {
                if (!_accountCollection.AccountExists(request.AccountId))
                    result.ResultCode = APIResultCodes.AccountDoesNotExist;

                var block = _serviceAccount.GetLastServiceBlock();
                if (block != null)
                {
                    result.BlockData = Json(block);
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

        public override Task<LookForNewTransferReply> LookForNewTransfer(LookForNewTransferRequest request, ServerCallContext context)
        {
            LookForNewTransferReply transfer_info = new LookForNewTransferReply();
            try
            {
                SendTransferBlock sendBlock = _accountCollection.FindUnsettledSendBlock(request.AccountId);

                if (sendBlock != null)
                {
                    TransactionBlock previousBlock = _accountCollection.FindBlockByHash(sendBlock.PreviousHash);
                    if (previousBlock == null)
                        transfer_info.ResultCode = APIResultCodes.CouldNotTraceSendBlockChain;
                    else
                    {
                        transfer_info.TransferJson = Json(sendBlock.GetTransaction(previousBlock)); //CalculateTransaction(sendBlock, previousSendBlock);
                        transfer_info.SourceHash = sendBlock.Hash;
                        transfer_info.NonFungibleTokenJson = Json(sendBlock.NonFungibleToken);
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

        public override Task<AuthorizationsReply> OpenAccountWithGenesis(OpenAccountWithGenesisRequest request, ServerCallContext context)
        {
            // Send to the authorizations sample - TO DO
            // For now, implementation for single-node testnet only
            // ***
            // to do - sign by authorizer and send to the outgoing queue
            var result = new AuthorizationsReply();

            try
            {
                var authorizer = new GenesisAuthorizer(_serviceAccount, _accountCollection);

                var openBlock = FromJson<LyraTokenGenesisBlock>(request.OpenTokenGenesisBlockJson);
                result.ResultCode = authorizer.Authorize(ref openBlock);
                if (result.ResultCode == APIResultCodes.Success)
                {
                    result.AuthorizationsJson = Json(openBlock.Authorizations);
                    result.ServiceHash = openBlock.ServiceHash;
                }
                else
                {
                    Console.WriteLine(openBlock.Print());
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in OpenAccountWithGenesis: " + e.Message);
                result.ResultCode = APIResultCodes.ExceptionInOpenAccountWithGenesis;
            }

            return Task.FromResult(result);
        }

        public override Task<AuthorizationsReply> ReceiveTransferAndOpenAccount(ReceiveTransferAndOpenAccountRequest request, ServerCallContext context)
        {
            // Send to the authorizations sample - TO DO
            // For now, implementation for single-node testnet only
            // ***
            // to do - sign by authorizer and send to the outgoing queue
            var result = new AuthorizationsReply();

            try
            {
                var authorizer = new NewAccountAuthorizer(_serviceAccount, _accountCollection);
                var openReceiveBlock = FromJson<OpenWithReceiveTransferBlock>(request.OpenReceiveBlockJson);
                result.ResultCode = authorizer.Authorize(ref openReceiveBlock);
                if (result.ResultCode != APIResultCodes.Success)
                    return Task.FromResult(result);

                result.AuthorizationsJson = Json(openReceiveBlock.Authorizations);
                result.ServiceHash = openReceiveBlock.ServiceHash;
                result.ResultCode = APIResultCodes.Success;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in ReceiveTransferAndOpenAccount: " + e.Message);
                result.ResultCode = APIResultCodes.ExceptionInReceiveTransferAndOpenAccount;
            }
            return Task.FromResult(result);
        }

        public override Task<AuthorizationsReply> OpenAccountWithImport(OpenAccountWithImportRequest request, ServerCallContext context)
        {
            var result = new AuthorizationsReply();

            try
            {
                var authorizer = new NewAccountWithImportAuthorizer(_serviceAccount, _accountCollection);
                var block = FromJson<OpenAccountWithImportBlock>(request.BlockJson);
                result.ResultCode = authorizer.Authorize(ref block);
                if (result.ResultCode != APIResultCodes.Success)
                    return Task.FromResult(result);

                result.AuthorizationsJson = Json(block.Authorizations);
                result.ServiceHash = block.ServiceHash;
                result.ResultCode = APIResultCodes.Success;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in OpenAccountWithImport: " + e.Message);
                result.ResultCode = APIResultCodes.UnknownError;
            }
            return Task.FromResult(result);
        }

        public override async Task<AuthorizationsReply> SendTransfer(SendTransferRequest request, ServerCallContext context)
        {
            // Send to the authorizations sample - TO DO
            // For now, implementation for single-node testnet only
            // ***
            // to do - sign by authorizer and send to the outgoing queue

            var result = new AuthorizationsReply();

            try
            {
                var authorizer = new SendTransferAuthorizer(_serviceAccount, _accountCollection);
                var sendBlock = FromJson<SendTransferBlock>(request.SendBlockJson);
                result.ResultCode = authorizer.Authorize(ref sendBlock);
                if (result.ResultCode != APIResultCodes.Success)
                {
                    Console.WriteLine("Authorization failed" + result.ResultCode.ToString());
                    //Console.WriteLine(JsonConvert.SerializeObject(sendBlock));
                    //Console.WriteLine(sendBlock.CalculateHash());
                    return await Task.FromResult(result);
                }

                var r = await ProcessTransferFee(sendBlock);
                if (r != APIResultCodes.Success)
                    Console.WriteLine("Error in SendTransfer->ProcessTransferFee: " + r.ToString());

                result.AuthorizationsJson = Json(sendBlock.Authorizations);
                result.ServiceHash = sendBlock.ServiceHash;
                result.ResultCode = APIResultCodes.Success;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in SendTransfer: " + e.Message);
                result.ResultCode = APIResultCodes.ExceptionInSendTransfer;
            }
            return await Task.FromResult(result);
        }

        public override Task<AuthorizationsReply> ReceiveTransfer(ReceiveTransferRequest request, ServerCallContext context)
        {
            // Send to the authorizations sample - TO DO
            // For now, implementation for single-node testnet only
            // ***
            // to do - sign by authorizer and send to the outgoing queue

            var result = new AuthorizationsReply();

            try
            {
                var authorizer = new ReceiveTransferAuthorizer(_serviceAccount, _accountCollection);
                var receiveBlock = FromJson<ReceiveTransferBlock>(request.ReceiveBlockJson);
                result.ResultCode = authorizer.Authorize(ref receiveBlock);

                if (result.ResultCode != APIResultCodes.Success)
                {
                    return Task.FromResult(result);
                }

                result.AuthorizationsJson = Json(receiveBlock.Authorizations);
                result.ServiceHash = receiveBlock.ServiceHash;
                result.ResultCode = APIResultCodes.Success;

            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in ReceiveTransfer: " + e.Message);
                result.ResultCode = APIResultCodes.ExceptionInReceiveTransfer;
            }
            return Task.FromResult(result);
        }

        public override Task<AuthorizationsReply> ImportAccount(ImportAccountRequest request, ServerCallContext context)
        {
            // Send to the authorizations sample - TO DO
            // For now, implementation for single-node testnet only
            // ***
            // to do - sign by authorizer and send to the outgoing queue

            var result = new AuthorizationsReply();

            try
            {

                var authorizer = new ImportAccountAuthorizer(_serviceAccount, _accountCollection);
                var block = FromJson<ImportAccountBlock>(request.ImportBlockJson);
                result.ResultCode = authorizer.Authorize(ref block);

                if (result.ResultCode != APIResultCodes.Success)
                {
                    return Task.FromResult(result);
                }

                result.AuthorizationsJson = Json(block.Authorizations);
                result.ServiceHash = block.ServiceHash;
                result.ResultCode = APIResultCodes.Success;

            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in ImportAccount: " + e.Message);
                result.ResultCode = APIResultCodes.UnknownError;
            }
            return Task.FromResult(result);
        }

        public override async Task<AuthorizationsReply> CreateToken(CreateTokenRequest request, ServerCallContext context)
        {
            var result = new AuthorizationsReply();

            //// filter the names
            //if (tokenBlock.DomainName.ToLower().StartsWith("lyra")
            //    || tokenBlock.Ticker.ToLower().StartsWith("lyra"))
            //{
            //    result.ResultCode = APIResultCodes.NameUnavailable;
            //    return result;
            //}

            try
            {
                var authorizer = new NewTokenAuthorizer(_serviceAccount, _accountCollection);
                var tokenBlock = FromJson<TokenGenesisBlock>(request.CreateTokenJson);
                result.ResultCode = authorizer.Authorize(ref tokenBlock);
                if (result.ResultCode != APIResultCodes.Success)
                {
                    return await Task.FromResult(result);
                }

                var r = await ProcessTokenGenerationFee(tokenBlock);
                if (r != APIResultCodes.Success)
                    Console.WriteLine("Error in CreateToken->ProcessTokenGenerationFee: " + r.ToString());

                result.AuthorizationsJson = Json(tokenBlock.Authorizations);
                result.ServiceHash = tokenBlock.ServiceHash;
                result.ResultCode = APIResultCodes.Success;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in CreateToken: " + e.Message);
                result.ResultCode = APIResultCodes.ExceptionInCreateToken;
            }

            return await Task.FromResult(result);
        }

        #region Fee processing private methods

        async Task<APIResultCodes> ProcessTransferFee(SendTransferBlock sendBlock)
        {
            // TO DO: handle all token balances, not just LYRA
            if (sendBlock.Fee != _serviceAccount.GetLastServiceBlock().TransferFee)
                return APIResultCodes.InvalidFeeAmount;

            return await ProcessFee(sendBlock.Hash, sendBlock.Fee);
        }

        async Task<APIResultCodes> ProcessTokenGenerationFee(TokenGenesisBlock tokenBlock)
        {
            if (tokenBlock.Fee != _serviceAccount.GetLastServiceBlock().TokenGenerationFee)
                return APIResultCodes.InvalidFeeAmount;

            return await ProcessFee(tokenBlock.Hash, tokenBlock.Fee);
        }

        private async Task<APIResultCodes> ProcessFee(string source, decimal fee)
        {
            var callresult = APIResultCodes.Success;
            var receiveBlock = new ReceiveFeeBlock
            {
                AccountID = _serviceAccount.AccountId,
                ServiceHash = string.Empty,
                SourceHash = source,
                Fee = 0,
                FeeType = AuthorizationFeeTypes.NoFee,
                Balances = new Dictionary<string, decimal>()
            };

            TransactionBlock latestBlock = _accountCollection.FindLatestBlock(_serviceAccount.AccountId);
            decimal newBalance = latestBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE] + fee;
            receiveBlock.Balances.Add(TokenGenesisBlock.LYRA_TICKER_CODE, newBalance);
            receiveBlock.InitializeBlock(latestBlock, _serviceAccount.PrivateKey, _serviceAccount.NetworkId);

            //receiveBlock.Signature = Signatures.GetSignature(_serviceAccount.PrivateKey, receiveBlock.Hash);

            var authorizer = new ReceiveTransferAuthorizer(_serviceAccount, _accountCollection);
            callresult = authorizer.Authorize(ref receiveBlock);

            return callresult;
        }

        #endregion

        // util 
        private T FromJson<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
        private string Json(object o)
        {
            return JsonConvert.SerializeObject(o);
        }
        // main
        void InitializeNode()
        {
            Console.WriteLine("Starting single-node network: " + NodeGlobalParameters.Network_Id);
            NodeGlobalParameters.IsSingleNodeTestnet = true;
            NodeGlobalParameters.Network_Id = _config.NetworkId;

            var service_database = new MongoServiceAccountDatabase(_config.DBConnect, NodeGlobalParameters.DEFAULT_DATABASE_NAME, ServiceAccount.SERVICE_ACCOUNT_NAME, NodeGlobalParameters.Network_Id);
            _serviceAccount = new ServiceAccount(service_database, NodeGlobalParameters.Network_Id);

            _accountCollection = new MongoAccountCollection(_config.DBConnect, NodeGlobalParameters.DEFAULT_DATABASE_NAME, NodeGlobalParameters.Network_Id);
            Console.WriteLine("Database Location: mongodb " + (_accountCollection as MongoAccountCollection).Cluster);

            //tradeMatchEngine = new TradeMatchEngine(accountCollection, serviceAccount);
            Console.WriteLine("Node is starting");

            _serviceAccount.StartSingleNodeTestnet(null);

        }





        public Task<BlockAPIResult> GetLastServiceBlock(string AccountId, string Signature)
        {
            throw new NotImplementedException();
        }

        public Task<BlockAPIResult> GetTokenGenesisBlock(string AccountId, string TokenTicker, string Signature)
        {
            throw new NotImplementedException();
        }


        public Task<BlockAPIResult> GetBlockByIndex(string AccountId, int Index, string Signature)
        {
            throw new NotImplementedException();
        }

        public Task<BlockAPIResult> GetBlockByHash(string AccountId, string Hash, string Signature)
        {
            throw new NotImplementedException();
        }

        public Task<NewTransferAPIResult> LookForNewTransfer(string AccountId, string Signature)
        {
            throw new NotImplementedException();
        }

        public Task<TradeAPIResult> LookForNewTrade(string AccountId, string BuyTokenCode, string SellTokenCode, string Signature)
        {
            throw new NotImplementedException();
        }

        public Task<NonFungibleListAPIResult> GetNonFungibleTokens(string AccountId, string Signature)
        {
            throw new NotImplementedException();
        }

        public Task<ActiveTradeOrdersAPIResult> GetActiveTradeOrders(string AccountId, string SellToken, string BuyToken, TradeOrderListTypes OrderType, string Signature)
        {
            throw new NotImplementedException();
        }

        public Task<AuthorizationAPIResult> SendTransfer(SendTransferBlock block)
        {
            throw new NotImplementedException();
        }

        public Task<AuthorizationAPIResult> ReceiveTransfer(ReceiveTransferBlock block)
        {
            throw new NotImplementedException();
        }

        public Task<AuthorizationAPIResult> ReceiveTransferAndOpenAccount(OpenWithReceiveTransferBlock block)
        {
            throw new NotImplementedException();
        }

        public Task<AuthorizationAPIResult> ImportAccount(ImportAccountBlock block)
        {
            throw new NotImplementedException();
        }

        public Task<AuthorizationAPIResult> OpenAccountWithGenesis(LyraTokenGenesisBlock block)
        {
            throw new NotImplementedException();
        }

        public Task<AuthorizationAPIResult> OpenAccountWithImport(OpenAccountWithImportBlock block)
        {
            throw new NotImplementedException();
        }

        public Task<AuthorizationAPIResult> CreateToken(TokenGenesisBlock block)
        {
            throw new NotImplementedException();
        }

        public Task<TradeOrderAuthorizationAPIResult> TradeOrder(TradeOrderBlock block)
        {
            throw new NotImplementedException();
        }

        public Task<AuthorizationAPIResult> Trade(TradeBlock block)
        {
            throw new NotImplementedException();
        }

        public Task<AuthorizationAPIResult> ExecuteTradeOrder(ExecuteTradeOrderBlock block)
        {
            throw new NotImplementedException();
        }

        public Task<AuthorizationAPIResult> CancelTradeOrder(CancelTradeOrderBlock block)
        {
            throw new NotImplementedException();
        }
    }
}
