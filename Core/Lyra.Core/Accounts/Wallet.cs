using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lyra.Core.Blocks;
using Lyra.Core.Cryptography;
using Lyra.Core.API;
using Newtonsoft.Json;
using System.Diagnostics;
using Lyra.Shared;
using System.Linq;
using Akka.Util;
using System.IO;

namespace Lyra.Core.Accounts
{
    /// <summary>
    /// the wallet works single threaded. if do multiple threaded operations, user muse sync before call any block creating API.
    /// </summary>
    public class Wallet
    {
        public string AccountName { get; }
        public string PrivateKey => _store.PrivateKey;

        public string AccountId => _store.AccountId;

        public string NetworkId => _store.NetworkId;

        public string VoteFor { get => _store.VoteFor; set => _store.VoteFor = value; }

        // to do 
        // 1) move rpcclient to CLI
        // 2) create interface and reference rpcclient by interface here, use the same interface in server and REST API client (Shopify app)  
        //private RPCClient _rpcClient = null;
        private IAccountDatabase _store;
        private LyraRestClient _rpcClient = null;

        private long SyncHeight = -1;
        private string SyncHash = string.Empty;

        public decimal TransferFee = 0; // in atomic units
        public decimal TokenGenerationFee = 0; // in atomic units
        public decimal TradeFee = 0; // in atomic units
        public bool AccountAlreadyImported = false;

        private TransactionBlock _lastTransactionBlock;

        public decimal MainBalance
        {
            get
            {
                if (_lastTransactionBlock != null && _lastTransactionBlock.Balances.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
                    return _lastTransactionBlock.Balances[LyraGlobal.OFFICIALTICKERCODE];
                else
                    return 0m;                    
            }
        }

        private Wallet(IAccountDatabase storage, string name, LyraRestClient rpcClient = null)
        {
            _store = storage;
            AccountName = name;
            _rpcClient = rpcClient;
        }

        public static string GetFullFolderName(string NetworkId, string FolderName)
        {
            return $"{Utilities.GetLyraDataDir(NetworkId, LyraGlobal.OFFICIALDOMAIN)}{Utilities.PathSeperator}{FolderName}{Utilities.PathSeperator}";
        }

        public static Wallet Open(IAccountDatabase store, string name, string password, LyraRestClient rpcClient = null)
        {
            var wallet = new Wallet(store, name, rpcClient);
            store.Open(name, password);
            return wallet;
        }

        public static void Create(IAccountDatabase store, string name, string password, string networkId)
        {
            (var privateKey, var publicKey) = Signatures.GenerateWallet();
            Create(store, name, password, networkId, privateKey);
        }

        public static Wallet Create(IAccountDatabase store, string name, string password, string networkId, string privateKey)
        {
            if (!Signatures.ValidatePrivateKey(privateKey))
            {
                throw new InvalidDataException("Failed to create wallet: invalid private key");
            }
            var wallet = new Wallet(store, name);
            var accountId = Signatures.GetAccountIdFromPrivateKey(privateKey);
            if (!store.Create(name, password, networkId, privateKey, accountId, ""))
                throw new Exception("Can't create wallet in storage.");
            return wallet;
        }
        // one-time "manual" sync up with the node 
        public async Task<APIResultCodes> Sync(LyraRestClient RPCClient)
        {
            if (RPCClient != null)
                _rpcClient = RPCClient;

            if (_rpcClient != null)
            {
                var result = await SyncServiceChain();
                if (result != APIResultCodes.Success)
                    return result;

                var blockResult = await _rpcClient.GetLastBlock(AccountId);
                if (blockResult.ResultCode == APIResultCodes.Success)
                {
                    _lastTransactionBlock = blockResult.GetBlock() as TransactionBlock;
                }

                result = await SyncIncomingTransfers();

                return result;
            }
            else
                return APIResultCodes.NoRPCServerConnection;
        }

        public string SignAPICallAsync()
        {
            return Signatures.GetSignature(PrivateKey, SyncHash, AccountId);
        }

        public TransactionBlock GetLatestBlock()
        {
            return _lastTransactionBlock;
        }

        public long GetLocalAccountHeight()
        {
            var lastTrans = GetLatestBlock();
            if (lastTrans != null)
                return lastTrans.Height;
            else
                return 0;
        }

        public async Task<List<string>> GetTokenNames(string keyword)
        {
            if (_rpcClient == null)
                return new List<string>();

            var result = await _rpcClient.GetTokenNames(AccountId, SignAPICallAsync(), keyword);
            if (result.ResultCode == APIResultCodes.Success)
                return result.Entities;
            else
                throw new Exception("Error get Token names: " + result.ResultCode.ToString());
        }

        private async Task<APIResultCodes> SyncServiceChain()
        {
            try
            {
                while (true)
                {
                    var result = await _rpcClient.GetSyncHeight();
                    if (result.ResultCode != APIResultCodes.Success)
                        return result.ResultCode;

                    if (NetworkId != result.NetworkId)
                        return APIResultCodes.InvalidNetworkId;

                    SyncHeight = result.Height;
                    SyncHash = result.SyncHash;

                    if (SyncHeight > 0)
                        break;
                    else
                        await Task.Delay(3000);
                }


                if (TransferFee == 0 || TokenGenerationFee == 0 || TradeFee == 0)
                {
                    var blockresult = await _rpcClient.GetLastServiceBlock();

                    if (blockresult.ResultCode != APIResultCodes.Success)
                        return blockresult.ResultCode;
                    ServiceBlock lastServiceBlock = blockresult.GetBlock() as ServiceBlock;
                    TransferFee = lastServiceBlock.TransferFee;
                    TokenGenerationFee = lastServiceBlock.TokenGenerationFee;
                    TradeFee = lastServiceBlock.TradeFee;
                    Console.WriteLine($"Last Service Block Received {lastServiceBlock.Height}");
                    Console.WriteLine(string.Format("Transfer Fee: {0} ", lastServiceBlock.TransferFee));
                    Console.WriteLine(string.Format("Token Generation Fee: {0} ", lastServiceBlock.TokenGenerationFee));
                    Console.WriteLine(string.Format("Trade Fee: {0} ", lastServiceBlock.TradeFee));
                    Console.Write(string.Format("{0}> ", AccountName));
                }
                return APIResultCodes.Success;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in SyncServiceChain(): " + e.Message);
                return APIResultCodes.UnknownError;
            }
        }

        private async Task<APIResultCodes> SyncIncomingTransfers()
        {
            try
            {
                var lookup_result = await _rpcClient.LookForNewTransfer(AccountId, SignAPICallAsync());
                int max_counter = 0;

                while (lookup_result.Successful() && max_counter < 100) // we don't want to enter an endless loop...
                {
                    max_counter++;

                    Console.WriteLine($"Received new transaction, sending request for settlement...");

                    var receive_result = await ReceiveTransfer(lookup_result);
                    if (!receive_result.Successful())
                        return receive_result.ResultCode;

                    lookup_result = await _rpcClient.LookForNewTransfer(AccountId, SignAPICallAsync());
                }

                // the fact that do one sent us any money does not mean this call failed...
                if (lookup_result.ResultCode == APIResultCodes.NoNewTransferFound)
                    return APIResultCodes.Success;

                if (lookup_result.ResultCode == APIResultCodes.AccountAlreadyImported)
                {
                    Console.WriteLine($"This account was imported (merged) to another account.");
                    AccountAlreadyImported = true;
                    return lookup_result.ResultCode;
                }

                return lookup_result.ResultCode;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in SyncIncomingTransfers(): " + e.Message);
                return APIResultCodes.UnknownError;
            }
        }

        public async Task<APIResultCodes> SyncNodeFees()
        {
            while (true)
            {
                var fbsResult = await _rpcClient.LookForNewFees(AccountId, SignAPICallAsync());
                if (fbsResult.ResultCode != APIResultCodes.Success || fbsResult.pendingFees == null)
                    return fbsResult.ResultCode;

                var feesEndSbResult = await _rpcClient.GetServiceBlockByIndex("Service", fbsResult.pendingFees.ServiceBlockEndHeight);
                if (feesEndSbResult.ResultCode != APIResultCodes.Success)
                    return feesEndSbResult.ResultCode;

                var feesEndSb = feesEndSbResult.GetBlock() as ServiceBlock;

                TransactionBlock latestBlock = GetLatestBlock();
                var receiveBlock = new ReceiveAuthorizerFeeBlock
                {
                    AccountID = AccountId,
                    VoteFor = VoteFor,
                    ServiceHash = await getLastServiceBlockHashAsync(),
                    SourceHash = feesEndSb.Hash,
                    ServiceBlockStartHeight = fbsResult.pendingFees.ServiceBlockStartHeight,
                    ServiceBlockEndHeight = fbsResult.pendingFees.ServiceBlockEndHeight,
                    Balances = latestBlock.Balances,
                    Fee = 0,
                    FeeType = AuthorizationFeeTypes.NoFee,
                    NonFungibleToken = null
                };

                if(receiveBlock.Balances.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
                {
                    receiveBlock.Balances[LyraGlobal.OFFICIALTICKERCODE] += fbsResult.pendingFees.TotalFees.ToBalanceLong();
                }
                else
                {
                    receiveBlock.Balances.Add(LyraGlobal.OFFICIALTICKERCODE, fbsResult.pendingFees.TotalFees.ToBalanceLong());
                }

                receiveBlock.InitializeBlock(latestBlock, PrivateKey, AccountId);

                if (!receiveBlock.ValidateTransaction(latestBlock))
                    throw new ApplicationException("ValidateTransaction failed");

                //receiveBlock.Signature = Signatures.GetSignature(PrivateKey, receiveBlock.Hash);

                var result = await _rpcClient.ReceiveFee(receiveBlock);

                if (result.ResultCode == APIResultCodes.BlockSignatureValidationFailed)
                {
                    Console.WriteLine($"BlockSignatureValidationFailed");
                    Console.WriteLine("Error Message: ");
                    Console.WriteLine(result.ResultMessage);
                    Console.WriteLine("Local Block: ");
                    Console.WriteLine(receiveBlock.Print());
                }
                else if (result.ResultCode != APIResultCodes.Success)
                {
                    Console.WriteLine($"Failed to authorize receive fee block with error code: {result.ResultCode}");
                    Console.WriteLine("Error Message: " + result.ResultMessage);
                }
                else
                {
                    _lastTransactionBlock = receiveBlock;

                    Console.WriteLine($"Receive fee block has been authorized successfully");
                    Console.WriteLine("Balance: " + await GetDisplayBalancesAsync());
                }
                Console.Write(string.Format("{0}> ", AccountName));
                return result.ResultCode;
            }
        }

        #region Reward Trade Processing

        public async Task<TradeAPIResult> LookForNewTrade(string BuyTokenCode, string SellTokenCode)
        {
            try
            {
                var lookup_result = await _rpcClient.LookForNewTrade(AccountId, BuyTokenCode, SellTokenCode, SignAPICallAsync());

                return lookup_result;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in LookForNewTrade(): " + e.Message);
                return new TradeAPIResult() { ResultCode = APIResultCodes.UnknownError, ResultMessage = e.Message };
            }
        }

        public async Task<AuthorizationAPIResult> RedeemRewards(string reward_token_code, decimal discount_amount)
        {
            var trade_orders = await GetActiveTradeOrders("*", reward_token_code, TradeOrderListTypes.SellOnly);
            if (trade_orders.ResultCode != APIResultCodes.Success)
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.TradeOrderNotFound, ResultMessage = "No matching order for redemption found." };

            // let's try the first order in the list, there should be only one matching order anyway
            var sell_order = trade_orders.GetList()[0];

            var redeem_block = new TradeBlock
            {
                AccountID = AccountId,
                SellTokenCode = sell_order.BuyTokenCode,
                BuyTokenCode = sell_order.SellTokenCode,
                BuyAmount = discount_amount,
                Fee = 0,
                FeeType = AuthorizationFeeTypes.NoFee,
                SellAmount = discount_amount * sell_order.Price,
                Balances = new Dictionary<string, long>(),
                DestinationAccountId = sell_order.AccountID,
                TradeOrderId = sell_order.Hash
            };

            var redeem_result = await Trade(redeem_block);
            return redeem_result;
        }


        public async Task<AuthorizationAPIResult> ExecuteSellOrder(TradeBlock trade, TradeOrderBlock order, NonFungibleToken nonfungible_token = null)
        {
            TransactionBlock previousBlock = GetLatestBlock();
            if (previousBlock == null)
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.PreviousBlockNotFound };

            //int sell_precision = await FindTokenPrecision(trade.BuyTokenCode);
            //if (sell_precision < 0)
            //    return new AuthorizationAPIResult() { ResultCode = APIResultCodes.TokenGenesisBlockNotFound };

            if (order == null)
            {
                order = await GetBlockByHash(trade.TradeOrderId) as TradeOrderBlock;
                if (order == null)
                    return new AuthorizationAPIResult() { ResultCode = APIResultCodes.TradeOrderNotFound };
            }

            if (trade.BuyAmount > order.TradeAmount)
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.InvalidTradeAmount };

