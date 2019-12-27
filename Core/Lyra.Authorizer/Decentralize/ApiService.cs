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
using Lyra.Core.Utils;
using System.Threading;
using Orleans.Concurrency;

namespace Lyra.Authorizer.Decentralize
{
    [StatelessWorker(1)] // max 1 activation per silo
    [StorageProvider(ProviderName = "OrleansStorage")]
    public class ApiService : Grain, INodeTransactionAPI//, IBlockConsensus
    {
        private readonly ILogger<ApiService> _log;
        ServiceAccount _serviceAccount;
        IAccountCollection _accountCollection;
        private LyraNodeConfig _config;
        GossipListener _gossipListener;
        ConsensusRuntimeConfig _consensus;

        long _useed = -1;

        public ApiService(ILogger<ApiService> logger, 
            IAccountCollection accountCollection,
            ServiceAccount serviceAccount,
            GossipListener gossipListener,
            ConsensusRuntimeConfig consensus,
            IOptions<LyraNodeConfig> config
            )
        {
            _log = logger;
            _config = config.Value;
            _accountCollection = accountCollection;
            _serviceAccount = serviceAccount;
            _gossipListener = gossipListener;
            _consensus = consensus;
        }

        public override async Task OnActivateAsync()
        {
            _log.LogInformation("ApiService: Activated");
            _useed = await _accountCollection.GetBlockCountAsync();

            //await Gossip(new ChatMsg($"LyraNode[{_config.Orleans.EndPoint.AdvertisedIPAddress}]", $"Startup. IsSeedNode: {IsSeedNode}"));
        }

        public long GenerateUniversalBlockIdAsync()
        {
            // if self master, use seeds; if not, ask master node
            return _useed++;
        }

        private async Task<AuthState> PostToConsensusAsync(TransactionBlock block)
        {
            _log.LogInformation($"ApiService: PostToConsensusAsync Called: {block.BlockType}");
            block.UIndex = GenerateUniversalBlockIdAsync();
            block.UHash = SignableObject.CalculateHash($"{block.UIndex}|{block.Index}|{block.Hash}");
            AuthorizingMsg msg = new AuthorizingMsg
            {
                From = _serviceAccount.AccountId,
                Block = block
            };
            var state = await _gossipListener.SendAuthorizingMessage(msg);
            state.Done.WaitOne();
            _log.LogInformation($"ApiService: PostToConsensusAsync Exited: IsAuthoringSuccess: {state.IsAuthoringSuccess}");
            return state;
        }

        internal async Task<bool> Pre_PrepareAsync(TransactionBlock block1, Func<TransactionBlock, Task<TransactionBlock>> OnBlockSucceed = null)
        {
            var state1 = await PostToConsensusAsync(block1);

            if(state1.IsAuthoringSuccess)
            {
                if(OnBlockSucceed != null)
                {
                    var block2 = await OnBlockSucceed(state1.InputMsg.Block);

                    var state2 = await PostToConsensusAsync(block2);

                    return state2.IsAuthoringSuccess;
                }
                return true;                
            }

            return false;
        }

        public async Task<AuthorizationAPIResult> OpenAccountWithGenesis(LyraTokenGenesisBlock block)
        {
            var result = new AuthorizationAPIResult();
            if (await Pre_PrepareAsync(block, async (b) =>
            {
                var feeResult = await ProcessTokenGenerationFee(b as LyraTokenGenesisBlock);
                return feeResult.block;
            }))
            {
                result.ResultCode = APIResultCodes.UnableToSendToConsensusNetwork;
                return result;
            }
            return result;
        }

        public async Task<AuthorizationAPIResult> ReceiveTransferAndOpenAccount(OpenWithReceiveTransferBlock openReceiveBlock)
        {
            var result = new AuthorizationAPIResult();

            // first send to network. 
            if (!await Pre_PrepareAsync(openReceiveBlock))
            {
                result.ResultCode = APIResultCodes.UnableToSendToConsensusNetwork;
            }
            else
            {
                result.ResultCode = APIResultCodes.Success;
            }
            return result;
        }


        public async Task<AuthorizationAPIResult> OpenAccountWithImport(OpenAccountWithImportBlock block)
        {
            var result = new AuthorizationAPIResult();

            // first send to network. 
            if (!await Pre_PrepareAsync(block))
            {
                result.ResultCode = APIResultCodes.UnableToSendToConsensusNetwork;
            }
            else
            {
                result.ResultCode = APIResultCodes.Success;
            }
            return result;
        }

