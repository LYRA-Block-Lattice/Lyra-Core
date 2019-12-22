using Lyra.Authorizer.Authorizers;
using Lyra.Authorizer.Services;
using Lyra.Core.Accounts;
using Lyra.Core.Accounts.Node;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Fees;
using Lyra.Core.Blocks.Transactions;
using Lyra.Core.Cryptography;
using Lyra.Exchange;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Newtonsoft.Json;
using Orleans;
using Orleans.Configuration;
using Orleans.Providers;
using Orleans.Runtime;
using Orleans.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Lyra.Authorizer.Decentralize
{
    public class LyraConfig
    {
        public string DatabaseName { get; set; }
        public string DBConnect { get; set; }
        public string DexDBConnect { get; set; }
        public string NetworkId { get; set; }
    }

    [StorageProvider(ProviderName = "OrleansStorage")]
    public class ApiService : Grain, INodeTransactionAPI, IBlockConsensus
    {
        private readonly ILogger<ApiService> _logger;
        ServiceAccount _serviceAccount;
        IAccountCollection _accountCollection;
        IAccountDatabase _accountDatabase;
        private LyraConfig _config;

        private string NodeTag;
        private bool IsSeedNode = false;
        private long UIndexSeed = 0;

        private IAsyncStream<ChatMsg> _gossipStream;

        public ApiService(ILogger<ApiService> logger, 
            IServiceProvider serviceProvider,
            IAccountCollection accountCollection,
            IAccountDatabase accountDatabase,
            ServiceAccount serviceAccount,
            IOptions<LyraConfig> config)
        {
            _logger = logger;
            _config = config.Value;
            _accountCollection = accountCollection;
            _accountDatabase = accountDatabase;
            _serviceAccount = serviceAccount;
        }

        //// main
        //public async Task InitializeNodeAsync(string nodeTag, bool isSeedNode)
        //{
        //    NodeTag = nodeTag;
        //    IsSeedNode = isSeedNode;

        //    Console.WriteLine("Starting Lyra network: " + _config.NetworkId);

        //    UIndexSeed = await _accountCollection.GetBlockCountAsync();
            
        //    Console.WriteLine("Database Location: mongodb " + (_accountCollection as MongoAccountCollection).Cluster);
        //    Console.WriteLine("Node is starting");
        //}

        public override Task OnActivateAsync()
        {
            return InitGossipChannel();
        }

        private async Task InitGossipChannel()
        {
            _gossipStream = GetStreamProvider(LyraGossipConstants.LyraGossipStreamProvider)
                .GetStream<ChatMsg>(Guid.Parse(LyraGossipConstants.LyraGossipStreamId), LyraGossipConstants.LyraGossipStreamNameSpace);
            await _gossipStream.SubscribeAsync(OnNextAsync, OnErrorAsync, OnCompletedAsync);

            RegisterTimer(s =>
            {
                return _gossipStream.OnNextAsync(new ChatMsg($"LyraNode[{NodeTag}]", "ImLive"));
            }, null, TimeSpan.FromMilliseconds(60000), TimeSpan.FromMilliseconds(60000));

            //await _gossipStream.OnNextAsync(new ChatMsg($"LyraNode[{NodeTag}]", $"Startup. IsSeedNode: {IsSeedNode}"));
        }

        public bool ModeConsensus => NodeService.Instance.ModeConsensus;

        public async Task SendMessage(ChatMsg msg)
        {
            await _gossipStream.OnNextAsync(msg);
        }

        public Task OnCompletedAsync()
        {
            _logger.LogInformation("Chatroom message stream received stream completed event");
            return Task.CompletedTask;
        }

        public Task OnErrorAsync(Exception ex)
        {
            _logger.LogInformation($"Chatroom is experiencing message delivery failure, ex :{ex}");
            return Task.CompletedTask;
        }

        public Task OnNextAsync(ChatMsg item, StreamSequenceToken token = null)
        {
            var info = $"=={item.Created}==         {item.From} said: {item.Text}";
            _logger.LogInformation(info);
            return Task.CompletedTask;
        }

        public long GenerateUniversalBlockId()
        {
            // if self master, use seeds; if not, ask master node
            return UIndexSeed++;
        }

        internal Task<bool> Pre_PrepareAsync(TransactionBlock block)
        {
            throw new NotImplementedException();
        }

        internal Task<bool> PrepareAsync(TransactionBlock block)
        {
            throw new NotImplementedException();
        }

        internal Task<bool> CommitAsync(TransactionBlock block)
        {
            throw new NotImplementedException();
        }

        public async Task<AuthorizationAPIResult> OpenAccountWithGenesis(LyraTokenGenesisBlock block)
        {
            // Send to the authorizations sample - TO DO
            // For now, implementation for single-node testnet only
            // ***
            // to do - sign by authorizer and send to the outgoing queue
            var result = new AuthorizationAPIResult();

            try
            {
                var authorizer = GrainFactory.GetGrain<GenesisAuthorizer>(Guid.NewGuid()); //new GenesisAuthorizer(_serviceAccount, _accountCollection);

                var openBlock = block;
                result.ResultCode = await authorizer.Authorize(openBlock);
                if (result.ResultCode == APIResultCodes.Success)
                {
                    result.Authorizations = openBlock.Authorizations;
                    result.ServiceHash = openBlock.ServiceHash;

                    await ProcessTokenGenerationFee(openBlock);
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

            return result;
        }

        public async Task<AuthorizationAPIResult> ReceiveTransferAndOpenAccount(OpenWithReceiveTransferBlock openReceiveBlock)
        {
            // Send to the authorizations sample - TO DO
            // For now, implementation for single-node testnet only
            // ***
            // to do - sign by authorizer and send to the outgoing queue
            var result = new AuthorizationAPIResult();

            try
            {
                var authorizer = GrainFactory.GetGrain<NewAccountAuthorizer>(Guid.NewGuid());//new NewAccountAuthorizer(this, _serviceAccount, _accountCollection);
                result.ResultCode = await authorizer.Authorize(openReceiveBlock);
                if (result.ResultCode != APIResultCodes.Success)
                    return result;

                result.Authorizations = openReceiveBlock.Authorizations;
                result.ServiceHash = openReceiveBlock.ServiceHash;
                result.ResultCode = APIResultCodes.Success;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in ReceiveTransferAndOpenAccount: " + e.Message);
                result.ResultCode = APIResultCodes.ExceptionInReceiveTransferAndOpenAccount;
            }
            return result;
        }


        public async Task<AuthorizationAPIResult> OpenAccountWithImport(OpenAccountWithImportBlock block)
        {
            var result = new AuthorizationAPIResult();

            try
            {
                var authorizer = GrainFactory.GetGrain<NewAccountWithImportAuthorizer>(Guid.NewGuid());//new NewAccountWithImportAuthorizer(this, _serviceAccount, _accountCollection);
                result.ResultCode = await authorizer.Authorize(block);
                if (result.ResultCode != APIResultCodes.Success)
                    return result;

                result.Authorizations = block.Authorizations;
                result.ServiceHash = block.ServiceHash;
                result.ResultCode = APIResultCodes.Success;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in OpenAccountWithImport: " + e.Message);
                result.ResultCode = APIResultCodes.UnknownError;
            }
            return result;
        }

        public async Task<AuthorizationAPIResult> SendTransfer(SendTransferBlock sendBlock)
        {
            // Send to the authorizations sample - TO DO
            // For now, implementation for single-node testnet only
            // ***
            // to do - sign by authorizer and send to the outgoing queue

            var result = new AuthorizationAPIResult();

            try
            {
                var authorizer = (sendBlock is ExchangingBlock) ?
                    GrainFactory.GetGrain<ExchangingAuthorizer>(Guid.NewGuid()) :
                    GrainFactory.GetGrain<IAuthorizer>(Guid.NewGuid(), "Lyra.Authorizer.Authorizers.SendTransferAuthorizer");

                result.ResultCode = await authorizer.Authorize(sendBlock);
                if (result.ResultCode != APIResultCodes.Success)
                {
                    Console.WriteLine("Authorization failed" + result.ResultCode.ToString());
                    //Console.WriteLine(JsonConvert.SerializeObject(sendBlock));
                    //Console.WriteLine(sendBlock.CalculateHash());
                    return result;
                }

                var r = await ProcessTransferFee(sendBlock);
                if (r != APIResultCodes.Success)
                    Console.WriteLine("Error in SendTransfer->ProcessTransferFee: " + r.ToString());

                result.Authorizations = sendBlock.Authorizations;
                result.ServiceHash = sendBlock.ServiceHash;
                result.ResultCode = APIResultCodes.Success;

                //// test. send notify
                //NotifyService.Notify(sendBlock.AccountID, NotifySource.Balance, "", "", "");
                //NotifyService.Notify(sendBlock.DestinationAccountId, NotifySource.Balance, "", "", "");
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in SendTransfer: " + e.Message);
                result.ResultCode = APIResultCodes.ExceptionInSendTransfer;
            }
            return await Task.FromResult(result);

        }

        public Task<AuthorizationAPIResult> SendExchangeTransfer(ExchangingBlock block)
        {
            return SendTransfer(block);
        }

        public async Task<AuthorizationAPIResult> ReceiveTransfer(ReceiveTransferBlock receiveBlock)
        {
            // Send to the authorizations sample - TO DO
            // For now, implementation for single-node testnet only
            // ***
            // to do - sign by authorizer and send to the outgoing queue

            var result = new AuthorizationAPIResult();

            try
            {
                var authorizer = GrainFactory.GetGrain<ReceiveTransferAuthorizer>(Guid.NewGuid());//new ReceiveTransferAuthorizer(this, _serviceAccount, _accountCollection);

                result.ResultCode = await authorizer.Authorize(receiveBlock);

                if (result.ResultCode != APIResultCodes.Success)
                {
                    return result;
                }

                result.Authorizations = receiveBlock.Authorizations;
                result.ServiceHash = receiveBlock.ServiceHash;
                result.ResultCode = APIResultCodes.Success;

            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in ReceiveTransfer: " + e.Message);
                result.ResultCode = APIResultCodes.ExceptionInReceiveTransfer;
            }
            return result;
        }

        public async Task<AuthorizationAPIResult> ImportAccount(ImportAccountBlock block)
        {
            // Send to the authorizations sample - TO DO
            // For now, implementation for single-node testnet only
            // ***
            // to do - sign by authorizer and send to the outgoing queue

            var result = new AuthorizationAPIResult();

            try
            {

                var authorizer = GrainFactory.GetGrain<ImportAccountAuthorizer>(Guid.NewGuid());//new ImportAccountAuthorizer(this, _serviceAccount, _accountCollection);

                result.ResultCode = await authorizer.Authorize(block);

                if (result.ResultCode != APIResultCodes.Success)
                {
                    return result;
                }

                result.Authorizations = block.Authorizations;
                result.ServiceHash = block.ServiceHash;
                result.ResultCode = APIResultCodes.Success;

            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in ImportAccount: " + e.Message);
                result.ResultCode = APIResultCodes.UnknownError;
            }
            return result;
        }

        public async Task<AuthorizationAPIResult> CreateToken(TokenGenesisBlock tokenBlock)
        {
            var result = new AuthorizationAPIResult();

            // filter the names
            if (tokenBlock.DomainName.ToLower().StartsWith("lyra")
                || tokenBlock.Ticker.ToLower().StartsWith("lyra"))
            {
                result.ResultCode = APIResultCodes.NameUnavailable;
                return result;
            }

            try
            {
                var authorizer = GrainFactory.GetGrain<NewTokenAuthorizer>(Guid.NewGuid());//new NewTokenAuthorizer(this, _serviceAccount, _accountCollection);

                result.ResultCode = await authorizer.Authorize(tokenBlock);
                if (result.ResultCode != APIResultCodes.Success)
                {
                    return result;
                }

                var r = await ProcessTokenGenerationFee(tokenBlock);
                if (r != APIResultCodes.Success)
                    Console.WriteLine("Error in CreateToken->ProcessTokenGenerationFee: " + r.ToString());

                result.Authorizations = tokenBlock.Authorizations;
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

        public async Task<ExchangeAccountAPIResult> CreateExchangeAccount(string AccountId, string Signature)
        {
            // TODO verify signature first

            // store to database
            var acct = await NodeService.Dealer.AddExchangeAccount(AccountId);

            var result = new ExchangeAccountAPIResult()
            {
                ResultCode = APIResultCodes.Success,
                AccountId = acct.AccountId
            };
            return result;            
        }

        public async Task<ExchangeBalanceAPIResult> GetExchangeBalance(string AccountId, string Signature)
        {
            var acct = await NodeService.Dealer.GetExchangeAccount(AccountId, true);
            var result = new ExchangeBalanceAPIResult()
            {
                ResultCode = APIResultCodes.Success
            };
            if(acct != null)
            {
                result.AccountId = acct.AccountId;
                result.Balance = acct.Balance;
            }
            return result;
        }
        public async Task<CancelKey> SubmitExchangeOrder(TokenTradeOrder reqOrder)
        {
            CancelKey key;
            if (!reqOrder.VerifySignature(reqOrder.AccountID)
                || reqOrder.TokenName == null 
                || reqOrder.Price <= 0 
                || reqOrder.Amount <= 0)
            {
                key = new CancelKey() { Key = string.Empty, State = OrderState.BadOrder };
                return key;
            }

            // verify the balance. 
            var acct = await NodeService.Dealer.GetExchangeAccount(reqOrder.AccountID, true);
            if(acct == null)
            {
                return new CancelKey() { Key = string.Empty, State = OrderState.BadOrder };
            }

            if (reqOrder.BuySellType == OrderType.Sell)
            {                
                if(acct.Balance.ContainsKey(reqOrder.TokenName) && acct.Balance[reqOrder.TokenName] < reqOrder.Amount)
                    return new CancelKey() { Key = string.Empty, State = OrderState.InsufficientFunds };
            }
            else
            {
                // buy order
                if(acct.Balance.ContainsKey(LyraGlobal.LYRA_TICKER_CODE) && acct.Balance[LyraGlobal.LYRA_TICKER_CODE] < reqOrder.Amount * reqOrder.Price)
                    return new CancelKey() { Key = string.Empty, State = OrderState.InsufficientFunds };
            }
                
            return await NodeService.Dealer.AddOrderAsync(acct, reqOrder);
        }

        public async Task<APIResult> CancelExchangeOrder(string AccountId, string Signature, string cancelKey)
        {
            await NodeService.Dealer.RemoveOrderAsync(cancelKey);
            return new APIResult() { ResultCode = APIResultCodes.Success };
        }

        public Task<ExchangeAccountAPIResult> CloseExchangeAccount(string AccountId, string Signature)
        {
            throw new NotImplementedException();
        }

        Task<APIResult> CustomizeNotifySettings(NotifySettings settings)
        {
            throw new NotImplementedException();
        }

        #region Fee processing private methods

        async Task<APIResultCodes> ProcessTransferFee(SendTransferBlock sendBlock)
        {
            // TO DO: handle all token balances, not just LYRA
            if(sendBlock is ExchangingBlock)
            {
                if(sendBlock.Fee != ExchangingBlock.FEE)
                    return APIResultCodes.InvalidFeeAmount;
            }
            else if (sendBlock.Fee != _serviceAccount.GetLastServiceBlock().TransferFee)
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

            TransactionBlock latestBlock = _accountCollection.FindLatestBlock(_serviceAccount.AccountId);
            if(latestBlock == null)
            {
                var receiveBlock = new OpenWithReceiveFeeBlock
                {
                    AccountType = AccountTypes.Service,
                    AccountID = _serviceAccount.AccountId,
                    ServiceHash = string.Empty,
                    SourceHash = source,
                    Fee = 0,
                    FeeType = AuthorizationFeeTypes.NoFee,
                    Balances = new Dictionary<string, decimal>()
                };
                receiveBlock.Balances.Add(LyraGlobal.LYRA_TICKER_CODE, fee);
                receiveBlock.InitializeBlock(null, _serviceAccount.PrivateKey, _serviceAccount.NetworkId);

                var authorizer = GrainFactory.GetGrain<NewAccountAuthorizer>(Guid.NewGuid());//new NewAccountAuthorizer(this, _serviceAccount, _accountCollection);
                callresult = await authorizer.Authorize(receiveBlock);
            }
            else
            {
                var receiveBlock = new ReceiveFeeBlock
                {
                    AccountID = _serviceAccount.AccountId,
                    ServiceHash = string.Empty,
                    SourceHash = source,
                    Fee = 0,
                    FeeType = AuthorizationFeeTypes.NoFee,
                    Balances = new Dictionary<string, decimal>()
                };

                decimal newBalance = latestBlock.Balances[LyraGlobal.LYRA_TICKER_CODE] + fee;
                receiveBlock.Balances.Add(LyraGlobal.LYRA_TICKER_CODE, newBalance);
                receiveBlock.InitializeBlock(latestBlock, _serviceAccount.PrivateKey, _serviceAccount.NetworkId);

                var authorizer = GrainFactory.GetGrain<ReceiveTransferAuthorizer>(Guid.NewGuid());//new ReceiveTransferAuthorizer(this, _serviceAccount, _accountCollection);
                callresult = await authorizer.Authorize(receiveBlock);
            }

            //receiveBlock.Signature = Signatures.GetSignature(_serviceAccount.PrivateKey, receiveBlock.Hash);
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


        public Task<ActiveTradeOrdersAPIResult> GetActiveTradeOrders(string AccountId, string SellToken, string BuyToken, TradeOrderListTypes OrderType, string Signature)
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

        public async Task<APIResult> RequestMarket(string tokenName)
        {
            if(tokenName != null)
                await NodeService.Dealer.SendMarket(tokenName);
            return new APIResult() { ResultCode = APIResultCodes.Success };
        }

        public async Task<List<ExchangeOrder>> GetOrdersForAccount(string AccountId, string Signature)
        {
            return await NodeService.Dealer.GetOrdersForAccount(AccountId);
        }

        //public override async Task<GetVersionReply> GetVersion(GetVersionRequest request, ServerCallContext context)
        //{
        //    var cr = await GetVersion(request.ApiVersion, request.AppName, request.Appversion);
        //    var result = new GetVersionReply()
        //    {
        //        ApiVersion = cr.ApiVersion,
        //        NodeVersion = cr.NodeVersion,
        //        ResultCode = cr.ResultCode,
        //        UpgradeNeeded = cr.UpgradeNeeded,
        //        MustUpgradeToConnect = cr.MustUpgradeToConnect
        //    };
        //    return result;
        //}

        //public override async Task<AccountHeightReply> GetSyncHeight(SimpleRequest request, ServerCallContext context)
        //{
        //    var cr = await GetSyncHeight();
        //    var result = new AccountHeightReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        NetworkId = cr.NetworkId,
        //        Height = cr.Height,
        //        SyncHash = cr.SyncHash
        //    };
        //    return result;
        //}
        //public override async Task<GetTokenNamesReply> GetTokenNames(GetTokenNamesRequest request, ServerCallContext context)
        //{
        //    var cr = await GetTokenNames(request.AccountId, request.Signature, request.Keyword);
        //    var result = new GetTokenNamesReply()
        //    {
        //        ResultCode = cr.ResultCode
        //    };
        //    result.TokenNames.AddRange(cr.TokenNames);

        //    return result;
        //}
        //public override async Task<GetAccountHeightReply> GetAccountHeight(StandardWalletRequest request, ServerCallContext context)
        //{
        //    var cr = await GetAccountHeight(request.AccountId, request.Signature);
        //    var result = new GetAccountHeightReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        Height = cr.Height,
        //        SyncHash = cr.SyncHash,
        //        NetworkId = cr.NetworkId
        //    };
        //    return result;
        //}
        //public override async Task<GetBlockReply> GetBlockByIndex(GetBlockByIndexRequest request, ServerCallContext context)
        //{
        //    var cr = await GetBlockByIndex(request.AccountId, request.Index, request.Signature);
        //    var result = new GetBlockReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        BlockData = cr.BlockData,
        //        ResultBlockType = cr.ResultBlockType
        //    };

        //    return result;
        //}
        //public override async Task<GetBlockReply> GetBlockByHash(GetBlockByHashRequest request, ServerCallContext context)
        //{
        //    var cr = await GetBlockByHash(request.AccountId, request.Hash, request.Signature);
        //    var result = new GetBlockReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        BlockData = cr.BlockData,
        //        ResultBlockType = cr.ResultBlockType
        //    };

        //    return result;
        //}
        //public override async Task<GetNonFungibleTokensReply> GetNonFungibleTokens(StandardWalletRequest request, ServerCallContext context)
        //{
        //    var cr = await GetNonFungibleTokens(request.AccountId, request.Signature);
        //    var result = new GetNonFungibleTokensReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        ListDataSerialized = cr.ListDataSerialized
        //    };
        //    return result;
        //}
        //public override async Task<GetBlockReply> GetTokenGenesisBlock(GetTokenGenesisBlockRequest request, ServerCallContext context)
        //{
        //    var cr = await GetTokenGenesisBlock(request.AccountId, request.TokenTicker, request.Signature);
        //    var result = new GetBlockReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        BlockData = cr.BlockData,
        //        ResultBlockType = cr.ResultBlockType
        //    };
        //    return result;
        //}
        //public override async Task<GetBlockReply> GetLastServiceBlock(StandardWalletRequest request, ServerCallContext context)
        //{
        //    var cr = await GetLastServiceBlock(request.AccountId, request.Signature);
        //    var result = new GetBlockReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        BlockData = cr.BlockData,
        //        ResultBlockType = cr.ResultBlockType
        //    };
        //    return result;
        //}
        //public override async Task<LookForNewTransferReply> LookForNewTransfer(StandardWalletRequest request, ServerCallContext context)
        //{
        //    var cr = await LookForNewTransfer(request.AccountId, request.Signature);
        //    LookForNewTransferReply transfer_info = new LookForNewTransferReply()
        //    {
        //        ResultCode = cr.ResultCode
        //    };
        //    if (cr.ResultCode != APIResultCodes.NoNewTransferFound)
        //    {
        //        transfer_info.NonFungibleTokenJson = Json(cr.NonFungibleToken);
        //        transfer_info.SourceHash = cr.SourceHash;
        //        transfer_info.TransferJson = Json(cr.Transfer);
        //    }
        //    return transfer_info;
        //}
        //public override async Task<AuthorizationsReply> OpenAccountWithGenesis(OpenAccountWithGenesisRequest request, ServerCallContext context)
        //{
        //    var openBlock = FromJson<LyraTokenGenesisBlock>(request.OpenTokenGenesisBlockJson);
        //    var cr = await OpenAccountWithGenesis(openBlock);
        //    var result = new AuthorizationsReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        ServiceHash = cr.ServiceHash ?? string.Empty,
        //        AuthorizationsJson = Json(cr.Authorizations)
        //    };
        //    return result;
        //}
        //public override async Task<AuthorizationsReply> ReceiveTransferAndOpenAccount(ReceiveTransferAndOpenAccountRequest request, ServerCallContext context)
        //{
        //    var openReceiveBlock = FromJson<OpenWithReceiveTransferBlock>(request.OpenReceiveBlockJson);
        //    var cr = await ReceiveTransferAndOpenAccount(openReceiveBlock);
        //    if(cr.ResultCode == APIResultCodes.Success)
        //    {
        //        var result = new AuthorizationsReply()
        //        {
        //            ResultCode = cr.ResultCode,
        //            ServiceHash = cr.ServiceHash ?? string.Empty,
        //            AuthorizationsJson = Json(cr.Authorizations)
        //        };
        //        return result;
        //    }
        //    else
        //    {
        //        var result = new AuthorizationsReply() { ResultCode = cr.ResultCode };
        //        return result;
        //    }

        //}
        //public override async Task<AuthorizationsReply> OpenAccountWithImport(OpenAccountWithImportRequest request, ServerCallContext context)
        //{
        //    var block = FromJson<OpenAccountWithImportBlock>(request.BlockJson);
        //    var cr = await OpenAccountWithImport(block);
        //    var result = new AuthorizationsReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        ServiceHash = cr.ServiceHash ?? string.Empty,
        //        AuthorizationsJson = Json(cr.Authorizations)
        //    };
        //    return result;
        //}
        //public override async Task<AuthorizationsReply> SendTransfer(SendTransferRequest request, ServerCallContext context)
        //{
        //    var sendBlock = FromJson<SendTransferBlock>(request.SendBlockJson);
        //    var cr = await SendTransfer(sendBlock);
        //    var result = new AuthorizationsReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        ServiceHash = cr.ServiceHash ?? string.Empty,
        //        AuthorizationsJson = Json(cr.Authorizations)
        //    };
        //    return result;
        //}
        //public override async Task<AuthorizationsReply> SendExchangeTransfer(SendTransferRequest request, ServerCallContext context)
        //{
        //    var sendBlock = FromJson<ExchangingBlock>(request.SendBlockJson);
        //    var cr = await SendTransfer(sendBlock);
        //    var result = new AuthorizationsReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        ServiceHash = cr.ServiceHash ?? string.Empty,
        //        AuthorizationsJson = Json(cr.Authorizations)
        //    };
        //    return result;
        //}
        //public override async Task<AuthorizationsReply> ReceiveTransfer(ReceiveTransferRequest request, ServerCallContext context)
        //{
        //    var receiveBlock = FromJson<ReceiveTransferBlock>(request.ReceiveBlockJson);
        //    var cr = await ReceiveTransfer(receiveBlock);
        //    var result = new AuthorizationsReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        ServiceHash = cr.ServiceHash ?? string.Empty,
        //        AuthorizationsJson = Json(cr.Authorizations)
        //    };
        //    return result;
        //}
        //public override async Task<AuthorizationsReply> ImportAccount(ImportAccountRequest request, ServerCallContext context)
        //{
        //    var block = FromJson<ImportAccountBlock>(request.ImportBlockJson);
        //    var cr = await ImportAccount(block);
        //    var result = new AuthorizationsReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        ServiceHash = cr.ServiceHash ?? string.Empty,
        //        AuthorizationsJson = Json(cr.Authorizations)
        //    };
        //    return result;
        //}


        //public override async Task<AuthorizationsReply> CreateToken(CreateTokenRequest request, ServerCallContext context)
        //{
        //    var tokenBlock = FromJson<TokenGenesisBlock>(request.CreateTokenJson);
        //    var cr = await CreateToken(tokenBlock);
        //    var result = new AuthorizationsReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        ServiceHash = cr.ServiceHash ?? string.Empty,
        //        AuthorizationsJson = Json(cr.Authorizations)
        //    };
        //    return result;
        //}

        //public override async Task<ExchangeAccountReply> CreateExchangeAccount(StandardWalletRequest request, ServerCallContext context)
        //{
        //    var cr = await CreateExchangeAccount(request.AccountId, request.Signature);
        //    var result = new ExchangeAccountReply()
        //    {
        //        ResultCode = cr.ResultCode,
        //        AccountId = cr.AccountId
        //    };
        //    return result;
        //}

        //public override async Task<GetExchangeBalanceReply> GetExchangeBalance(StandardWalletRequest request, ServerCallContext context)
        //{
        //    var cr = await GetExchangeBalance(request.AccountId, request.Signature);
        //    var result = new GetExchangeBalanceReply()
        //    {
        //        AccountId = cr.AccountId,
        //        ResultCode = cr.ResultCode,
        //        BalanceJson = Json(cr.Balance)
        //    };
        //    return result;
        //}

        //public override async Task<SubmitExchangeOrderReply> SubmitExchangeOrder(SubmitExchangeOrderRequest request, ServerCallContext context)
        //{
        //    var order = FromJson<TokenTradeOrder>(request.TokenTradeOrderJson);
        //    var cr = await SubmitExchangeOrder(order);
        //    var result = new SubmitExchangeOrderReply()
        //    {
        //        CancelKeyJson = Json(cr)
        //    };
        //    return result;
        //}

    }
}