            var balance_change = trade.BuyAmount;

            var fee = TradeFee;
            var fee_type = AuthorizationFeeTypes.Regular;
            if (order.CoverAnotherTradersFee)
            {
                fee = TradeFee * 2;
                fee_type = AuthorizationFeeTypes.BothParties;
            }
            else
            if (order.AnotherTraderWillCoverFee)
            {
                fee = 0;
                fee_type = AuthorizationFeeTypes.NoFee;
            }

            if (trade.BuyTokenCode == LyraGlobal.OFFICIALTICKERCODE)
                balance_change += fee;

            // see if we have enough tokens
            if (previousBlock.Balances[trade.BuyTokenCode] < balance_change)
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.InsufficientFunds };

            // see if we have enough LYR to pay the transfer fee
            if (fee > 0)
            {
                if (trade.BuyTokenCode != LyraGlobal.OFFICIALTICKERCODE)
                    if (previousBlock.Balances[LyraGlobal.OFFICIALTICKERCODE] < fee)
                        return new AuthorizationAPIResult() { ResultCode = APIResultCodes.InsufficientFunds };
            }

            var execute_block = new ExecuteTradeOrderBlock()
            {
                AccountID = AccountId,
                DestinationAccountId = trade.AccountID,
                Balances = new Dictionary<string, long>(),
                TradeId = trade.Hash,
                TradeOrderId = trade.TradeOrderId,
                SellTokenCode = trade.BuyTokenCode,
                SellAmount = trade.BuyAmount,
                Fee = fee,
                FeeType = fee_type,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE
            };