        public async Task<AuthorizationAPIResult> SendTransfer(SendTransferBlock sendBlock)
        {
            var result = new AuthorizationAPIResult();
            if (await Pre_PrepareAsync(sendBlock, async (b) =>
            {
                var feeResult = await ProcessTransferFee(b as SendTransferBlock);
                return feeResult.block;
            }))
            {
                result.ResultCode = APIResultCodes.Success;
            }
            else
            {
                result.ResultCode = APIResultCodes.UnableToSendToConsensusNetwork;
            }

            return result;
        }

        public Task<AuthorizationAPIResult> SendExchangeTransfer(ExchangingBlock block)
        {
            return SendTransfer(block);
        }

        public async Task<AuthorizationAPIResult> ReceiveTransfer(ReceiveTransferBlock receiveBlock)
        {
            var result = new AuthorizationAPIResult();

            if (!await Pre_PrepareAsync(receiveBlock))
            {
                result.ResultCode = APIResultCodes.UnableToSendToConsensusNetwork;
            }
            else
            {
                result.ResultCode = APIResultCodes.Success;
            }
            return result;
        }

        public async Task<AuthorizationAPIResult> ImportAccount(ImportAccountBlock block)
        {
            var result = new AuthorizationAPIResult();

            if (!await Pre_PrepareAsync(block))
            {
                result.ResultCode = APIResultCodes.UnableToSendToConsensusNetwork;
                return result;
            }
            else
            {
                result.ResultCode = APIResultCodes.Success;
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

            if (!await Pre_PrepareAsync(tokenBlock, async (b) =>
            {
                var feeResult = await ProcessTokenGenerationFee(b as TokenGenesisBlock);
                return feeResult.block;
            }))
            {
                result.ResultCode = APIResultCodes.UnableToSendToConsensusNetwork;
            }
            else
            {
                result.ResultCode = APIResultCodes.Success;
            }
            return result;
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
            var result = reqOrder.VerifySignature(reqOrder.AccountID);
            if (!result
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

        async Task<(APIResultCodes result, TransactionBlock block)> ProcessTransferFee(SendTransferBlock sendBlock)
        {
            // TO DO: handle all token balances, not just LYRA
            if(sendBlock is ExchangingBlock)
            {
                if(sendBlock.Fee != ExchangingBlock.FEE)
                    return (APIResultCodes.InvalidFeeAmount, null);
            }
            else if (sendBlock.Fee != _serviceAccount.GetLastServiceBlock().TransferFee)
                return (APIResultCodes.InvalidFeeAmount, null);

            return await ProcessFee(sendBlock.Hash, sendBlock.Fee);
        }

        async Task<(APIResultCodes result, TransactionBlock block)> ProcessTokenGenerationFee(TokenGenesisBlock tokenBlock)
        {
            if (tokenBlock.Fee != _serviceAccount.GetLastServiceBlock().TokenGenerationFee)
                return (APIResultCodes.InvalidFeeAmount, null);

            return await ProcessFee(tokenBlock.Hash, tokenBlock.Fee);
        }

        private async Task<(APIResultCodes result, TransactionBlock block)> ProcessFee(string source, decimal fee)
        {
            var callresult = APIResultCodes.Success;
            TransactionBlock blockresult = null;

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
                receiveBlock.InitializeBlock(null, _serviceAccount.PrivateKey, _serviceAccount.NetworkId, AccountId: _serviceAccount.AccountId);

                //var authorizer = GrainFactory.GetGrain<IAuthorizer>(Guid.NewGuid(), "Lyra.Authorizer.Authorizers.NewAccountAuthorizer");
                //callresult = await authorizer.Authorize(receiveBlock);
                blockresult = receiveBlock;
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
                receiveBlock.InitializeBlock(latestBlock, _serviceAccount.PrivateKey, _serviceAccount.NetworkId, AccountId: _serviceAccount.AccountId);

                //var authorizer = GrainFactory.GetGrain<IAuthorizer>(Guid.NewGuid(), "Lyra.Authorizer.Authorizers.ReceiveTransferAuthorizer");
                //callresult = await authorizer.Authorize(receiveBlock);
                blockresult = receiveBlock;
            }

            //receiveBlock.Signature = Signatures.GetSignature(_serviceAccount.PrivateKey, receiveBlock.Hash);
            return (callresult, blockresult);
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
