using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;
using Lyra.Core.Blocks.Fees;
using Lyra.Core.Accounts.Node;
using Lyra.Core.API;

using Newtonsoft.Json;
using Lyra.Node.Authorizers;


namespace Lyra.Node
{
    public class RPCMethods : INodeAPI
    {
        readonly ServiceAccount _serviceAccount;
        readonly IAccountCollection _accountCollection;
        readonly TradeMatchEngine _TradeMatchEngine;
        //readonly Authorizer _authorizer;

        public RPCMethods(ServiceAccount serviceAccount, IAccountCollection accountCollection, TradeMatchEngine tradeMatchEngine)
        {
            _serviceAccount = serviceAccount;
            _accountCollection = accountCollection;
            _TradeMatchEngine = tradeMatchEngine;
            //_authorizer = new Authorizer(_serviceAccount, _accountCollection);
        }

        public async Task<AccountHeightAPIResult> GetSyncHeight()
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
                result.ResultMessage = e.Message;
            }
            return result;
        }

        //public async Task<int> GetSyncHeightAsync()
        //{
        //    return await Task.Run(() => _serviceAccount.GetLatestBlock().Index);
        //}

        public async Task<AccountHeightAPIResult> GetAccountHeight(string AccountId, string Signature)
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
                result.ResultMessage = e.Message;
            }
            return result;

        }

        //public async Task<int> GetAccountHeightAsync(string AccountId)
        //{
        //    return await Task.Run(
        //        () =>
        //        {
        //            if (_accountCollection.AccountExists(AccountId))
        //                return _accountCollection.FindLatestBlock(AccountId).Index;
        //            else
        //                return 0;
        //        }
        //    );
        //}

        public async Task<BlockAPIResult> GetBlockByIndex(string AccountId, int Index, string Signature)
        {
            var result = new BlockAPIResult();

            try
            {
                if (_accountCollection.AccountExists(AccountId))
                {
                    var block = _accountCollection.FindBlockByIndex(AccountId, Index);
                    if (block != null)
                    {
                        result.SetBlock(block);
                        result.ResultBlockType = block.GetBlockType();
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
                result.ResultMessage = e.Message;
            }

            return result;
        }

        public async Task<BlockAPIResult> GetBlockByHash(string AccountId, string Hash, string Signature)
        {
            var result = new BlockAPIResult();

            try
            {
                if (!_accountCollection.AccountExists(AccountId))
                    result.ResultCode = APIResultCodes.AccountDoesNotExist;

                var block = _accountCollection.FindBlockByHash(AccountId, Hash);
                if (block != null)
                {
                    result.SetBlock(block);
                    result.ResultBlockType = block.GetBlockType();
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                    result.ResultCode = APIResultCodes.BlockNotFound;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetBlock(Hash): " + e.Message);
                result.ResultCode = APIResultCodes.UnknownError;
                result.ResultMessage = e.Message;
            }

            return result;
        }

        public async Task<NonFungibleListAPIResult> GetNonFungibleTokens(string AccountId, string Signature)
        {
            var result = new NonFungibleListAPIResult();

            try
            {
                if (!_accountCollection.AccountExists(AccountId))
                    result.ResultCode = APIResultCodes.AccountDoesNotExist;

                var list = _accountCollection.GetNonFungibleTokens(AccountId);
                if (list != null)
                {
                    result.SetList(list);
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                    result.ResultCode = APIResultCodes.NoNonFungibleTokensFound;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetNonFungibleTokens: " + e.Message);
                result.ResultCode = APIResultCodes.UnknownError;
                result.ResultMessage = e.Message;
            }

            return result;
        }



        public async Task<BlockAPIResult> GetTokenGenesisBlock(string AccountId, string TokenTicker, string Signature)
        {
            var result = new BlockAPIResult();

            try
            {
                //if (!_accountCollection.AccountExists(AccountId))
                //    result.ResultCode = APIResultCodes.AccountDoesNotExist;

                var block = _accountCollection.FindTokenGenesisBlock(null, TokenTicker);
                if (block != null)
                {
                    result.SetBlock(block);
                    result.ResultBlockType = block.GetBlockType();
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                    result.ResultCode = APIResultCodes.TokenGenesisBlockNotFound;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetTokenTokenGenesisBlock: " + e.Message);
                result.ResultCode = APIResultCodes.UnknownError;
                result.ResultMessage = e.Message;
            }

            return result;
        }

        public async Task<ActiveTradeOrdersAPIResult> GetActiveTradeOrders(string AccountId, string SellToken, string BuyToken, TradeOrderListTypes OrderType, string Signature)
        {
            var result = new ActiveTradeOrdersAPIResult();

            try
            {
                if (!_accountCollection.AccountExists(AccountId))
                    result.ResultCode = APIResultCodes.AccountDoesNotExist;

                var list = _TradeMatchEngine.GetActiveTradeOrders(SellToken, BuyToken, OrderType);
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

        public async Task<BlockAPIResult> GetLastServiceBlock(string AccountId, string Signature)
        {
            var result = new BlockAPIResult();

            try
            {
                if (!_accountCollection.AccountExists(AccountId))
                    result.ResultCode = APIResultCodes.AccountDoesNotExist;

                var block = _serviceAccount.GetLastServiceBlock();
                if (block != null)
                {
                    result.SetBlock(block);
                    result.ResultBlockType = block.GetBlockType();
                    result.ResultCode = APIResultCodes.Success;
                }
                else
                    result.ResultCode = APIResultCodes.ServiceBlockNotFound;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in GetLastServiceBlock: " + e.Message);
                result.ResultCode = APIResultCodes.UnknownError;
                result.ResultMessage = e.Message;
            }

            return result;
        }

        // we need both the send block itself and its previous block in the chain to get the transaction amount,
        // so we return the value tuple containing both blocks
        public async Task<NewTransferAPIResult> LookForNewTransfer(string AccountId, string Signature)
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
                transfer_info.ResultMessage = e.Message;
            }
            return transfer_info;
        }

        // we need both the send block itself and its previous block in the chain to get the transaction amount,
        // so we return the value tuple containing both blocks
        public async Task<TradeAPIResult> LookForNewTrade(string AccountId, string BuyTokenCode, string SellTokenCode, string Signature)
        {
            var result = new TradeAPIResult();
            try
            {
                if (!_accountCollection.AccountExists(AccountId))
                    result.ResultCode = APIResultCodes.AccountDoesNotExist;

                var trade = _accountCollection.FindUnexecutedTrade(AccountId, BuyTokenCode, SellTokenCode);

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
        #region Authorization methods 

        public async Task<AuthorizationAPIResult> OpenAccountWithGenesis(LyraTokenGenesisBlock OpenTokenGenesisBlock)
        {
            // Send to the authorizations sample - TO DO
            // For now, implementation for single-node testnet only
            // ***
            // to do - sign by authorizer and send to the outgoing queue
            var result = new AuthorizationAPIResult();

            try
            {
                var authorizer = new GenesisAuthorizer(_serviceAccount, _accountCollection);

                result.ResultCode = authorizer.Authorize(ref OpenTokenGenesisBlock);
                if (result.ResultCode == APIResultCodes.Success)
                {
                    result.Authorizations = OpenTokenGenesisBlock.Authorizations;
                    result.ServiceHash = OpenTokenGenesisBlock.ServiceHash;
                }
                else
                {
                    Console.WriteLine(OpenTokenGenesisBlock.Print());
                }

            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in OpenAccountWithGenesis: " + e.Message);
                result.ResultCode = APIResultCodes.ExceptionInOpenAccountWithGenesis;
                result.ResultMessage = e.Message;
            }

            return result;
        }

        public async Task<AuthorizationAPIResult> ReceiveTransferAndOpenAccount(OpenWithReceiveTransferBlock OpenReceiveBlock)
        {
            // Send to the authorizations sample - TO DO
            // For now, implementation for single-node testnet only
            // ***
            // to do - sign by authorizer and send to the outgoing queue
            var result = new AuthorizationAPIResult();

            try
            {
                var authorizer = new NewAccountAuthorizer(_serviceAccount, _accountCollection);
                result.ResultCode = authorizer.Authorize(ref OpenReceiveBlock);
                if (result.ResultCode != APIResultCodes.Success)
                    return result;

                result.Authorizations = OpenReceiveBlock.Authorizations;
                result.ServiceHash = OpenReceiveBlock.ServiceHash;
                result.ResultCode = APIResultCodes.Success;

            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in ReceiveTransferAndOpenAccount: " + e.Message);
                result.ResultCode = APIResultCodes.ExceptionInReceiveTransferAndOpenAccount;
                result.ResultMessage = e.Message;
            }
            return result;

        }

        public async Task<AuthorizationAPIResult> OpenAccountWithImport(OpenAccountWithImportBlock block)
        {
            var result = new AuthorizationAPIResult();

            try
            {
                var authorizer = new NewAccountWithImportAuthorizer(_serviceAccount, _accountCollection);
                result.ResultCode = authorizer.Authorize(ref block);
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
                result.ResultMessage = e.Message;
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
                var authorizer = new SendTransferAuthorizer(_serviceAccount, _accountCollection);
                result.ResultCode = authorizer.Authorize(ref sendBlock);
                if (result.ResultCode != APIResultCodes.Success)
                {
                    Console.WriteLine("Authorization failed" + result.ResultCode.ToString());
                    //Console.WriteLine(JsonConvert.SerializeObject(sendBlock));
                    //Console.WriteLine(sendBlock.CalculateHash());
                    result.ResultMessage = JsonConvert.SerializeObject(sendBlock);
                    return result;
                }

                var r = await ProcessTransferFee(sendBlock);
                if (r != APIResultCodes.Success)
                    Console.WriteLine("Error in SendTransfer->ProcessTransferFee: " + r.ToString());

                result.Authorizations = sendBlock.Authorizations;
                result.ServiceHash = sendBlock.ServiceHash;
                result.ResultCode = APIResultCodes.Success;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in SendTransfer: " + e.Message);
                result.ResultCode = APIResultCodes.ExceptionInSendTransfer;
                result.ResultMessage = e.Message;
            }
            return result;

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

                var authorizer = new ReceiveTransferAuthorizer(_serviceAccount, _accountCollection);
                result.ResultCode = authorizer.Authorize(ref receiveBlock);

                if (result.ResultCode != APIResultCodes.Success)
                {
                    result.ResultMessage = JsonConvert.SerializeObject(receiveBlock);
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
                result.ResultMessage = e.Message;
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

                var authorizer = new ImportAccountAuthorizer(_serviceAccount, _accountCollection);
                result.ResultCode = authorizer.Authorize(ref block);

                if (result.ResultCode != APIResultCodes.Success)
                {
                    result.ResultMessage = JsonConvert.SerializeObject(block);
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
                result.ResultMessage = e.Message;
            }
            return result;

        }

        public async Task<AuthorizationAPIResult> CreateToken(TokenGenesisBlock tokenBlock)
        {
            var result = new AuthorizationAPIResult();

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
                result.ResultCode = authorizer.Authorize(ref tokenBlock);
                if (result.ResultCode != APIResultCodes.Success)
                {
                    result.ResultMessage = JsonConvert.SerializeObject(tokenBlock);
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
                result.ResultMessage = e.Message;
            }

            return result;
        }

        public async Task<TradeOrderAuthorizationAPIResult> TradeOrder(TradeOrderBlock block)
        {
            var result = new TradeOrderAuthorizationAPIResult();

            try
            {
                var authorizer = new TradeOrderAuthorizer(_serviceAccount, _accountCollection, _TradeMatchEngine);
                result.ResultCode = authorizer.Authorize(ref block);

                if (result.ResultCode == APIResultCodes.TradeOrderMatchFound)
                {
                    Console.WriteLine("Match Found");
                    result.SetBlock(authorizer.MatchTradeBlock);
                    return result;
                }
                else
                if (result.ResultCode != APIResultCodes.Success)
                {
                    //Console.WriteLine("Authorization failed" + result.ResultCode.ToString());
                    //Console.WriteLine(JsonConvert.SerializeObject(block));
                    //Console.WriteLine(block.Print());
                    //Console.WriteLine(block.CalculateHash());
                    result.ResultMessage = JsonConvert.SerializeObject(block);
                    return result;
                }
                else
                {

                    // TO DO - the fee probably should be paid here:
                    // 1) to allow multiple executions per single order
                    // 2) to prevent DoS attacks
                    //var r = await ProcessTokenGenerationFee(block);
                    //if (r != APIResultCodes.Success)
                    //    Console.WriteLine("Error in CreateToken->ProcessTokenGenerationFee: " + r.ToString());

                    result.Authorizations = block.Authorizations;
                    result.ServiceHash = block.ServiceHash;
                    result.ResultCode = APIResultCodes.Success;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in TradeOrder: " + e.Message);
                result.ResultCode = APIResultCodes.ExceptionInTradeOrderAuthorizer;
                result.ResultMessage = e.Message;
            }

            return result;
        }

        public async Task<AuthorizationAPIResult> Trade(TradeBlock block)
        {
            var result = new AuthorizationAPIResult();

            try
            {
                var authorizer = new TradeAuthorizer(_serviceAccount, _accountCollection, _TradeMatchEngine);
                result.ResultCode = authorizer.Authorize(ref block);
                if (result.ResultCode != APIResultCodes.Success)
                {
                    result.ResultMessage = JsonConvert.SerializeObject(block);
                    return result;
                }

                //_accountCollection.AddBlock(block);
                //var r = await ProcessTokenGenerationFee(tokenBlock);
                //if (r != APIResultCodes.Success)
                //    Console.WriteLine("Error in CreateToken->ProcessTokenGenerationFee: " + r.ToString());

                result.Authorizations = block.Authorizations;
                result.ServiceHash = block.ServiceHash;
                result.ResultCode = APIResultCodes.Success;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in Trade: " + e.Message);
                result.ResultCode = APIResultCodes.ExceptionInTradeAuthorizer;
                result.ResultMessage = e.Message;
            }

            return result;
        }

        public async Task<AuthorizationAPIResult> ExecuteTradeOrder(ExecuteTradeOrderBlock block)
        {
            var result = new AuthorizationAPIResult();

            try
            {
                var authorizer = new ExecuteTradeOrderAuthorizer(_serviceAccount, _accountCollection, _TradeMatchEngine);
                result.ResultCode = authorizer.Authorize(ref block);
                if (result.ResultCode != APIResultCodes.Success)
                {
                    result.ResultMessage = JsonConvert.SerializeObject(block);
                    return result;
                }

                result.Authorizations = block.Authorizations;
                result.ServiceHash = block.ServiceHash;
                result.ResultCode = APIResultCodes.Success;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in ExecuteTradeOrder: " + e.Message);
                result.ResultCode = APIResultCodes.ExceptionInExecuteTradeOrderAuthorizer;
                result.ResultMessage = e.Message;
            }

            return result;
        }

        public async Task<AuthorizationAPIResult> CancelTradeOrder(CancelTradeOrderBlock block)
        {
            var result = new AuthorizationAPIResult();

            try
            {
                var authorizer = new CancelTradeOrderAuthorizer(_serviceAccount, _accountCollection, _TradeMatchEngine);
                result.ResultCode = authorizer.Authorize(ref block);
                if (result.ResultCode != APIResultCodes.Success)
                {
                    result.ResultMessage = JsonConvert.SerializeObject(block);
                    return result;
                }

                //var r = await ProcessTokenGenerationFee(tokenBlock);
                //if (r != APIResultCodes.Success)
                //    Console.WriteLine("Error in CreateToken->ProcessTokenGenerationFee: " + r.ToString());

                result.Authorizations = block.Authorizations;
                result.ServiceHash = block.ServiceHash;
                result.ResultCode = APIResultCodes.Success;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in CancelTradeOrder: " + e.Message);
                result.ResultCode = APIResultCodes.ExceptionInCancelTradeOrderAuthorizer;
                result.ResultMessage = e.Message;
            }

            return result;
        }
        #endregion Authorization methods 

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

        async Task<APIResultCodes> ProcessFee(string source, decimal fee)
        {
            APIResultCodes result;

            if (!_accountCollection.AccountExists(_serviceAccount.AccountId))
            {
                var openBlock = new OpenWithReceiveFeeBlock
                {
                    AccountType = AccountTypes.Standard,
                    AccountID = _serviceAccount.AccountId,
                    ServiceHash = string.Empty,
                    SourceHash = source,
                    Balances = new Dictionary<string, decimal>(),
                    Fee = 0,
                    FeeType = AuthorizationFeeTypes.NoFee
                };

                openBlock.Balances.Add(TokenGenesisBlock.LYRA_TICKER_CODE, fee);
                openBlock.InitializeBlock(null, _serviceAccount.PrivateKey, _serviceAccount.NetworkId);
                //openBlock.Signature = Signatures.GetSignature(_serviceAccount.PrivateKey, openBlock.Hash);
                var callresult = await ReceiveTransferAndOpenAccount(openBlock);
                result = callresult.ResultCode;
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

                TransactionBlock latestBlock = _accountCollection.FindLatestBlock(_serviceAccount.AccountId);
                decimal newBalance = latestBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE] + fee;
                receiveBlock.Balances.Add(TokenGenesisBlock.LYRA_TICKER_CODE, newBalance);
                receiveBlock.InitializeBlock(latestBlock, _serviceAccount.PrivateKey, _serviceAccount.NetworkId);

                //receiveBlock.Signature = Signatures.GetSignature(_serviceAccount.PrivateKey, receiveBlock.Hash);

                var callresult = await ReceiveTransfer(receiveBlock);
                result = callresult.ResultCode;
            }
            return result;
        }

        #endregion



    }
}