            // The funds were previously locked by the sell order so we only put the difference between the previously locked amount and the actual trade amount
            var final_balance_change = order.TradeAmount - balance_change;

            // If the trade amount fully covers the order, there is no change in the balance as the entire Tx amount was previously "locked" by the original order 
            execute_block.Balances.Add(execute_block.SellTokenCode, (previousBlock.Balances[execute_block.SellTokenCode].ToBalanceDecimal() - final_balance_change).ToBalanceLong());

            // for customer tokens, we pay fee in LYR (unless they are accepted by authorizers as a fee - TO DO)
            if (fee > 0)
            {
                if (execute_block.SellTokenCode != LyraGlobal.OFFICIALTICKERCODE)
                    execute_block.Balances.Add(LyraGlobal.OFFICIALTICKERCODE, (previousBlock.Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal() - fee).ToBalanceLong());
            }

            // transfer unchanged token balances from the previous block
            foreach (var balance in previousBlock.Balances)
                if (!(execute_block.Balances.ContainsKey(balance.Key)))
                    execute_block.Balances.Add(balance.Key, balance.Value);

            if (nonfungible_token != null)
            {
                if (nonfungible_token.Denomination != execute_block.SellAmount)
                    return new AuthorizationAPIResult() { ResultCode = APIResultCodes.InvalidNonFungibleAmount };

                if (nonfungible_token.TokenCode != execute_block.SellTokenCode)
                    return new AuthorizationAPIResult() { ResultCode = APIResultCodes.InvalidNonFungibleTokenCode };

                //execute_block.NonFungibleTokens = new List<INonFungibleToken>();
                //execute_block.NonFungibleTokens.Add(nonfungible_token);
                execute_block.NonFungibleToken = nonfungible_token;
            }

            execute_block.ServiceHash = await getLastServiceBlockHashAsync();

            execute_block.InitializeBlock(previousBlock, PrivateKey, AccountId);

            // TO DO - override the trasanction validation method in ExecuteTradeBlock
            //if (!execute_block.ValidateTransaction(previousBlock))
            //    return new AuthorizationAPIResult() { ResultCode = APIResultCodes.SendTransactionValidationFailed };

            var result = await _rpcClient.ExecuteTradeOrder(execute_block);

            if (result.ResultCode == APIResultCodes.Success)
            {
                _lastTransactionBlock = execute_block;
            }
            else
            {
                // thsi is for debug purpose only
                if (result.ResultCode == APIResultCodes.BlockSignatureValidationFailed)
                    result.ResultMessage = execute_block.GetHashInput();
            }
            return result;
        }

        #endregion

        // launches a live wallet with auto-updates enabled
        //public void Launch(INodeAPI RPCClient)
        //{

        //    //_rpcClient = RPCClient;
        //    //timer1 = new Timer(async _ =>
        //    //    {
        //    //        if (timer_busy1)
        //    //            return;
        //    //        try
        //    //        {
        //    //            timer_busy1 = true;
        //    //            await Sync(_rpcClient);
        //    //        }
        //    //        finally
        //    //        {
        //    //            timer_busy1 = false;
        //    //        }
        //    //    },
        //    //     null, 500, 5000);
        //}

        public async Task<string> GetDisplayBalancesAsync()
        {
            string res = "0";
            TransactionBlock lastBlock = GetLatestBlock();
            if (lastBlock != null)
            {
                res = $"\n";
                foreach (var balance in lastBlock.Balances)
                {
                    //int precision = FindTokenPrecision(balance.Key).Result;

                    //res = res + string.Format(@"{0} {1}    ", balance.Value / Math.Pow(10, precision != -1?precision:0), balance.Key);
                    res += $"{balance.Value.ToBalanceDecimal()} {balance.Key}\n";
                }
                if (lastBlock.NonFungibleToken != null)
                {
                    var discount_token_genesis = await _rpcClient.GetTokenGenesisBlock(AccountId, lastBlock.NonFungibleToken.TokenCode, SignAPICallAsync());
                    if (discount_token_genesis != null)
                    {
                        var issuer_account_id = (discount_token_genesis.GetBlock() as TokenGenesisBlock).AccountID;
                        var decryptor = new ECC_DHA_AES_Encryptor();
                        string decrypted_redemption_code = decryptor.Decrypt(PrivateKey, issuer_account_id, lastBlock.NonFungibleToken.SerialNumber, lastBlock.NonFungibleToken.RedemptionCode);

                        res += $"Shopify Discount: {lastBlock.NonFungibleToken.Denomination.ToString("C")} Redemption Code: {decrypted_redemption_code}  \n";
                    }
                }
            }
            return res;
        }

        public async Task<(string name, decimal Denomination, string Redemption)> NonFungToStringAsync(NonFungibleToken nftoken)
        {
            var discount_token_genesis = await _rpcClient.GetTokenGenesisBlock(AccountId, nftoken.TokenCode, SignAPICallAsync());
            if (discount_token_genesis?.ResultCode == APIResultCodes.Success)
            {
                var tgb = discount_token_genesis.GetBlock() as TokenGenesisBlock;
                var issuer_account_id = tgb.AccountID;
                var decryptor = new ECC_DHA_AES_Encryptor();
                string decrypted_redemption_code = decryptor.Decrypt(PrivateKey, issuer_account_id, nftoken.SerialNumber, nftoken.RedemptionCode);

                return (tgb.Owner, nftoken.Denomination, decrypted_redemption_code);
                //res += $"Shopify Discount: {nftoken.Denomination.ToString("C")} Redemption Code: {decrypted_redemption_code}  \n";
            }
            else
                return (null, 0, null);
        }

        public int GetNumberOfNonZeroBalances()
        {
            int result = 0;
            var last = GetLatestBlock();
            if (last != null)
            {
                foreach (var balance in last.Balances)
                {
                    if (balance.Value > 0)
                        result++;
                }
            }
            return result;
        }

        public async Task<TransactionBlock> GetBlockByIndex(long Index)
        {
            var blockResult = await _rpcClient.GetBlockByIndex(AccountId, Index);
            if (blockResult.ResultCode == APIResultCodes.Success)
                return blockResult.GetBlock() as TransactionBlock;
            else
                return null;
        }

        public async Task<TransactionBlock> GetBlockByHash(string Hash)
        {
            var blockResult = await _rpcClient.GetBlockByHash(AccountId, Hash, SignAPICallAsync());
            if (blockResult.ResultCode == APIResultCodes.Success)
                return blockResult.GetBlock() as TransactionBlock;
            else
                return null;
        }

        //public async Task<AuthorizationAPIResult> ImportAccount(string ImportedAccountKey)
        //{
        //    TransactionBlock previousBlock = GetLatestBlock();
        //    if (previousBlock == null)
        //    {
        //        var import_block = new OpenAccountWithImportBlock
        //        {
        //            AccountID = AccountId,
        //            Balances = new Dictionary<string, long>(),
        //            //PaymentID = string.Empty,
        //            Fee = TransferFee,
        //            FeeCode = LyraGlobal.OFFICIALTICKERCODE,
        //            FeeType = AuthorizationFeeTypes.Regular
        //        };
        //    }
        //    else
        //    {
        //    }
        //}


        public async Task<AuthorizationAPIResult> Send(decimal Amount, string DestinationAccountId, string ticker = LyraGlobal.OFFICIALTICKERCODE, bool ToExchange = false)
        {
            // verify input
            AuthorizationAPIResult result;

            if(Amount <= 0)
            {
                result = new AuthorizationAPIResult { ResultCode = APIResultCodes.InvalidAmountToSend };
                return result;
            }

            if(!Signatures.ValidateAccountId(DestinationAccountId) || DestinationAccountId == AccountId)
            {
                result = new AuthorizationAPIResult { ResultCode = APIResultCodes.InvalidAccountId };
                return result;
            }

            var currentSvcBlock = await _rpcClient.GetLastServiceBlock();

            while (true)
            {
                result = await SendOnce(Amount, DestinationAccountId, ticker, ToExchange);
                if (result.ResultCode == APIResultCodes.ConsensusTimeout)
                {                    
                    bool viewChanged = false;
                    for (int i = 0; i < 30; i++)       // wait 30 seconds for consensus network to recovery
                    {
                        var nextSvcBlock = await _rpcClient.GetLastServiceBlock();
                        if (currentSvcBlock.ResultCode == APIResultCodes.Success &&
                            nextSvcBlock.ResultCode == APIResultCodes.Success &&
                            nextSvcBlock.GetBlock().Height > currentSvcBlock.GetBlock().Height)
                        {
                            viewChanged = true;
                            break;
                        }

                        await Task.Delay(1000);
                    }
                    if (viewChanged)
                        continue;
                    else
                        break;
                }
                else
                    break;
            }
            return result;
        }

        private async Task<AuthorizationAPIResult> SendOnce(decimal Amount, string DestinationAccountId, string ticker = LyraGlobal.OFFICIALTICKERCODE, bool ToExchange = false)
        {
            Trace.Assert(Amount > 0);
            if (Amount <= 0)
                throw new Exception("Amount must > 0");

            TransactionBlock previousBlock = GetLatestBlock();
            if (previousBlock == null)
            {
                //throw new ApplicationException("Previous block not found");
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.PreviousBlockNotFound };
            }

            //int precision = await FindTokenPrecision(ticker);
            //if (precision < 0)
            //{

            //    return new AuthorizationAPIResult() { ResultCode = APIResultCodes.TokenGenesisBlockNotFound };
            //}

            //long atomicamount = (long)(Amount * (decimal)Math.Pow(10, precision));
            var balance_change = Amount;

            //var transaction = new TransactionInfo() { TokenCode = ticker, Amount = atomicamount };

            var fee = ToExchange ? ExchangingBlock.FEE : TransferFee;

            if (ticker == LyraGlobal.OFFICIALTICKERCODE)
                balance_change += fee;

            // see if we have enough tokens
            if (previousBlock.Balances[ticker] < balance_change.ToBalanceLong())
            {
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.InsufficientFunds };
                //throw new ApplicationException("Insufficient funds");
            }

            // see if we have enough LYR to pay the transfer fee
            if (ticker != LyraGlobal.OFFICIALTICKERCODE)
                if (!previousBlock.Balances.ContainsKey(LyraGlobal.OFFICIALTICKERCODE) || previousBlock.Balances[LyraGlobal.OFFICIALTICKERCODE] < fee.ToBalanceLong())
                {
                    //throw new ApplicationException("Insufficient funds to pay transfer fee");
                    return new AuthorizationAPIResult() { ResultCode = APIResultCodes.InsufficientFunds };
                }

            //var svcBlockResult = await _rpcClient.GetLastServiceBlock(AccountId, SignAPICallAsync());
            //if (svcBlockResult.ResultCode != APIResultCodes.Success)
            //{
            //    throw new Exception("Unable to get latest service block.");
            //}

            SendTransferBlock sendBlock;
            if (ToExchange)
            {
                sendBlock = new ExchangingBlock()
                {
                    AccountID = AccountId,
                    VoteFor = VoteFor,
                    ServiceHash = await getLastServiceBlockHashAsync(), //svcBlockResult.GetBlock().Hash,
                    DestinationAccountId = DestinationAccountId,
                    Balances = new Dictionary<string, long>(),
                    //PaymentID = string.Empty,
                    Fee = fee,
                    FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                    FeeType = fee == 0m ? AuthorizationFeeTypes.NoFee : AuthorizationFeeTypes.Regular
                };
            }
            else
            {
                sendBlock = new SendTransferBlock()
                {
                    AccountID = AccountId,
                    VoteFor = VoteFor,
                    ServiceHash = await getLastServiceBlockHashAsync(), //svcBlockResult.GetBlock().Hash,
                    DestinationAccountId = DestinationAccountId,
                    Balances = new Dictionary<string, long>(),
                    //PaymentID = string.Empty,
                    Fee = fee,
                    FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                    FeeType = fee == 0m ? AuthorizationFeeTypes.NoFee : AuthorizationFeeTypes.Regular
                };
            };

            sendBlock.Balances.Add(ticker, previousBlock.Balances[ticker] - balance_change.ToBalanceLong());
            //sendBlock.Transaction = transaction;

            // for customer tokens, we pay fee in LYR (unless they are accepted by authorizers as a fee - TO DO)
            if (ticker != LyraGlobal.OFFICIALTICKERCODE)
                sendBlock.Balances.Add(LyraGlobal.OFFICIALTICKERCODE, previousBlock.Balances[LyraGlobal.OFFICIALTICKERCODE] - fee.ToBalanceLong());

            // transfer unchanged token balances from the previous block
            foreach (var balance in previousBlock.Balances)
                if (!(sendBlock.Balances.ContainsKey(balance.Key)))
                    sendBlock.Balances.Add(balance.Key, balance.Value);

            sendBlock.InitializeBlock(previousBlock, PrivateKey, AccountId);

            if (!sendBlock.ValidateTransaction(previousBlock))
            {
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.SendTransactionValidationFailed };
                //throw new ApplicationException("ValidateTransaction failed");
            }

            //sendBlock.Signature = Signatures.GetSignature(PrivateKey, sendBlock.Hash);
            AuthorizationAPIResult result;
            if (ToExchange)
                result = await _rpcClient.SendExchangeTransfer((ExchangingBlock)sendBlock);
            else
            {
                //var stopwatch = Stopwatch.StartNew();
                result = await _rpcClient.SendTransfer(sendBlock);
                //stopwatch.Stop();
                //Console.WriteLine($"_rpcClient.SendTransfer: {stopwatch.ElapsedMilliseconds} ms.");
            }

            if (result.ResultCode == APIResultCodes.Success)
                _lastTransactionBlock = sendBlock;

            return result;
        }

        public async Task<TradeOrderAuthorizationAPIResult> TradeOrder(
            TradeOrderTypes orderType, string SellToken, string BuyToken, decimal MaxAmount, decimal MinAmount, decimal Price, bool CoverAnotherTradersFee, bool AnotherTraderWillCoverFee)
        {
            TransactionBlock previousBlock = GetLatestBlock();
            if (previousBlock == null)
                return new TradeOrderAuthorizationAPIResult() { ResultCode = APIResultCodes.PreviousBlockNotFound };

            // For sell order: how many buy tokens are needed to buy one sell token
            // For buy order: how many sell tokens are needed to buy one buy token

            decimal balance_change;

            if (orderType == TradeOrderTypes.Sell)
            {
                // For sell order: how many buy tokens are needed to buy one sell token
                balance_change = MaxAmount;
            }
            else
            {
                // For buy order: how many sell tokens are needed to buy one buy token

                // buy order locks (sends to nowhere) the MaxAmount of Sell tokens multiplied by Price

                balance_change = MaxAmount * Price;
            }

            // Let's handle the fees. We don't pay fees for placing orders but we have to reserve funds so we could pay a fee for trading 
            if (CoverAnotherTradersFee && AnotherTraderWillCoverFee)
                return new TradeOrderAuthorizationAPIResult() { ResultCode = APIResultCodes.InvalidFeeType, ResultMessage = "both CoverAnotherTradersFee and AnotherTraderWillCoverFee cannobe be set to true." };

            var trading_fee = TradeFee;

            if (AnotherTraderWillCoverFee)
                trading_fee = 0;

            if (CoverAnotherTradersFee)
                trading_fee *= 2;

            if (SellToken == LyraGlobal.OFFICIALTICKERCODE)
                balance_change += trading_fee;

            // see if we have enough LYR to pay the transfer fee
            if (trading_fee > 0 && SellToken != LyraGlobal.OFFICIALTICKERCODE)
            {
                if (!previousBlock.Balances.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
                    return new TradeOrderAuthorizationAPIResult() { ResultCode = APIResultCodes.InsufficientFunds };

                if (previousBlock.Balances[LyraGlobal.OFFICIALTICKERCODE] < trading_fee)
                    return new TradeOrderAuthorizationAPIResult() { ResultCode = APIResultCodes.InsufficientFunds };
            }
            // see if we have enough tokens to sell.
            // For both sell and buy orders we send sell tokens, as different order types sell "opposite" tokens
            if (previousBlock.Balances[SellToken] < balance_change)
                return new TradeOrderAuthorizationAPIResult() { ResultCode = APIResultCodes.InsufficientFunds };

            //var svcBlockResult = await _rpcClient.GetLastServiceBlock(AccountId, SignAPICallAsync());

            var tradeBlock = new TradeOrderBlock
            {
                AccountID = AccountId,
                ServiceHash = await getLastServiceBlockHashAsync(),
                DestinationAccountId = string.Empty, // we are sending to nowhere
                Balances = new Dictionary<string, long>(),
                //PaymentID = string.Empty,
                Fee = 0, // We don't pay fees for placing orders
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,
                TradeAmount = MaxAmount,
                MinTradeAmount = MinAmount,
                Price = Price,
                MaxQuantity = 1,
                SellTokenCode = SellToken,
                BuyTokenCode = BuyToken,
                OrderType = orderType,
                CoverAnotherTradersFee = CoverAnotherTradersFee,
                AnotherTraderWillCoverFee = AnotherTraderWillCoverFee
            };

            tradeBlock.Balances.Add(SellToken, previousBlock.Balances[SellToken] - balance_change.ToBalanceLong());
            //sendBlock.Transaction = transaction;

            // We have to count for the fee here to make sure we lock enough funds to pay fee later in ExecuteTradeOrder or Trade Block.
            // for customer tokens, we pay fee in LYR (unless they are accepted by authorizers as a fee - TO DO)
            if (trading_fee > 0 && SellToken != LyraGlobal.OFFICIALTICKERCODE)
                tradeBlock.Balances.Add(LyraGlobal.OFFICIALTICKERCODE, previousBlock.Balances[LyraGlobal.OFFICIALTICKERCODE] - trading_fee.ToBalanceLong());

            // transfer unchanged token balances from the previous block
            foreach (var balance in previousBlock.Balances)
                if (!(tradeBlock.Balances.ContainsKey(balance.Key)))
                    tradeBlock.Balances.Add(balance.Key, balance.Value);

            tradeBlock.InitializeBlock(previousBlock, PrivateKey, AccountId);

            if (!tradeBlock.ValidateTransaction(previousBlock))
                return new TradeOrderAuthorizationAPIResult() { ResultCode = APIResultCodes.TradeOrderValidationFailed };

            //tradeBlock.Signature = Signatures.GetSignature(PrivateKey, tradeBlock.Hash);

            var result = await _rpcClient.TradeOrder(tradeBlock);

            if (result.ResultCode == APIResultCodes.Success)
                _lastTransactionBlock = tradeBlock;

            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="trade">
        /// The trade order block
        /// </param>
        /// <returns></returns>
        public async Task<AuthorizationAPIResult> Trade(TradeBlock trade)
        {
            TransactionBlock previousBlock = GetLatestBlock();
            if (previousBlock == null)
                return new TradeOrderAuthorizationAPIResult() { ResultCode = APIResultCodes.PreviousBlockNotFound };

            var balance_change = trade.SellAmount;

            if (trade.SellTokenCode == LyraGlobal.OFFICIALTICKERCODE)
                balance_change += trade.Fee;

            trade.Balances.Add(trade.SellTokenCode, previousBlock.Balances[trade.SellTokenCode] - balance_change.ToBalanceLong());

            // transfer unchanged token balances from the previous block
            foreach (var balance in previousBlock.Balances)
                if (!(trade.Balances.ContainsKey(balance.Key)))
                    trade.Balances.Add(balance.Key, balance.Value);

            trade.ServiceHash = await getLastServiceBlockHashAsync();
            trade.InitializeBlock(previousBlock, PrivateKey, AccountId);

            var trade_result = await _rpcClient.Trade(trade);

            if (trade_result.ResultCode == APIResultCodes.BlockSignatureValidationFailed)
            {
                Console.WriteLine($"BlockSignatureValidationFailed");
                Console.WriteLine("Error Message: ");
                Console.WriteLine(trade_result.ResultMessage);
                Console.WriteLine("Local Block: ");
                Console.WriteLine(trade.Print());
            }

            if (trade_result.ResultCode == APIResultCodes.Success)
                _lastTransactionBlock = trade;

            return trade_result;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="OrderId">
        /// Teh hash of the trade order block
        /// </param>
        /// <returns></returns>
        public async Task<AuthorizationAPIResult> CancelTradeOrder(string OrderId)
        {
            var order = await GetBlockByHash(OrderId) as TradeOrderBlock;
            if (order == null)
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.BlockNotFound };

            var cancelBlock = new CancelTradeOrderBlock
            {
                AccountID = AccountId,
                ServiceHash = await getLastServiceBlockHashAsync(),
                Balances = new Dictionary<string, long>(),
                FeeType = AuthorizationFeeTypes.NoFee,
                TradeOrderId = OrderId
            };

            var previous_to_order_block = await GetBlockByHash(order.PreviousHash);
            if (previous_to_order_block == null)
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.PreviousBlockNotFound };

            var order_transaction = order.GetTransaction(previous_to_order_block);

            var previousBlock = GetLatestBlock();

            cancelBlock.Balances.Add(order.SellTokenCode, (previousBlock.Balances[order.SellTokenCode].ToBalanceDecimal() + order_transaction.TotalBalanceChange).ToBalanceLong());

            // transfer unchanged token balances from the previous block
            foreach (var balance in previousBlock.Balances)
                if (!(cancelBlock.Balances.ContainsKey(balance.Key)))
                    cancelBlock.Balances.Add(balance.Key, balance.Value);

            cancelBlock.InitializeBlock(previousBlock, PrivateKey, AccountId);

            var result = await _rpcClient.CancelTradeOrder(cancelBlock);

            if (result.ResultCode == APIResultCodes.Success)
            {
                _lastTransactionBlock = cancelBlock;
            }
            else
            if (result.ResultCode == APIResultCodes.BlockSignatureValidationFailed)
            {
                Console.WriteLine($"BlockSignatureValidationFailed");
                Console.WriteLine("Error Message: " + result.ResultMessage);
                Console.WriteLine("Local Block: " + JsonConvert.SerializeObject(cancelBlock));
            }
            else
            {
                Console.WriteLine("Authorization failed" + result.ResultCode.ToString());
                Console.WriteLine("Error Message: " + result.ResultMessage);
            }
            return result;
        }

        public async Task<ActiveTradeOrdersAPIResult> GetActiveTradeOrders(string SellToken, string BuyToken, TradeOrderListTypes OrderType)
        {
            var result = await _rpcClient.GetActiveTradeOrders(AccountId, SellToken, BuyToken, OrderType, SignAPICallAsync());
            return result;
        }

        //public async Task<string> PrintActiveTradeOrdersAsync()
        //{
        //    var orders_result = await _rpcClient.GetActiveTradeOrders(AccountId, null, null, TradeOrderListTypes.All, SignAPICallAsync());
        //    if (orders_result.ResultCode != APIResultCodes.Success)
        //    {
        //        return "No active trade orders found";
        //    }

        //    var orders = orders_result.GetList();

        //    string result = "Sell Orders:\n";
        //    foreach (var order in orders)
        //        if (order.OrderType == TradeOrderTypes.Sell)
        //            result += $"Sell: {order.TradeAmount} {order.SellTokenCode} Price: {order.Price} {order.BuyTokenCode}\n";

        //    Console.WriteLine("Buy Orders:");
        //    foreach (var order in orders)
        //        if (order.OrderType == TradeOrderTypes.Buy)
        //            result += $"Buy: {order.TradeAmount} {order.BuyTokenCode} Price: {order.Price} {order.SellTokenCode}\n";

        //    return result;
        //}


        //private async Task<int> FindTokenPrecision(string token)
        //{
        //    int precision = -1;

        //    // see if we have this already in local storage
        //    var genesisBlock = _storage.GetTokenInfo(token);
        //    if (genesisBlock == null)
        //    {
        //        var result = await _rpcClient.GetTokenGenesisBlock(AccountId, token, SignAPICallAsync());
        //        if (result.ResultCode == APIResultCodes.Success)
        //        {
        //            genesisBlock = result.GetBlock() as TokenGenesisBlock;
        //            SaveTokenInfoBlock(genesisBlock);

        //            //Console.WriteLine($"Found Token Genesis Block for {genesisBlock.Ticker}");
        //            //Console.WriteLine("Balance: " + GetDisplayBalances());
        //            //Console.Write(string.Format("{0}> ", AccountName));
        //        }
        //    }

        //    if (genesisBlock != null)
        //        precision = (int)genesisBlock.Precision;

        //    return precision;
        //}

        private async Task<AuthorizationAPIResult> OpenStandardAccountWithReceiveBlock(NewTransferAPIResult new_transfer_info)
        {
            var svcBlockResult = await _rpcClient.GetLastServiceBlock();
            if (svcBlockResult.ResultCode != APIResultCodes.Success)
            {
                throw new Exception("Unable to get latest service block.");
            }

            var openReceiveBlock = new OpenWithReceiveTransferBlock
            {
                AccountType = AccountTypes.Standard,
                AccountID = AccountId,
                ServiceHash = svcBlockResult.GetBlock().Hash,
                SourceHash = new_transfer_info.SourceHash,
                Balances = new Dictionary<string, long>(),
                Fee = 0,
                FeeType = AuthorizationFeeTypes.NoFee,
                NonFungibleToken = new_transfer_info.NonFungibleToken,
                VoteFor = VoteFor
            };

            openReceiveBlock.Balances.Add(new_transfer_info.Transfer.TokenCode, new_transfer_info.Transfer.Amount.ToBalanceLong());
            openReceiveBlock.InitializeBlock(null, PrivateKey, AccountId);

            //openReceiveBlock.Signature = Signatures.GetSignature(PrivateKey, openReceiveBlock.Hash);

            var result = await _rpcClient.ReceiveTransferAndOpenAccount(openReceiveBlock);

            if (result.ResultCode == APIResultCodes.BlockSignatureValidationFailed)
            {
                Console.WriteLine($"BlockSignatureValidationFailed");
                Console.WriteLine("Error Message: ");
                Console.WriteLine(result.ResultMessage);
                Console.WriteLine("Local Block: ");
                Console.WriteLine(openReceiveBlock.Print());
            }
            else
            if (result.ResultCode != APIResultCodes.Success)
            {
                Console.WriteLine($"ReceiveTransferAndOpenAccount: Failed to authorize receive transfer block with error code: {result.ResultCode}");
                Console.WriteLine("Error Message: " + result.ResultMessage);
            }
            else
            {
                _lastTransactionBlock = openReceiveBlock;
                Console.WriteLine($"Receive transfer block has been authorized successfully");
                Console.WriteLine("Balance: " + await GetDisplayBalancesAsync());
            }
            Console.Write(string.Format("{0}> ", AccountName));
            return result;
        }

        private async Task<AuthorizationAPIResult> OpenStandardAccountWithImport(string ImportPrivateKey, TransactionBlock last_imported_block, string imported_account_id)
        {
            var open_import_block = new OpenAccountWithImportBlock
            {
                AccountType = AccountTypes.Standard,
                AccountID = AccountId,
                ImportedAccountId = imported_account_id,
                ServiceHash = await getLastServiceBlockHashAsync(),
                SourceHash = null,
                Balances = new Dictionary<string, long>(),
                Fee = 0,
                FeeType = AuthorizationFeeTypes.NoFee,
                NonFungibleToken = null,
                VoteFor = VoteFor
            };


            if (last_imported_block != null)
            {
                open_import_block.ImportedLastBlockHash = last_imported_block.Hash;
                // transfer all token balances from the imported block
                foreach (var balance in last_imported_block.Balances)
                    open_import_block.Balances.Add(balance.Key, balance.Value);
            }

            open_import_block.InitializeBlock(null, PrivateKey, AccountId);

            open_import_block.ImportedAccountSignature = Signatures.GetSignature(ImportPrivateKey, open_import_block.Hash, imported_account_id);

            var result = await _rpcClient.OpenAccountWithImport(open_import_block);

            await ProcessResultAsync(result, "Import Account", open_import_block);

            return result;
        }

        private async Task ProcessResultAsync(AuthorizationAPIResult result, string OperationName, TransactionBlock block)
        {
            if (result.ResultCode == APIResultCodes.BlockSignatureValidationFailed)
            {
                Console.WriteLine($"BlockSignatureValidationFailed");
                Console.WriteLine("Error Message: ");
                Console.WriteLine(result.ResultMessage);
                Console.WriteLine("Local Block: ");
                Console.WriteLine(block.Print());
            }
            else
            if (result.ResultCode != APIResultCodes.Success)
            {
                Console.WriteLine(OperationName + $": Operation failed with result: {result.ResultCode}");
                Console.WriteLine("Error Message: " + result.ResultMessage);
            }
            else
            {
                Console.WriteLine(OperationName + $": Operation success");
                _lastTransactionBlock = block;
                Console.WriteLine("Balance: " + await GetDisplayBalancesAsync());
            }
            Console.Write(string.Format("{0}> ", AccountName));
            
        }

        public async Task<AuthorizationAPIResult> ImportAccount(string ImportPrivateKey)
        {
            string imported_account_id = Signatures.GetAccountIdFromPrivateKey(ImportPrivateKey);
            var imported_account_height_result = await _rpcClient.GetAccountHeight(imported_account_id);
            TransactionBlock last_imported_block = null;
            if (imported_account_height_result.Successful())
            {
                long imported_account_height = imported_account_height_result.Height;

                var last_imported_block_result = await _rpcClient.GetBlockByIndex(imported_account_id, imported_account_height);
                if (!last_imported_block_result.Successful())
                    throw new Exception("Unable to get latest imported account block.");

                last_imported_block = last_imported_block_result.GetBlock() as TransactionBlock;
                if (last_imported_block == null)
                    throw new Exception("Unable to get latest imported account block.");
            }

            if (GetLocalAccountHeight() == 0) // if this is new account with no blocks
                return await OpenStandardAccountWithImport(ImportPrivateKey, last_imported_block, imported_account_id);

            TransactionBlock previousBlock = GetLatestBlock();
            if (previousBlock == null)
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.PreviousBlockNotFound };

            var import_block = new ImportAccountBlock
            {
                AccountID = AccountId,
                ImportedAccountId = imported_account_id,
                ServiceHash = await getLastServiceBlockHashAsync(),
                SourceHash = null,
                Balances = new Dictionary<string, long>(),
                Fee = 0,
                FeeType = AuthorizationFeeTypes.NoFee,
                NonFungibleToken = null,
                VoteFor = VoteFor
            };

            if (last_imported_block != null)
            {
                import_block.ImportedLastBlockHash = last_imported_block.Hash;

                // transfer all token balances from the imported block
                foreach (var balance in last_imported_block.Balances)
                    import_block.Balances.Add(balance.Key, balance.Value);
            }
            
            // Now add all existing balances as the import block becomes the new block that contains balances of both accounts
            foreach (var balance in previousBlock.Balances)
                if (!(import_block.Balances.ContainsKey(balance.Key)))
                    import_block.Balances.Add(balance.Key, balance.Value);
                else
                    import_block.Balances[balance.Key] += balance.Value;

            if (last_imported_block != null) // only validate if imported account is not empty
                if (!import_block.ValidateTransaction(previousBlock))
                    throw new ApplicationException("ValidateTransaction failed");

            import_block.InitializeBlock(previousBlock, PrivateKey, AccountId);

            import_block.ImportedAccountSignature = Signatures.GetSignature(ImportPrivateKey, import_block.Hash, imported_account_id);

            var result = await _rpcClient.ImportAccount(import_block);

            await ProcessResultAsync(result, "Import Account", import_block);

            return result;
        }

        private async Task<AuthorizationAPIResult> ReceiveTransfer(NewTransferAPIResult new_transfer_info)
        {

            // *** Slava - July 19, 2020 - I am not sure if we need this call anymore? 
            //await FindTokenPrecision(new_transfer_info.Transfer.TokenCode);
            // ***

            if (GetLocalAccountHeight() == 0) // if this is new account with no blocks
                return await OpenStandardAccountWithReceiveBlock(new_transfer_info);

            var svcBlockResult = await _rpcClient.GetLastServiceBlock();
            if (svcBlockResult.ResultCode != APIResultCodes.Success)
            {
                throw new Exception("Unable to get latest service block.");
            }

            var receiveBlock = new ReceiveTransferBlock
            {
                AccountID = AccountId,
                VoteFor = VoteFor,
                ServiceHash = svcBlockResult.GetBlock().Hash,
                SourceHash = new_transfer_info.SourceHash,
                Balances = new Dictionary<string, long>(),
                Fee = 0,
                FeeType = AuthorizationFeeTypes.NoFee,
                NonFungibleToken = new_transfer_info.NonFungibleToken
            };

            TransactionBlock latestBlock = GetLatestBlock();

            var newBalance = new_transfer_info.Transfer.Amount;
            // if the recipient's account has this token already, add the transaction amount to the existing balance
            if (latestBlock.Balances.ContainsKey(new_transfer_info.Transfer.TokenCode))
                newBalance += latestBlock.Balances[new_transfer_info.Transfer.TokenCode].ToBalanceDecimal();

            receiveBlock.Balances.Add(new_transfer_info.Transfer.TokenCode, newBalance.ToBalanceLong());

            // transfer unchanged token balances from the previous block
            foreach (var balance in latestBlock.Balances)
                if (!(receiveBlock.Balances.ContainsKey(balance.Key)))
                    receiveBlock.Balances.Add(balance.Key, balance.Value);

            receiveBlock.InitializeBlock(latestBlock, PrivateKey, AccountId);

            if (!receiveBlock.ValidateTransaction(latestBlock))
                throw new ApplicationException("ValidateTransaction failed");

            //receiveBlock.Signature = Signatures.GetSignature(PrivateKey, receiveBlock.Hash);

            var result = await _rpcClient.ReceiveTransfer(receiveBlock);

            await ProcessResultAsync(result, "Receive Transfer", receiveBlock);

            return result;
        }

        //long IntPow(long x, uint pow)
        //{
        //    long ret = 1;
        //    while (pow != 0)
        //    {
        //        if ((pow & 1) == 1)
        //            ret *= x;
        //        x *= x;
        //        pow >>= 1;
        //    }
        //    return ret;
        //}

        public async Task<APIResultCodes> CreateGenesisForCoreTokenAsync()
        {
            var svcBlockResult = await _rpcClient.GetLastServiceBlock();
            if (svcBlockResult.ResultCode != APIResultCodes.Success)
            {
                throw new Exception("Unable to get latest service block.");
            }

            // initiate test coins
            var openTokenGenesisBlock = new LyraTokenGenesisBlock
            {
                AccountType = AccountTypes.Standard,
                Ticker = LyraGlobal.OFFICIALTICKERCODE,
                DomainName = LyraGlobal.OFFICIALDOMAIN,
                ContractType = ContractTypes.Cryptocurrency,
                Description = LyraGlobal.PRODUCTNAME + " Gas Token",
                Precision = LyraGlobal.OFFICIALTICKERPRECISION,
                IsFinalSupply = true,
                AccountID = AccountId,
                Balances = new Dictionary<string, long>(),
                ServiceHash = svcBlockResult.GetBlock().Hash,
                Fee = TokenGenerationFee,
                FeeType = AuthorizationFeeTypes.Regular,
                Icon = "https://i.imgur.com/L3h0J1K.png",
                Image = "https://i.imgur.com/B8l4ZG5.png",
                RenewalDate = DateTime.Now.AddYears(1000)
            };
            // TO DO - set service hash
            var transaction = new TransactionInfo() { TokenCode = openTokenGenesisBlock.Ticker, Amount = LyraGlobal.OFFICIALGENESISAMOUNT };
            //var transaction = new TransactionInfo() { TokenCode = openTokenGenesisBlock.Ticker, Amount = 150000000 };

            openTokenGenesisBlock.Balances.Add(transaction.TokenCode, transaction.Amount.ToBalanceLong()); // This is current supply in atomic units (1,000,000.00)
                                                                                                           //openTokenGenesisBlock.Transaction = transaction;
            openTokenGenesisBlock.InitializeBlock(null, PrivateKey, AccountId);

            //openTokenGenesisBlock.Signature = Signatures.GetSignature(PrivateKey, openTokenGenesisBlock.Hash);

            var result = await _rpcClient.OpenAccountWithGenesis(openTokenGenesisBlock);

            if (result.ResultCode == APIResultCodes.BlockSignatureValidationFailed)
            {
                Console.WriteLine($"BlockSignatureValidationFailed");
                Console.WriteLine("Error Message: ");
                Console.WriteLine(result.ResultMessage);
                Console.WriteLine("Local Block: ");
                Console.WriteLine(openTokenGenesisBlock.Print());
            }
            else
            if (result.ResultCode != APIResultCodes.Success)
            {
                Console.WriteLine($"Failed to add genesis block with error code: {result.ResultCode}");
                Console.WriteLine("Error Message: " + result.ResultMessage);
                Console.WriteLine(openTokenGenesisBlock.Print());

            }
            else
            {
                _lastTransactionBlock = openTokenGenesisBlock;
                Console.WriteLine($"Genesis block has been authorized successfully");
                Console.WriteLine("Balance: " + await GetDisplayBalancesAsync());
            }
            //Console.Write(string.Format("{0}> ", AccountName));
            return result.ResultCode;
        }

        public async Task<AuthorizationAPIResult> CreateToken(
            string tokenName,
            string domainName,
            string description,
            sbyte precision,
            decimal supply,
            bool isFinalSupply,
            string owner, // shop name
            string address, // shop URL
            string currency, // USD
            ContractTypes contractType, // reward or discount or custom
            Dictionary<string, string> tags)
        {
            if (string.IsNullOrWhiteSpace(domainName))
                domainName = "Custom";

            string ticker = domainName + "/" + tokenName;

            TransactionBlock latestBlock = GetLatestBlock();
            if (latestBlock == null || latestBlock.Balances[LyraGlobal.OFFICIALTICKERCODE] < TokenGenerationFee.ToBalanceLong())
            {
                //throw new ApplicationException("Insufficent funds");
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.InsufficientFunds };
            }

            var svcBlockResult = await _rpcClient.GetLastServiceBlock();
            if (svcBlockResult.ResultCode != APIResultCodes.Success)
            {
                throw new Exception("Unable to get latest service block.");
            }

            // initiate test coins
            TokenGenesisBlock tokenBlock = new TokenGenesisBlock
            {
                Ticker = ticker,
                DomainName = domainName,
                Description = description,
                Precision = precision,
                IsFinalSupply = isFinalSupply,
                //CustomFee = 0,
                //CustomFeeAccountId = string.Empty,
                AccountID = AccountId,
                Balances = new Dictionary<string, long>(),
                ServiceHash = svcBlockResult.GetBlock().Hash,
                Fee = TokenGenerationFee,
                FeeType = AuthorizationFeeTypes.Regular,
                Owner = owner,
                Address = address,
                Currency = currency,
                Tags = tags,
                RenewalDate = DateTime.Now.Add(TimeSpan.FromDays(365)),
                ContractType = contractType,
                VoteFor = VoteFor
            };
            // TO DO - set service hash

            //var transaction = new TransactionInfo() { TokenCode = ticker, Amount = supply * (long)Math.Pow(10, precision) };
            var transaction = new TransactionInfo() { TokenCode = ticker, Amount = supply };

            tokenBlock.Balances.Add(transaction.TokenCode, transaction.Amount.ToBalanceLong()); // This is current supply in atomic units (1,000,000.00)
            tokenBlock.Balances.Add(LyraGlobal.OFFICIALTICKERCODE, latestBlock.Balances[LyraGlobal.OFFICIALTICKERCODE] - TokenGenerationFee.ToBalanceLong());
            //tokenBlock.Transaction = transaction;
            // transfer unchanged token balances from the previous block
            foreach (var balance in latestBlock.Balances)
                if (!(tokenBlock.Balances.ContainsKey(balance.Key)))
                    tokenBlock.Balances.Add(balance.Key, balance.Value);

            tokenBlock.InitializeBlock(latestBlock, PrivateKey, AccountId);

            //tokenBlock.Signature = Signatures.GetSignature(PrivateKey, tokenBlock.Hash);

            var result = await _rpcClient.CreateToken(tokenBlock);

            if (result.ResultCode == APIResultCodes.BlockSignatureValidationFailed)
            {
                Console.WriteLine($"BlockSignatureValidationFailed");
                Console.WriteLine("Error Message: ");
                Console.WriteLine(result.ResultMessage);
                Console.WriteLine("Local Block: ");
                Console.WriteLine(tokenBlock.Print());
            }
            else
            if (result.ResultCode == APIResultCodes.Success)
            {
                _lastTransactionBlock = tokenBlock;
            }
            return result;
        }

        public string PrintLastBlock()
        {
            TransactionBlock latestBlock = GetLatestBlock();
            if (latestBlock == null)
                return "no blocks found";
            //return JsonConvert.SerializeObject(latestBlock);
            return latestBlock.Print();
        }

        public string PrintBlock(string blockindex)
        {
            int index;
            try
            {
                index = Convert.ToInt32(blockindex);
            }
            catch
            {
                return "incorrect index";
            }

            var block = GetBlockByIndex(index).Result;
            if (block == null)
                return "block not found";

            //return JsonConvert.SerializeObject(latestBlock);
            return block.Print();
        }

        //private ExecuteTradeOrderBlock CreateExecuteTradeDiscountBlock(TradeBlock trade, TradeOrderBlock order, LoyaltyDiscountToken discount_token)
        //{
        //    TransactionBlock previousBlock = GetLatestBlock();

        //    var block = new ExecuteTradeOrderBlock()
        //    {
        //        AccountID = AccountId,
        //        DestinationAccountId = trade.AccountID,
        //        Balances = new Dictionary<string, long>(),
        //        TradeId = trade.Hash,
        //        TradeOrderId = trade.TradeOrderId,
        //        SellTokenCode = discount_token.TokenCode,
        //        SellAmount = discount_token.Denomination,
        //    };


        //    // no change in USD balance as the entire Tx amount was previously "locked" by the original order 
        //    block.Balances.Add(discount_token.TokenCode, previousBlock.Balances[discount_token.TokenCode]);
        //    // no change in LGT balance as the fee amount was previously "locked" by the original order
        //    block.Balances.Add(LyraGlobal.LYRA_TICKER_CODE, previousBlock.Balances[LyraGlobal.LYRA_TICKER_CODE]);

        //    // The fee, which was "reserved" by the originakl order, now is really paid
        //    block.Fee = (long)(0.1 * Math.Pow(10, 2) * 2);
        //    block.FeeCode = LyraGlobal.LYRA_TICKER_CODE;

        //    block.NonFungibleToken = discount_token;

        //    block.InitializeBlock(previousBlock, PrivateKey);

        //    return block;
        //}

        // checks the checksum of public or private key
        public bool ValidatePrivateKey(string private_key)
        {
            if (string.IsNullOrEmpty(private_key))
                return false;
            return Signatures.ValidatePrivateKey(private_key);
        }

        public async Task<ServiceBlock> GetLastServiceBlockAsync()
        {
            var svcBlockResult = await _rpcClient.GetLastServiceBlock();
            if (svcBlockResult.ResultCode != APIResultCodes.Success)
            {
                throw new Exception($"Unable to retrieve the latest service block. Result Code: {svcBlockResult.ResultCode}");
            }
            return svcBlockResult.GetBlock() as ServiceBlock;
        }

        private async Task<string> getLastServiceBlockHashAsync()
        {
            var svcBlockResult = await GetLastServiceBlockAsync();
            if (svcBlockResult == null)
            {
                throw new Exception($"Unable to retrieve the latest service block. ");
            }
            return svcBlockResult.Hash;
        }
    }
}


