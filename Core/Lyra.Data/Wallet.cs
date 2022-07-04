using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lyra.Core.Blocks;
using Lyra.Core.API;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Linq;
using System.IO;
using Lyra.Data.Crypto;
using Lyra.Data;
using System.Data;
using Lyra.Data.API;
using Lyra.Data.Utils;
using System.Threading;
using Lyra.Data.Blocks;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.API.ODR;
using System.Globalization;

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

        private string? _newVoteFor;
        public void SetVoteFor(string voteTarget)
        {
            _newVoteFor = voteTarget;
        }
        public string? VoteFor => _newVoteFor ?? (_lastSyncBlock?.VoteFor);

        private bool _noConsole;

        private CancellationToken _cancel;
        // to do 
        // 1) move rpcclient to CLI
        // 2) create interface and reference rpcclient by interface here, use the same interface in server and REST API client (Shopify app)  
        //private RPCClient _rpcClient = null;
        private readonly IAccountDatabase _store;
        private ILyraAPI? _rpcClient = null;
        public ILyraAPI? RPC => _rpcClient;

        private long SyncHeight = -1;
        private string SyncHash = string.Empty;

        public decimal TransferFee = 0; // in atomic units
        public decimal TokenGenerationFee = 0; // in atomic units
        public decimal TradeFee = 0; // in atomic units
        public bool AccountAlreadyImported = false;

        private TransactionBlock _lastSyncBlock;

        public decimal BaseBalance
        {
            get
            {
                if (_lastSyncBlock != null && _lastSyncBlock.Balances.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
                    return _lastSyncBlock.Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();
                else
                    return 0m;
            }
        }

        public bool NoConsole { get => _noConsole; set => _noConsole = value; }

        protected Wallet(IAccountDatabase storage, string name, ILyraAPI? rpcClient = null)
        {
            _store = storage;
            AccountName = name;
            _rpcClient = rpcClient;

            var platform = Environment.OSVersion.Platform.ToString();
            if (platform == "Android" || platform == "iOS")
                NoConsole = true;
            else
                NoConsole = false;
        }

        private void PrintConLine(string s)
        {
            if (NoConsole)
                return;

            Console.WriteLine(s);
        }
        //private void PrintCon(string s1, string s2 = null)
        //{
        //    if (NoConsole)
        //        return;

        //    if (s2 == null)
        //        Console.Write(s1);
        //    else
        //        Console.Write(s1, s2);
        //}

        public void SetClient(ILyraAPI client)
        {
            _rpcClient = client;
        }

        public static string GetFullFolderName(string NetworkId, string FolderName)
        {
            return $"{Utilities.GetLyraDataDir(NetworkId, LyraGlobal.OFFICIALDOMAIN)}{Utilities.PathSeperator}{FolderName}{Utilities.PathSeperator}";
        }

        public static Wallet Open(IAccountDatabase store, string name, string password, ILyraAPI? rpcClient = null)
        {
            var wallet = new Wallet(store, name, rpcClient);
            store.Open(name, password);
            return wallet;
        }

        public static void Create(IAccountDatabase store, string name, string password, string networkId)
        {
            (var privateKey, _) = Signatures.GenerateWallet();
            Create(store, name, password, networkId, privateKey);
        }

        public static Wallet Create(IAccountDatabase store, string name, string password, string networkId, string privateKey)
        {
            if (!Signatures.ValidatePrivateKey(privateKey))
            {
                throw new InvalidDataException("Failed to create wallet: invalid private key");
            }

            if (store.Exists(name))
            {
                throw new InvalidDataException($"Wallet named {name} is already exist.");
            }

            var wallet = new Wallet(store, name);
            var accountId = Signatures.GetAccountIdFromPrivateKey(privateKey);
            if (!store.Create(name, password, networkId, privateKey, accountId, ""))
                throw new Exception("Can't create wallet in storage.");
            return wallet;
        }
        // one-time "manual" sync up with the node 
        public async Task<APIResultCodes> SyncAsync(ILyraAPI RPCClient)
        {
            return await SyncAsync(RPCClient, CancellationToken.None);
        }
        public async Task<APIResultCodes> SyncAsync(ILyraAPI RPCClient, CancellationToken cancel)
        {
            if (RPCClient != null)
                _rpcClient = RPCClient;

            if (cancel != CancellationToken.None)
                _cancel = cancel;

            if (_rpcClient == null)
                throw new InvalidOperationException("No RPC Client");

            var ret = await SyncServiceChainAsync();
            if (ret != APIResultCodes.Success)
                return ret;

            var blockResult1 = await _rpcClient.GetLastBlockAsync(AccountId);

            if (blockResult1 == null)
                return APIResultCodes.NoRPCServerConnection;

            if (blockResult1.Successful())
                _lastSyncBlock = blockResult1.GetBlock() as TransactionBlock;

            await SyncIncomingTransfersAsync();

            var blockResult = await _rpcClient.GetLastBlockAsync(AccountId);
            if (blockResult == null)
                return APIResultCodes.NotFound;

            if (blockResult.Successful())
                _lastSyncBlock = blockResult.GetBlock() as TransactionBlock;

            return blockResult.ResultCode;
        }

        public string SignAPICall()
        {
            return Signatures.GetSignature(PrivateKey, SyncHash, AccountId);
        }

        public async Task<TokenGenesisBlock> GetTokenGenesisBlockAsync(string TokenCode)
        {
            var res = await _rpcClient.GetTokenGenesisBlockAsync(AccountId, TokenCode, SignAPICall());
            if (res?.ResultCode == APIResultCodes.Success)
                return res.GetBlock() as TokenGenesisBlock;
            else
                return null;
        }

        public long GetLocalAccountHeight()
        {
            return _lastSyncBlock?.Height ?? 0;
        }

        public async Task<List<string>> GetTokenNamesAsync(string keyword)
        {
            if (_rpcClient == null)
                return new List<string>();

            var result = await _rpcClient.GetTokenNamesAsync(AccountId, SignAPICall(), keyword);
            if (result.ResultCode == APIResultCodes.Success)
                return result.Entities;
            else
                throw new Exception("Error get Token names: " + result.ResultCode.ToString());
        }

        private async Task<APIResultCodes> SyncServiceChainAsync()
        {
            try
            {
                while (!_cancel.IsCancellationRequested)
                {
                    var result = await _rpcClient.GetSyncHeightAsync();
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
                    var blockresult = await _rpcClient.GetLastServiceBlockAsync();

                    if (blockresult.ResultCode != APIResultCodes.Success)
                        return blockresult.ResultCode;
                    ServiceBlock lastServiceBlock = blockresult.GetBlock() as ServiceBlock;
                    TransferFee = lastServiceBlock.TransferFee;
                    TokenGenerationFee = lastServiceBlock.TokenGenerationFee;
                    TradeFee = lastServiceBlock.TradeFee;
                }
                return APIResultCodes.Success;
            }
            catch (Exception e)
            {
                PrintConLine("Exception in SyncServiceChain(): " + e.Message);
                return APIResultCodes.UnknownError;
            }
        }

        public async Task<bool> GetPendingRecvAsync()
        {
            var lookup_result = await _rpcClient.LookForNewTransfer2Async(AccountId, SignAPICall());
            return lookup_result.Successful();
        }

        private async Task<APIResultCodes> SyncIncomingTransfersAsync()
        {
            try
            {
                var lookup_result = await _rpcClient.LookForNewTransfer2Async(AccountId, SignAPICall());
                int max_counter = 0;

                if (lookup_result == null)
                    return APIResultCodes.NotFound;

                while (!_cancel.IsCancellationRequested && lookup_result.Successful() && max_counter < 100) // we don't want to enter an endless loop...
                {
                    max_counter++;

                    PrintConLine($"Received new transaction, sending request for settlement...");

                    var receive_result = await ReceiveTransferAsync(lookup_result);
                    if (!receive_result.Successful())
                        return receive_result.ResultCode;

                    lookup_result = await _rpcClient.LookForNewTransfer2Async(AccountId, SignAPICall());
                }

                // the fact that do one sent us any money does not mean this call failed...
                if (lookup_result.ResultCode == APIResultCodes.NoNewTransferFound)
                    return APIResultCodes.Success;

                if (lookup_result.ResultCode == APIResultCodes.AccountAlreadyImported)
                {
                    PrintConLine($"This account was imported (merged) to another account.");
                    AccountAlreadyImported = true;
                    return lookup_result.ResultCode;
                }

                return lookup_result.ResultCode;
            }
            catch (Exception e)
            {
                PrintConLine("Exception in SyncIncomingTransfers(): " + e.Message);
                return APIResultCodes.UnknownError;
            }
        }



        #region Reward Trade Processing
        /*
        public async Task<TradeAPIResult> LookForNewTradeAsync(string BuyTokenCode, string SellTokenCode)
        {
            try
            {
                var lookup_result = await _rpcClient.LookForNewTradeAsync(AccountId, BuyTokenCode, SellTokenCode, SignAPICall());

                return lookup_result;
            }
            catch (Exception e)
            {
                PrintConLine("Exception in LookForNewTrade(): " + e.Message);
                return new TradeAPIResult() { ResultCode = APIResultCodes.UnknownError, ResultMessage = e.Message };
            }
        }

        public async Task<AuthorizationAPIResult> RedeemRewardsAsync(string reward_token_code, decimal discount_amount)
        {
            var trade_orders = await GetActiveTradeOrdersAsync("*", reward_token_code, TradeOrderListTypes.SellOnly);
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
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                SellAmount = discount_amount * sell_order.Price,
                Balances = new Dictionary<string, long>(),
                DestinationAccountId = sell_order.AccountID,
                TradeOrderId = sell_order.Hash
            };

            var redeem_result = await TradeAsync(redeem_block);
            return redeem_result;
        }


        public async Task<AuthorizationAPIResult> ExecuteSellOrderAsync(TradeBlock trade, TradeOrderBlock order, NonFungibleToken nonfungible_token = null)
        {
            TransactionBlock previousBlock = GetLatestBlock();
            if (previousBlock == null)
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.PreviousBlockNotFound };

            //int sell_precision = await FindTokenPrecision(trade.BuyTokenCode);
            //if (sell_precision < 0)
            //    return new AuthorizationAPIResult() { ResultCode = APIResultCodes.TokenGenesisBlockNotFound };

            if (order == null)
            {
                order = await GetBlockByHashAsync(trade.TradeOrderId) as TradeOrderBlock;
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

            execute_block.ServiceHash = await GetLastServiceBlockHashAsync();

            execute_block.InitializeBlock(previousBlock, PrivateKey, AccountId);

            // TO DO - override the trasanction validation method in ExecuteTradeBlock
            //if (!execute_block.ValidateTransaction(previousBlock))
            //    return new AuthorizationAPIResult() { ResultCode = APIResultCodes.SendTransactionValidationFailed };

            var result = await _rpcClient.ExecuteTradeOrderAsync(execute_block);

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
        }*/

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


        private async Task<TransactionBlock> GetLatestBlockAsync()
        {
            var result = await _rpcClient.GetLastBlockAsync(AccountId);
            if (result.Successful())
                return result.GetBlock() as TransactionBlock;

            throw new Exception($"Can't get wallet state: {result.ResultCode}");
        }

        public string GetDisplayBalances()
        {
            string res = "0";
            TransactionBlock lastBlock = GetLastSyncBlock();
            if (lastBlock != null)
            {
                res = $"\n";
                foreach (var balance in lastBlock.Balances)
                {
                    res += $"    {balance.Value.ToBalanceDecimal()} {balance.Key}\n";
                }
                //if (lastBlock.NonFungibleToken != null)
                //{
                //    var sent_or_receive = lastBlock is ReceiveTransferBlock ? "received" : "sent";
                //    res += $"Last Non-Fungible Token {sent_or_receive}: {lastBlock.NonFungibleToken.TokenCode}  \n";
                //    res += await GetDisplayNFTInstanceAsync(lastBlock.NonFungibleToken);
                //}
            }
            return res;
        }

        // returns a list of all NFT instances owned by the account
        public async Task<List<NonFungibleToken>> GetNonFungibleTokensAsync()
        {
            var res = await _rpcClient.GetNonFungibleTokensAsync(AccountId, SignAPICall());
            if (res != null && res.Successful())
                return res.GetList();
            else
                return null;
        }

        public async Task<string> GetDisplayNFTInstanceAsync(NonFungibleToken nft)
        {
            string res = string.Empty;
            var genesis_block = await GetTokenGenesisBlockAsync(nft.TokenCode);
            if (genesis_block == null)
            {
                res += $"    Cannot retrieve token genesis block for {nft.TokenCode}  \n";
                return res;
            }

            if (genesis_block.ContractType == ContractTypes.Collectible)
            {
                res += $"    Collectible NFT: {nft.TokenCode} \n";
                res += $"    Serial Number: {nft.SerialNumber}  \n";
            }
            else
            {
                if (!string.IsNullOrEmpty(nft.RedemptionCode))
                {
                    var issuer_account_id = genesis_block.AccountID;
                    var decryptor = new ECC_DHA_AES_Encryptor();
                    string decrypted_redemption_code = decryptor.Decrypt(PrivateKey, issuer_account_id, nft.SerialNumber, nft.RedemptionCode);
                    res += $"    Shopify Discount: {nft.TokenCode} \n";
                    res += $"    Discount Amount: {nft.Denomination:C} \n";
                    res += $"    Discount Code: {decrypted_redemption_code}  \n";
                }
            }
            if (!string.IsNullOrEmpty(genesis_block.Description))
                res += $"    Description: {genesis_block.Description}  \n";
            if (!string.IsNullOrEmpty(genesis_block.Owner))
                res += $"    Issuer: {genesis_block.Owner}  \n";
            if (!string.IsNullOrEmpty(genesis_block.Address))
                res += $"    Address: {genesis_block.Address}  \n";
            if (!string.IsNullOrEmpty(genesis_block.Icon))
                res += $"    Icon: {genesis_block.Icon}  \n";
            if (!string.IsNullOrEmpty(genesis_block.Image))
                res += $"    Image: {genesis_block.Image}  \n";
            return res;
        }

        public async Task<(string name, decimal Denomination, string Redemption)> NonFungToStringAsync(NonFungibleToken nftoken)
        {
            var tgb = await GetTokenGenesisBlockAsync(nftoken.TokenCode);
            if (tgb != null)
            {
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
            var last = _lastSyncBlock;
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

        public async Task<TransactionBlock> GetBlockByIndexAsync(long Index)
        {
            var blockResult = await _rpcClient.GetBlockByIndexAsync(AccountId, Index);
            if (blockResult.ResultCode == APIResultCodes.Success)
                return blockResult.GetBlock() as TransactionBlock;
            else
                return null;
        }

        public async Task<TransactionBlock> GetBlockByHashAsync(string Hash)
        {
            var blockResult = await _rpcClient.GetBlockByHashAsync(AccountId, Hash, SignAPICall());
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


        public async Task<AuthorizationAPIResult> SendAsync(decimal Amount, string DestinationAccountId, string ticker = LyraGlobal.OFFICIALTICKERCODE)
        {
            // verify input
            AuthorizationAPIResult result;

            if (Amount <= 0)
            {
                result = new AuthorizationAPIResult { ResultCode = APIResultCodes.InvalidAmountToSend };
                return result;
            }

            if (!Signatures.ValidateAccountId(DestinationAccountId) || DestinationAccountId == AccountId)
            {
                result = new AuthorizationAPIResult { ResultCode = APIResultCodes.InvalidAccountId };
                return result;
            }

            var currentSvcBlock = await _rpcClient.GetLastServiceBlockAsync();

            while (true)
            {
                result = await SendOnceAsync(Amount, DestinationAccountId, ticker, null);
                if (result.ResultCode == APIResultCodes.ConsensusTimeout
                    || result.ResultCode == APIResultCodes.ServiceBlockNotFound)
                {
                    bool viewChanged = false;
                    for (int i = 0; i < 30; i++)       // wait 30 seconds for consensus network to recovery
                    {
                        var nextSvcBlock = await _rpcClient.GetLastServiceBlockAsync();
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

        public async Task<AuthorizationAPIResult> SendExAsync(string DestinationAccountId, Dictionary<string, decimal> Amounts, Dictionary<string, string> tags)
        {
            var previousBlock = await GetLatestBlockAsync();
            if (previousBlock == null)
                throw new Exception("Network offline.");

            if (!previousBlock.Balances.ContainsKey(LyraGlobal.OFFICIALTICKERCODE) ||
                previousBlock.Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal() <
                    TransferFee + (Amounts.ContainsKey(LyraGlobal.OFFICIALTICKERCODE)
                    ? Amounts[LyraGlobal.OFFICIALTICKERCODE]
                    : 0)
                )
                return new AuthorizationAPIResult { 
                    ResultCode = APIResultCodes.InsufficientFunds,
                    ResultMessage = $"Sending {Amounts[LyraGlobal.OFFICIALTICKERCODE]} LYR",
                };

            if (Amounts.Any(a => a.Value <= 0m))
                throw new Exception("Amount must > 0");

            if (previousBlock == null)
            {
                //throw new Exception("Previous block not found");
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.PreviousBlockNotFound };
            }

            // check tokens exists
            if (Amounts.Keys.Any(a => !previousBlock.Balances.ContainsKey(a)))
            {
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.TokenNotFound };
            }

            // check if fee is enough
            // check if amounts is enough

            ////long atomicamount = (long)(Amount * (decimal)Math.Pow(10, precision));
            //var balance_change = Amount;

            ////var transaction = new TransactionInfo() { TokenCode = ticker, Amount = atomicamount };

            var fee = TransferFee;

            SendTransferBlock sendBlock = new SendTransferBlock()
            {
                AccountID = AccountId,
                VoteFor = VoteFor,
                ServiceHash = await GetLastServiceBlockHashAsync(), //svcBlockResult.GetBlock().Hash,
                DestinationAccountId = DestinationAccountId,
                Balances = new Dictionary<string, long>(),
                Tags = tags,
                Fee = fee,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = fee == 0m ? AuthorizationFeeTypes.NoFee : AuthorizationFeeTypes.Regular
            };

            // transfer unchanged token balances from the previous block
            foreach (var balance in previousBlock.Balances)
            {
                if (Amounts.ContainsKey(balance.Key))
                {
                    sendBlock.Balances.Add(balance.Key, (balance.Value.ToBalanceDecimal() - Amounts[balance.Key]).ToBalanceLong());
                }
                else
                {
                    sendBlock.Balances.Add(balance.Key, balance.Value);
                }
            }
            // substract the fee
            // for customer tokens, we pay fee in LYR (unless they are accepted by authorizers as a fee - TO DO)
            sendBlock.Balances[LyraGlobal.OFFICIALTICKERCODE] = (sendBlock.Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal() - fee).ToBalanceLong();

            sendBlock.InitializeBlock(previousBlock, PrivateKey, AccountId);
            if (!sendBlock.VerifyHash())
                throw new Exception("Send Block hash verify failed.");

            if (!sendBlock.ValidateTransaction(previousBlock))
            {
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.SendTransactionValidationFailed };
                //throw new Exception("ValidateTransaction failed");
            }

            //sendBlock.Signature = Signatures.GetSignature(PrivateKey, sendBlock.Hash);
            AuthorizationAPIResult result;
            //var stopwatch = Stopwatch.StartNew();
            result = await _rpcClient.SendTransferAsync(sendBlock);
            //stopwatch.Stop();
            //PrintConLine($"_rpcClient.SendTransfer: {stopwatch.ElapsedMilliseconds} ms.");

            if(result.Successful())
            {
                _lastSyncBlock = sendBlock;
            }
            return result;
        }

        private async Task<AuthorizationAPIResult> SendOnceAsync(decimal Amount, string DestinationAccountId, string ticker, Dictionary<string, string> tags)
        {
            Trace.Assert(Amount > 0);
            if (Amount <= 0)
                throw new Exception("Amount must > 0");

            TransactionBlock previousBlock = await GetLatestBlockAsync();
            if (previousBlock == null)
            {
                //throw new Exception("Previous block not found");
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

            var fee = TransferFee;

            if (ticker == LyraGlobal.OFFICIALTICKERCODE)
                balance_change += fee;

            // see if we have enough tokens
            if (previousBlock.Balances[ticker] < balance_change.ToBalanceLong())
            {
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.InsufficientFunds };
                //throw new Exception("Insufficient funds");
            }

            // see if we have enough LYR to pay the transfer fee
            if (ticker != LyraGlobal.OFFICIALTICKERCODE)
                if (!previousBlock.Balances.ContainsKey(LyraGlobal.OFFICIALTICKERCODE) || previousBlock.Balances[LyraGlobal.OFFICIALTICKERCODE] < fee.ToBalanceLong())
                {
                    //throw new Exception("Insufficient funds to pay transfer fee");
                    return new AuthorizationAPIResult() { ResultCode = APIResultCodes.InsufficientFunds };
                }

            //var svcBlockResult = await _rpcClient.GetLastServiceBlock(AccountId, SignAPICallAsync());
            //if (svcBlockResult.ResultCode != APIResultCodes.Success)
            //{
            //    throw new Exception("Unable to get latest service block.");
            //}

            SendTransferBlock sendBlock = new SendTransferBlock()
            {
                AccountID = AccountId,
                VoteFor = VoteFor,
                ServiceHash = await GetLastServiceBlockHashAsync(), //svcBlockResult.GetBlock().Hash,
                DestinationAccountId = DestinationAccountId,
                Balances = new Dictionary<string, long>(),
                //PaymentID = string.Empty,
                Tags = tags,
                Fee = fee,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = fee == 0m ? AuthorizationFeeTypes.NoFee : AuthorizationFeeTypes.Regular
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
                //throw new Exception("ValidateTransaction failed");
            }

            //sendBlock.Signature = Signatures.GetSignature(PrivateKey, sendBlock.Hash);
            AuthorizationAPIResult result;
            //var stopwatch = Stopwatch.StartNew();
            result = await _rpcClient.SendTransferAsync(sendBlock);
            //stopwatch.Stop();
            //PrintConLine($"_rpcClient.SendTransfer: {stopwatch.ElapsedMilliseconds} ms.");
            if (result.Successful())
            {
                _lastSyncBlock = sendBlock;
            }
            return result;
        }

        // issues a new instance of collectible NFT
        public async Task<AuthorizationAPIResult> IssueNFTAsync(string DestinationAccountId, string ticker, string SerialNumber)
        {
            //var nft_genesis = await _rpcClient.GetTokenGenesisBlock(AccountId, ticker, SignAPICallAsync());
            //if (nft_genesis == null || nft_genesis.GetBlock() == null)
            //    return new AuthorizationAPIResult() { ResultCode = APIResultCodes.TokenGenesisBlockNotFound };
            NonFungibleToken nft = new NonFungibleToken
            {
                SerialNumber = SerialNumber,
                TokenCode = ticker,
                Denomination = 1
            };
            nft.Sign(PrivateKey, AccountId);

            return await SendNFTInternalAsync(DestinationAccountId, ticker, nft);
        }

        // Transfers and existing instance of collectible NFT to another account
        public async Task<AuthorizationAPIResult> SendNFTAsync(string DestinationAccountId, string ticker, string SerialNumber)
        {
            var height = GetLocalAccountHeight();
            ReceiveTransferBlock receive_token_block = null;

            // Scan the current account and find the receive block that continas the token that we want to send
            for (var index = height; index >= 0; index--)
            {

                var block = await GetBlockByIndexAsync(index);
                if (!(block is ReceiveTransferBlock))
                    continue;
                if (!block.ContainsNonFungibleToken())
                    continue;

                if (block.NonFungibleToken.TokenCode == ticker && block.NonFungibleToken.SerialNumber == SerialNumber)
                {
                    receive_token_block = block as ReceiveTransferBlock;
                    break;
                }
            }

            if (receive_token_block == null)
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.NFTInstanceNotFound };

            return await SendNFTInternalAsync(DestinationAccountId, ticker, receive_token_block.NonFungibleToken);
        }

        // Transfers and existing instance or issues a new instance of collectible NFT - this method can be used by either IssueNFT and SendNFT
        private async Task<AuthorizationAPIResult> SendNFTInternalAsync(string DestinationAccountId, string ticker, NonFungibleToken nft)
        {
            TransactionBlock previousBlock = await GetLatestBlockAsync();
            if (previousBlock == null)
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.PreviousBlockNotFound };

            decimal balance_change = 1;

            var fee = TransferFee;

            // see if we have enough tokens
            if (previousBlock.Balances[ticker] < balance_change.ToBalanceLong())
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.InsufficientFunds };

            // see if we have enough LYR to pay the transfer fee
            if (!previousBlock.Balances.ContainsKey(LyraGlobal.OFFICIALTICKERCODE) || previousBlock.Balances[LyraGlobal.OFFICIALTICKERCODE] < fee.ToBalanceLong())
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.InsufficientFunds };

            SendTransferBlock sendBlock;

            sendBlock = new SendTransferBlock()
            {
                AccountID = AccountId,
                VoteFor = VoteFor,
                ServiceHash = await GetLastServiceBlockHashAsync(),
                DestinationAccountId = DestinationAccountId,
                Balances = new Dictionary<string, long>(),
                Fee = fee,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = fee == 0m ? AuthorizationFeeTypes.NoFee : AuthorizationFeeTypes.Regular,
                NonFungibleToken = nft
            };

            sendBlock.Balances.Add(ticker, previousBlock.Balances[ticker] - balance_change.ToBalanceLong());

            // for customer tokens, we pay fee in LYR (unless they are accepted by authorizers as a fee - TO DO)
            sendBlock.Balances.Add(LyraGlobal.OFFICIALTICKERCODE, previousBlock.Balances[LyraGlobal.OFFICIALTICKERCODE] - fee.ToBalanceLong());

            // transfer unchanged token balances from the previous block
            foreach (var balance in previousBlock.Balances)
                if (!(sendBlock.Balances.ContainsKey(balance.Key)))
                    sendBlock.Balances.Add(balance.Key, balance.Value);

            sendBlock.InitializeBlock(previousBlock, PrivateKey, AccountId);

            if (!sendBlock.ValidateTransaction(previousBlock))
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.SendTransactionValidationFailed };

            AuthorizationAPIResult result;
            result = await _rpcClient.SendTransferAsync(sendBlock);
            if (result.Successful())
            {
                _lastSyncBlock = sendBlock;
            }
            return result;
        }
        /*
        public async Task<TradeOrderAuthorizationAPIResult> TradeOrderAsync(
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
                ServiceHash = await GetLastServiceBlockHashAsync(),
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

            var result = await _rpcClient.TradeOrderAsync(tradeBlock);

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
        public async Task<AuthorizationAPIResult> TradeAsync(TradeBlock trade)
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

            trade.ServiceHash = await GetLastServiceBlockHashAsync();
            trade.InitializeBlock(previousBlock, PrivateKey, AccountId);

            var trade_result = await _rpcClient.TradeAsync(trade);

            if (trade_result.ResultCode == APIResultCodes.BlockSignatureValidationFailed)
            {
                PrintConLine($"BlockSignatureValidationFailed");
                PrintConLine("Error Message: ");
                PrintConLine(trade_result.ResultMessage);
                PrintConLine("Local Block: ");
                PrintConLine(trade.Print());
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
        public async Task<AuthorizationAPIResult> CancelTradeOrderAsync(string OrderId)
        {
            var order = await GetBlockByHashAsync(OrderId) as TradeOrderBlock;
            if (order == null)
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.BlockNotFound };

            var cancelBlock = new CancelTradeOrderBlock
            {
                AccountID = AccountId,
                ServiceHash = await GetLastServiceBlockHashAsync(),
                Balances = new Dictionary<string, long>(),
                FeeType = AuthorizationFeeTypes.NoFee,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                TradeOrderId = OrderId
            };

            var previous_to_order_block = await GetBlockByHashAsync(order.PreviousHash);
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

            var result = await _rpcClient.CancelTradeOrderAsync(cancelBlock);

            if (result.ResultCode == APIResultCodes.Success)
            {
                _lastTransactionBlock = cancelBlock;
            }
            else
            if (result.ResultCode == APIResultCodes.BlockSignatureValidationFailed)
            {
                PrintConLine($"BlockSignatureValidationFailed");
                PrintConLine("Error Message: " + result.ResultMessage);
                PrintConLine("Local Block: " + JsonConvert.SerializeObject(cancelBlock));
            }
            else
            {
                PrintConLine("Authorization failed" + result.ResultCode.ToString());
                PrintConLine("Error Message: " + result.ResultMessage);
            }
            return result;
        }

        public async Task<ActiveTradeOrdersAPIResult> GetActiveTradeOrdersAsync(string SellToken, string BuyToken, TradeOrderListTypes OrderType)
        {
            var result = await _rpcClient.GetActiveTradeOrdersAsync(AccountId, SellToken, BuyToken, OrderType, SignAPICall());
            return result;
        }*/

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

        //    PrintConLine("Buy Orders:");
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

        //            //PrintConLine($"Found Token Genesis Block for {genesisBlock.Ticker}");
        //            //PrintConLine("Balance: " + GetDisplayBalances());
        //            ////PrintCon(string.Format("{0}> ", AccountName));
        //        }
        //    }

        //    if (genesisBlock != null)
        //        precision = (int)genesisBlock.Precision;

        //    return precision;
        //}

        private async Task<AuthorizationAPIResult> OpenStandardAccountWithReceiveBlockAsync(NewTransferAPIResult2 new_transfer_info)
        {
            var svcBlockResult = await _rpcClient.GetLastServiceBlockAsync();
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
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                NonFungibleToken = new_transfer_info.NonFungibleToken,
                VoteFor = VoteFor
            };

            foreach (var chg in new_transfer_info.Transfer.Changes)
            {
                openReceiveBlock.Balances.Add(chg.Key, chg.Value.ToBalanceLong());
            }
            openReceiveBlock.InitializeBlock(null, PrivateKey, AccountId);

            //openReceiveBlock.Signature = Signatures.GetSignature(PrivateKey, openReceiveBlock.Hash);

            var result = await _rpcClient.ReceiveTransferAndOpenAccountAsync(openReceiveBlock);

            if (result.ResultCode == APIResultCodes.BlockSignatureValidationFailed)
            {
                PrintConLine($"BlockSignatureValidationFailed");
                PrintConLine("Error Message: ");
                PrintConLine(result.ResultMessage);
                PrintConLine("Local Block: ");
                PrintConLine(openReceiveBlock.Print());
            }
            else
            if (result.ResultCode != APIResultCodes.Success)
            {
                PrintConLine($"ReceiveTransferAndOpenAccount: Failed to authorize receive transfer block with error code: {result.ResultCode}");
                PrintConLine("Error Message: " + result.ResultMessage);
            }
            else
            {
                PrintConLine($"Receive transfer block has been authorized successfully");
                PrintConLine("Balance: " + GetDisplayBalances());
            }

            if (result.Successful())
            {
                _lastSyncBlock = openReceiveBlock;
            }

            //PrintCon(string.Format("{0}> ", AccountName));
            return result;
        }

/*        private async Task<AuthorizationAPIResult> OpenStandardAccountWithImportAsync(string ImportPrivateKey, TransactionBlock last_imported_block, string imported_account_id)
        {
            var open_import_block = new OpenAccountWithImportBlock
            {
                AccountType = AccountTypes.Standard,
                AccountID = AccountId,
                ImportedAccountId = imported_account_id,
                ServiceHash = await GetLastServiceBlockHashAsync(),
                SourceHash = null,
                Balances = new Dictionary<string, long>(),
                Fee = 0,
                FeeType = AuthorizationFeeTypes.NoFee,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
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

            var result = await _rpcClient.OpenAccountWithImportAsync(open_import_block);

            await ProcessResultAsync(result, "Import Account", open_import_block);

            return result;
        }*/

        private async Task ProcessResultAsync(AuthorizationAPIResult result, string OperationName, TransactionBlock block)
        {
            if (result.ResultCode == APIResultCodes.BlockSignatureValidationFailed)
            {
                PrintConLine($"BlockSignatureValidationFailed");
                PrintConLine("Error Message: ");
                PrintConLine(result.ResultMessage);
                PrintConLine("Local Block: ");
                PrintConLine(block.Print());
            }
            else
            if (result.ResultCode != APIResultCodes.Success)
            {
                PrintConLine(OperationName + $": Operation failed with result: {result.ResultCode}");
                PrintConLine("Error Message: " + result.ResultMessage);
            }
            else
            {
                PrintConLine(OperationName + $": Operation success");
                PrintConLine("Balance: " + GetDisplayBalances());
            }
            //PrintCon(string.Format("{0}> ", AccountName));
        }

/*        public async Task<AuthorizationAPIResult> ImportAccountAsync(string ImportPrivateKey)
        {
            throw new Exception("Wallet import obsolete");

            string imported_account_id = Signatures.GetAccountIdFromPrivateKey(ImportPrivateKey);
            var imported_account_height_result = await _rpcClient.GetAccountHeightAsync(imported_account_id);
            TransactionBlock last_imported_block = null;
            if (imported_account_height_result.Successful())
            {
                long imported_account_height = imported_account_height_result.Height;

                var last_imported_block_result = await _rpcClient.GetBlockByIndexAsync(imported_account_id, imported_account_height);
                if (!last_imported_block_result.Successful())
                    throw new Exception("Unable to get latest imported account block.");

                last_imported_block = last_imported_block_result.GetBlock() as TransactionBlock;
                if (last_imported_block == null)
                    throw new Exception("Unable to get latest imported account block.");
            }

            if (GetLocalAccountHeight() == 0) // if this is new account with no blocks
                return await OpenStandardAccountWithImportAsync(ImportPrivateKey, last_imported_block, imported_account_id);

            TransactionBlock previousBlock = await GetLatestBlockAsync();
            if (previousBlock == null)
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.PreviousBlockNotFound };

            var import_block = new ImportAccountBlock
            {
                AccountID = AccountId,
                ImportedAccountId = imported_account_id,
                ServiceHash = await GetLastServiceBlockHashAsync(),
                SourceHash = null,
                Balances = new Dictionary<string, long>(),
                Fee = 0,
                FeeType = AuthorizationFeeTypes.NoFee,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
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
                    throw new Exception("ValidateTransaction failed");

            import_block.InitializeBlock(previousBlock, PrivateKey, AccountId);

            import_block.ImportedAccountSignature = Signatures.GetSignature(ImportPrivateKey, import_block.Hash, imported_account_id);

            var result = await _rpcClient.ImportAccountAsync(import_block);

            await ProcessResultAsync(result, "Import Account", import_block);

            return result;
        }*/

        private async Task<AuthorizationAPIResult> ReceiveTransferAsync(NewTransferAPIResult2 new_transfer_info)
        {

            // *** Slava - July 19, 2020 - I am not sure if we need this call anymore? 
            //await FindTokenPrecision(new_transfer_info.Transfer.TokenCode);
            // ***

            if (GetLocalAccountHeight() == 0) // if this is new account with no blocks
                return await OpenStandardAccountWithReceiveBlockAsync(new_transfer_info);

            var svcBlockResult = await _rpcClient.GetLastServiceBlockAsync();
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
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                NonFungibleToken = new_transfer_info.NonFungibleToken
            };

            TransactionBlock latestBlock = await GetLatestBlockAsync();

            //var latestBalances = latestBlock.Balances.ToDecimalDict();
            var recvBalances = latestBlock.Balances.ToDecimalDict();
            foreach (var chg in new_transfer_info.Transfer.Changes)
            {
                if (recvBalances.ContainsKey(chg.Key))
                {
                    recvBalances[chg.Key] += chg.Value;
                    //Console.WriteLine($"Receiving {chg.Key}: {chg.Value}");
                }                    
                else
                    recvBalances.Add(chg.Key, chg.Value);
            }

            receiveBlock.Balances = recvBalances.ToLongDict();

            receiveBlock.InitializeBlock(latestBlock, PrivateKey, AccountId);

            if (!receiveBlock.ValidateTransaction(latestBlock))
                throw new Exception("ValidateTransaction failed");

            //receiveBlock.Signature = Signatures.GetSignature(PrivateKey, receiveBlock.Hash);

            var result = await _rpcClient.ReceiveTransferAsync(receiveBlock);
            await ProcessResultAsync(result, "Receive Transfer", receiveBlock);

            if (result.Successful())
            {
                _lastSyncBlock = receiveBlock;
            }

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


        /* This is obsolete */

        //public async Task<APIResultCodes> CreateGenesisForCoreTokenAsync()
        //{
        //    var svcBlockResult = await _rpcClient.GetLastServiceBlock();
        //    if (svcBlockResult.ResultCode != APIResultCodes.Success)
        //    {
        //        throw new Exception("Unable to get latest service block.");
        //    }

        //    // initiate test coins
        //    var openTokenGenesisBlock = new LyraTokenGenesisBlock
        //    {
        //        AccountType = AccountTypes.Standard,
        //        Ticker = LyraGlobal.OFFICIALTICKERCODE,
        //        DomainName = LyraGlobal.OFFICIALDOMAIN,
        //        ContractType = ContractTypes.Cryptocurrency,
        //        Description = LyraGlobal.PRODUCTNAME + " Gas Token",
        //        Precision = LyraGlobal.OFFICIALTICKERPRECISION,
        //        IsFinalSupply = true,
        //        AccountID = AccountId,
        //        Balances = new Dictionary<string, long>(),
        //        ServiceHash = svcBlockResult.GetBlock().Hash,
        //        Fee = TokenGenerationFee,
        //        FeeType = AuthorizationFeeTypes.Regular,
        //        Icon = "https://i.imgur.com/L3h0J1K.png",
        //        Image = "https://i.imgur.com/B8l4ZG5.png",
        //        RenewalDate = DateTime.UtcNow.AddYears(100)
        //    };
        //    // TO DO - set service hash
        //    var transaction = new TransactionInfo() { TokenCode = openTokenGenesisBlock.Ticker, Amount = LyraGlobal.OFFICIALGENESISAMOUNT };
        //    //var transaction = new TransactionInfo() { TokenCode = openTokenGenesisBlock.Ticker, Amount = 150000000 };

        //    openTokenGenesisBlock.Balances.Add(transaction.TokenCode, transaction.Amount.ToBalanceLong()); // This is current supply in atomic units (1,000,000.00)
        //                                                                                                   //openTokenGenesisBlock.Transaction = transaction;
        //    openTokenGenesisBlock.InitializeBlock(null, PrivateKey, AccountId);

        //    //openTokenGenesisBlock.Signature = Signatures.GetSignature(PrivateKey, openTokenGenesisBlock.Hash);

        //    var result = await _rpcClient.OpenAccountWithGenesis(openTokenGenesisBlock);

        //    if (result.ResultCode == APIResultCodes.BlockSignatureValidationFailed)
        //    {
        //        PrintConLine($"BlockSignatureValidationFailed");
        //        PrintConLine("Error Message: ");
        //        PrintConLine(result.ResultMessage);
        //        PrintConLine("Local Block: ");
        //        PrintConLine(openTokenGenesisBlock.Print());
        //    }
        //    else
        //    if (result.ResultCode != APIResultCodes.Success)
        //    {
        //        PrintConLine($"Failed to add genesis block with error code: {result.ResultCode}");
        //        PrintConLine("Error Message: " + result.ResultMessage);
        //        PrintConLine(openTokenGenesisBlock.Print());

        //    }
        //    else
        //    {
        //        _lastTransactionBlock = openTokenGenesisBlock;
        //        PrintConLine($"Genesis block has been authorized successfully");
        //        PrintConLine("Balance: " + await GetDisplayBalancesAsync());
        //    }
        //    //PrintCon(string.Format("{0}> ", AccountName));
        //    return result.ResultCode;
        //}


        public async Task<AuthorizationAPIResult> CreateTokenAsync(
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

            TransactionBlock latestBlock = await GetLatestBlockAsync();
            if (latestBlock == null || latestBlock.Balances[LyraGlobal.OFFICIALTICKERCODE] < TokenGenerationFee.ToBalanceLong())
            {
                //throw new Exception("Insufficent funds");
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.InsufficientFunds };
            }

            var svcBlockResult = await _rpcClient.GetLastServiceBlockAsync();
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
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.Regular,
                Owner = owner,
                Address = address,
                Currency = currency,
                Tags = tags,
                RenewalDate = DateTime.UtcNow.Add(TimeSpan.FromDays(3650)),
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

            var result = await _rpcClient.CreateTokenAsync(tokenBlock);

            if (result.ResultCode == APIResultCodes.BlockSignatureValidationFailed)
            {
                PrintConLine($"BlockSignatureValidationFailed");
                PrintConLine("Error Message: ");
                PrintConLine(result.ResultMessage);
                PrintConLine("Local Block: ");
                PrintConLine(tokenBlock.Print());
            }

            if (result.Successful())
            {
                _lastSyncBlock = tokenBlock;
            }

            return result;
        }

        // Creates Custom User-defined Collectible NFT
        public async Task<AuthorizationAPIResult> CreateNFTAsync(
            string tokenName,
            string domainName,
            string description,
            decimal supply,
            bool isFinalSupply,
            string owner, // shop name
            string address, // shop URL
            string icon, // icon URL
            string image, // image URL
            Dictionary<string, string> tags)
        {
            if (string.IsNullOrWhiteSpace(domainName))
                domainName = "Custom";

            string ticker = domainName + "/" + tokenName;

            TransactionBlock latestBlock = await GetLatestBlockAsync();
            if (latestBlock == null || latestBlock.Balances[LyraGlobal.OFFICIALTICKERCODE] < TokenGenerationFee.ToBalanceLong())
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.InsufficientFunds };

            var svcBlockResult = await _rpcClient.GetLastServiceBlockAsync();
            if (svcBlockResult.ResultCode != APIResultCodes.Success)
                throw new Exception("Unable to get latest service block.");

            TokenGenesisBlock tokenBlock = new TokenGenesisBlock
            {
                Ticker = ticker,
                DomainName = domainName,
                Description = description,
                Precision = 0,
                IsFinalSupply = isFinalSupply,
                AccountID = AccountId,
                Balances = new Dictionary<string, long>(),
                ServiceHash = svcBlockResult.GetBlock().Hash,
                Fee = TokenGenerationFee,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.Regular,
                Owner = owner,
                Address = address,
                Tags = tags,
                RenewalDate = DateTime.UtcNow.Add(TimeSpan.FromDays(3650)),
                ContractType = ContractTypes.Collectible,
                Icon = icon,
                Image = image,
                VoteFor = VoteFor,
                IsNonFungible = true,
                NonFungibleType = NonFungibleTokenTypes.Collectible,
                NonFungibleKey = AccountId
            };

            var transaction = new TransactionInfo() { TokenCode = ticker, Amount = supply };

            tokenBlock.Balances.Add(transaction.TokenCode, transaction.Amount.ToBalanceLong()); // This is current supply in atomic units (1,000,000.00)
            tokenBlock.Balances.Add(LyraGlobal.OFFICIALTICKERCODE, latestBlock.Balances[LyraGlobal.OFFICIALTICKERCODE] - TokenGenerationFee.ToBalanceLong());

            // transfer unchanged token balances from the previous block
            foreach (var balance in latestBlock.Balances)
                if (!(tokenBlock.Balances.ContainsKey(balance.Key)))
                    tokenBlock.Balances.Add(balance.Key, balance.Value);

            tokenBlock.InitializeBlock(latestBlock, PrivateKey, AccountId);

            var result = await _rpcClient.CreateTokenAsync(tokenBlock);

            await ProcessResultAsync(result, "CreateNFT", tokenBlock);

            return result;
        }

        private async Task<string?[]> GetProperTokenNameAsync(string[] tokenNames)
        {
            var result = await tokenNames.SelectAsync(async a => await _rpcClient.GetTokenGenesisBlockAsync(AccountId, a, SignAPICall()));
            return result.Select(a => a.GetBlock() as TokenGenesisBlock)
                .Select(b => b?.Ticker)
                .OrderBy(a => a)
                .ToArray();
        }

        public async Task<PoolInfoAPIResult> GetLiquidatePoolAsync(string token0, string token1)
        {
            var result = await _rpcClient.GetPoolAsync(token0, token1);
            return result;
        }

        public async Task<AuthorizationAPIResult> CreateLiquidatePoolAsync(string token0, string token1)
        {
            var tokenNames = await GetProperTokenNameAsync(new[] { token0, token1 });

            if (tokenNames.Any(a => a == null))
                return new AuthorizationAPIResult { ResultCode = APIResultCodes.TokenGenesisBlockNotFound };

            var pool = await _rpcClient.GetPoolAsync(tokenNames[0], tokenNames[1]);
            if (pool.PoolAccountId != null)
                return new AuthorizationAPIResult { ResultCode = APIResultCodes.PoolAlreadyExists };

            var tags = new Dictionary<string, string>
            {
                { "token0", tokenNames[0] },
                { "token1", tokenNames[1] },
                { Block.REQSERVICETAG, BrokerActions.BRK_POOL_CRPL }
            };
            var amounts = new Dictionary<string, decimal>
            {
                { LyraGlobal.OFFICIALTICKERCODE, PoolFactoryBlock.PoolCreateFee }
            };
            return await SendExAsync(pool.PoolFactoryAccountId, amounts, tags);
        }

        public async Task<AuthorizationAPIResult> AddLiquidateToPoolAsync(string token0, decimal token0Amount, string token1, decimal token1Amount)
        {
            var pool = await _rpcClient.GetPoolAsync(token0, token1);
            if (pool.PoolAccountId == null)
                return new AuthorizationAPIResult { ResultCode = APIResultCodes.PoolNotExists };

            var amountsDeposit = new Dictionary<string, decimal>
            {
                { token0, token0Amount },
                { token1, token1Amount }
            };

            var tags = new Dictionary<string, string>
            {
                { "poolid", pool.PoolAccountId },
                { Block.REQSERVICETAG, BrokerActions.BRK_POOL_ADDLQ }
            };

            var poolDepositResult = await SendExAsync(pool.PoolAccountId, amountsDeposit, tags);
            return poolDepositResult;
        }

        public async Task<AuthorizationAPIResult> RemoveLiquidateFromPoolAsync(string token0, string token1)
        {
            var pool = await _rpcClient.GetPoolAsync(token0, token1);
            if (pool.PoolAccountId == null)
                return new AuthorizationAPIResult { ResultCode = APIResultCodes.PoolNotExists };

            var tags = new Dictionary<string, string>
            {
                { Block.REQSERVICETAG, BrokerActions.BRK_POOL_RMLQ },
                { "poolid", pool.PoolAccountId },
            };
            var amounts = new Dictionary<string, decimal>
            {
                { LyraGlobal.OFFICIALTICKERCODE, 1m }
            };
            var poolWithdrawResult = await SendExAsync(pool.PoolFactoryAccountId, amounts, tags);
            return poolWithdrawResult;
        }

        public async Task<AuthorizationAPIResult> SwapTokenAsync(string token0, string token1, string tokenToSwap, decimal amountToSwap, decimal amountToGet)
        {
            var pool = await _rpcClient.GetPoolAsync(token0, token1);
            if (pool.PoolAccountId == null)
                return new AuthorizationAPIResult { ResultCode = APIResultCodes.PoolNotExists };

            var tags = new Dictionary<string, string>
            {
                { Block.REQSERVICETAG, BrokerActions.BRK_POOL_SWAP },
                { "poolid", pool.PoolAccountId },
                { "minrecv", $"{amountToGet.ToBalanceLong()}" }
            };
            var amounts = new Dictionary<string, decimal>
            {
                { tokenToSwap, amountToSwap }
            };
            var swapTokenResult = await SendExAsync(pool.PoolAccountId, amounts, tags);
            return swapTokenResult;
        }

        #region Staking Account
        public async Task<BlockAPIResult> CreateProfitingAccountAsync(string Name, ProfitingType ptype, decimal shareRito, int maxVoter)
        {
            var tags = new Dictionary<string, string>
            {
                { Block.REQSERVICETAG, BrokerActions.BRK_PFT_CRPFT },
                { "name", Name },   // get by name. name can't duplicate
                { "ptype", ptype.ToString() },
                { "share", $"{shareRito}" },
                { "seats", $"{maxVoter}" }
            };
            var amounts = new Dictionary<string, decimal>
            {
                { LyraGlobal.OFFICIALTICKERCODE, PoolFactoryBlock.ProfitingAccountCreateFee }
            };
            var result = await SendExAsync(PoolFactoryBlock.FactoryAccount, amounts, tags);
            if (result.ResultCode != APIResultCodes.Success)
                return new BlockAPIResult { ResultCode = result.ResultCode };

            var latx = await GetLatestBlockAsync();
            for (int i = 0; i < 60; i++)
            {
                // then find by RelatedTx                
                var blocks = await _rpcClient.GetBlocksByRelatedTxAsync(latx.Hash);
                if (blocks.Successful())
                {
                    var txs = blocks.GetBlocks();
                    var gen = txs.FirstOrDefault(a => a is ProfitingBlock pb && pb.OwnerAccountId == AccountId);
                    if (gen != null)
                    {
                        var ret = new BlockAPIResult
                        {
                            ResultCode = APIResultCodes.Success,
                        };
                        ret.SetBlock(gen);
                        return ret;
                    }
                }
                await Task.Delay(1000);
            }

            return new BlockAPIResult { ResultCode = APIResultCodes.ConsensusTimeout };
        }

        public async Task<AuthorizationAPIResult> CreateDividendsAsync(string profitingAccountId)
        {
            var amounts = new Dictionary<string, decimal>
            {
                { "LYR", 1 }
            };

            var tags = new Dictionary<string, string>
            {
                { Block.REQSERVICETAG, BrokerActions.BRK_PFT_GETPFT },
                { "pftid", profitingAccountId }
            };

            var getpftResult = await SendExAsync(PoolFactoryBlock.FactoryAccount, amounts, tags);
            return getpftResult;
        }

        public async Task<BlockAPIResult> CreateStakingAccountAsync(string Name, string voteFor, int daysToStake, bool compoundMode)
        {
            var tags = new Dictionary<string, string>
            {
                { Block.REQSERVICETAG, BrokerActions.BRK_STK_CRSTK },
                { "name", Name },
                { "voting", voteFor },
                { "days", daysToStake.ToString() },
                { "compound", compoundMode.ToString() }
            };
            var amounts = new Dictionary<string, decimal>
            {
                { LyraGlobal.OFFICIALTICKERCODE, PoolFactoryBlock.StakingAccountCreateFee }
            };
            var result = await SendExAsync(PoolFactoryBlock.FactoryAccount, amounts, tags);
            if (result.ResultCode != APIResultCodes.Success)
                return new BlockAPIResult { ResultCode = result.ResultCode };

            var latx = await GetLatestBlockAsync();
            for (int i = 0; i < 60; i++)
            {
                // then find by RelatedTx
                var blocks = await _rpcClient.GetBlocksByRelatedTxAsync(latx.Hash);
                if (blocks.Successful())
                {
                    var txs = blocks.GetBlocks();
                    var gen = txs.FirstOrDefault(a => a is IBrokerAccount pb && pb.OwnerAccountId == AccountId);
                    if (gen != null)
                    {
                        var ret = new BlockAPIResult
                        {
                            ResultCode = APIResultCodes.Success,
                        };
                        ret.SetBlock(gen);
                        return ret;
                    }
                }
                await Task.Delay(1000);
            }

            return new BlockAPIResult { ResultCode = APIResultCodes.ConsensusTimeout };
        }

        public async Task<AuthorizationAPIResult> AddStakingAsync(string stakingAccountId, decimal amount)
        {
            var amountsDeposit = new Dictionary<string, decimal>
            {
                { "LYR", amount }
            };

            var tags = new Dictionary<string, string>
            {
                { Block.REQSERVICETAG, BrokerActions.BRK_STK_ADDSTK },
            };

            var addStkResult = await SendExAsync(stakingAccountId, amountsDeposit, tags);
            return addStkResult;
        }

        public async Task<IStaking> GetStakingAsync(string stakingAccountId)
        {
            var result = await _rpcClient.GetLastBlockAsync(stakingAccountId);
            if (result.Successful())
                return result.GetBlock() as IStaking;
            else
                return null;
        }

        public async Task<AuthorizationAPIResult> UnStakingAsync(string stakingAccountId)
        {
            var tags = new Dictionary<string, string>
            {
                { Block.REQSERVICETAG, BrokerActions.BRK_STK_UNSTK },
                { "stkid", stakingAccountId },
            };

            var amounts = new Dictionary<string, decimal>
            {
                { LyraGlobal.OFFICIALTICKERCODE, 1m }
            };

            var addStkResult = await SendExAsync(PoolFactoryBlock.FactoryAccount, amounts, tags);
            return addStkResult;
        }
        #endregion

        #region dex deposition & withdraw
        public async Task<AuthorizationAPIResult> CreateDexWalletAsync(string symbol, string provider)
        {
            var tags = new Dictionary<string, string>
            {
                { Block.REQSERVICETAG, BrokerActions.BRK_DEX_DPOREQ },
                { "symbol", symbol },
                { "provider", provider }
            };

            var amounts = new Dictionary<string, decimal>
            {
                { LyraGlobal.OFFICIALTICKERCODE, PoolFactoryBlock.DexWalletCreateFee }
            };

            var result = await SendExAsync(PoolFactoryBlock.FactoryAccount, amounts, tags);
            return result;
        }

        public async Task<List<IDexWallet>> GetAllDexWalletsAsync(string owner)
        {
            var ret = await RPC.GetAllDexWalletsAsync(owner);
            if (ret.Successful())
                return ret.GetBlocks().Cast<IDexWallet>().ToList();
            else
                return null;
        }

        public async Task<IDexWallet> FindDexWalletAsync(string owner, string symbol, string provider)
        {
            var ret = await RPC.FindDexWalletAsync(owner, symbol, provider);
            if (ret.Successful())
                return ret.GetBlock() as IDexWallet;
            else
                return null;
        }

        /// <summary>
        /// this method is for dex server.
        /// </summary>
        /// <param name="dexWalletId"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        public async Task<AuthorizationAPIResult> DexMintTokenAsync(string dexWalletId, decimal amount)
        {
            var tags = new Dictionary<string, string>
            {
                { Block.REQSERVICETAG, BrokerActions.BRK_DEX_MINT },
                { "dexid", dexWalletId },
                { "amount", amount.ToBalanceLong().ToString() }
            };

            var amounts = new Dictionary<string, decimal>
            {
                { LyraGlobal.OFFICIALTICKERCODE, 1 }
            };

            var result = await SendExAsync(PoolFactoryBlock.FactoryAccount, amounts, tags);
            return result;
        }

        /// <summary>
        /// this method is for user to get token to own account.
        /// </summary>
        /// <param name="dexWalletId"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        public async Task<AuthorizationAPIResult> DexGetTokenAsync(string dexWalletId, decimal amount)
        {
            var tags = new Dictionary<string, string>
            {
                { Block.REQSERVICETAG, BrokerActions.BRK_DEX_GETTKN },
                { "dexid", dexWalletId },
                { "amount", amount.ToBalanceLong().ToString() }
            };

            var amounts = new Dictionary<string, decimal>
            {
                { LyraGlobal.OFFICIALTICKERCODE, 1 }
            };

            var result = await SendExAsync(PoolFactoryBlock.FactoryAccount, amounts, tags);
            return result;
        }

        public async Task<AuthorizationAPIResult> DexPutTokenAsync(string dexWalletId, string ticker, decimal amount)
        {
            var tags = new Dictionary<string, string>
            {
                { Block.REQSERVICETAG, BrokerActions.BRK_DEX_PUTTKN },
                { "dexid", dexWalletId },
            };

            var amounts = new Dictionary<string, decimal>
            {
                { ticker, amount }
            };

            var result = await SendExAsync(dexWalletId, amounts, tags);
            return result;
        }

        public async Task<AuthorizationAPIResult> DexWithdrawTokenAsync(string dexWalletId, string extaddress, decimal amount)
        {
            var tags = new Dictionary<string, string>
            {
                { Block.REQSERVICETAG, BrokerActions.BRK_DEX_WDWREQ },
                { "dexid", dexWalletId },
                { "extaddr", extaddress },
                { "amount", amount.ToBalanceLong().ToString() }
            };

            var amounts = new Dictionary<string, decimal>
            {
                { LyraGlobal.OFFICIALTICKERCODE, 1 }
            };

            var result = await SendExAsync(PoolFactoryBlock.FactoryAccount, amounts, tags);
            return result;
        }
        #endregion

        #region DAO
        public async Task<AuthorizationAPIResult> CreateDAOAsync(string name, string description, decimal shareRito, decimal sellerFeeRatio, decimal buyerFeeRatio, int maxVoter, int sellerPar, int buyerPar)
        {
            var tags = new Dictionary<string, string>
            {
                { Block.REQSERVICETAG, BrokerActions.BRK_DAO_CRDAO },
                { "name", name },
                { "desc", description },
                { "share", shareRito.ToString(CultureInfo.InvariantCulture) },
                { "seats", $"{maxVoter}" },
                { "sellerPar", sellerPar.ToString(CultureInfo.InvariantCulture) },
                { "buyerPar", buyerPar.ToString(CultureInfo.InvariantCulture) },
                { "sellerFeeRatio", sellerFeeRatio.ToString(CultureInfo.InvariantCulture) },
                { "buyerFeeRatio", buyerFeeRatio.ToString(CultureInfo.InvariantCulture) },
            };

            var amounts = new Dictionary<string, decimal>
            {
                { LyraGlobal.OFFICIALTICKERCODE, PoolFactoryBlock.DaoCreateFee }
            };

            var result = await SendExAsync(PoolFactoryBlock.FactoryAccount, amounts, tags);
            return result;
        }
        public async Task<AuthorizationAPIResult> JoinDAOAsync(string daoid, decimal amount)
        {
            var tags = new Dictionary<string, string>
            {
                { Block.REQSERVICETAG, BrokerActions.BRK_DAO_JOIN },
                { "daoid", daoid },
            };

            var amounts = new Dictionary<string, decimal>
            {
                { LyraGlobal.OFFICIALTICKERCODE, amount }
            };

            var result = await SendExAsync(daoid, amounts, tags);
            return result;
        }
        public async Task<AuthorizationAPIResult> LeaveDAOAsync(string daoid)
        {
            var tags = new Dictionary<string, string>
            {
                { Block.REQSERVICETAG, BrokerActions.BRK_DAO_LEAVE },
                { "daoid", daoid },
            };

            var amounts = new Dictionary<string, decimal>
            {
                { LyraGlobal.OFFICIALTICKERCODE, 1m }
            };

            var result = await SendExAsync(daoid, amounts, tags);
            return result;
        }
        #endregion

        #region OTC
        public async Task<AuthorizationAPIResult> CreateOTCOrderAsync(OTCOrder order)
        {
            if (LyraGlobal.OFFICIALTICKERCODE.Equals(order.crypto, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentOutOfRangeException("Order for LYR is not supported.");

            var tags = new Dictionary<string, string>
            {
                { Block.REQSERVICETAG, BrokerActions.BRK_OTC_CRODR },
                { "data", JsonConvert.SerializeObject(order) },
            };

            var amounts = new Dictionary<string, decimal>
            {
                { LyraGlobal.OFFICIALTICKERCODE, PoolFactoryBlock.DexWalletCreateFee + order.collateral },                
            };

            if (order.dir == TradeDirection.Sell)
                amounts.Add(order.crypto, order.amount);

            var result = await SendExAsync(order.daoId, amounts, tags);
            return result;
        }

        public async Task<AuthorizationAPIResult> CreateOTCTradeAsync(OTCTrade trade)
        {
            var tags = new Dictionary<string, string>
            {
                { Block.REQSERVICETAG, BrokerActions.BRK_OTC_CRTRD },
                { "data", JsonConvert.SerializeObject(trade) },
            };

            var amounts = new Dictionary<string, decimal>
            {
                { LyraGlobal.OFFICIALTICKERCODE, PoolFactoryBlock.DexWalletCreateFee + trade.collateral },
            };

            if (trade.dir == TradeDirection.Sell)
                amounts.Add(trade.crypto, trade.amount);

            var result = await SendExAsync(trade.daoId, amounts, tags);
            return result;
        }

        public async Task<AuthorizationAPIResult> OTCTradeFiatPaymentSentAsync(string tradeid)
        {
            var tags = new Dictionary<string, string>
            {
                { Block.REQSERVICETAG, BrokerActions.BRK_OTC_TRDPAYSENT },
                { "tradeid", tradeid },
            };

            var amounts = new Dictionary<string, decimal>
            {
                { LyraGlobal.OFFICIALTICKERCODE, 1 },
            };

            var result = await SendExAsync(tradeid, amounts, tags);
            return result;
        }

        public async Task<AuthorizationAPIResult> OTCTradeFiatPaymentConfirmAsync(string tradeid)
        {
            var tags = new Dictionary<string, string>
            {
                { Block.REQSERVICETAG, BrokerActions.BRK_OTC_TRDPAYGOT },
                { "tradeid", tradeid },
            };

            var amounts = new Dictionary<string, decimal>
            {
                { LyraGlobal.OFFICIALTICKERCODE, 1 },
            };

            var result = await SendExAsync(tradeid, amounts, tags);
            return result;
        }

        public async Task<AuthorizationAPIResult> DelistOTCOrderAsync(string daoid, string orderid)
        {
            var tags = new Dictionary<string, string>
            {
                { Block.REQSERVICETAG, BrokerActions.BRK_OTC_ORDDELST },
                { "daoid", daoid },
                { "orderid", orderid },
            };

            var amounts = new Dictionary<string, decimal>
            {
                { LyraGlobal.OFFICIALTICKERCODE, 1 },
            };

            var result = await SendExAsync(daoid, amounts, tags);
            return result;
        }

        public async Task<AuthorizationAPIResult> CloseOTCOrderAsync(string daoid, string orderid)
        {
            var tags = new Dictionary<string, string>
            {
                { Block.REQSERVICETAG, BrokerActions.BRK_OTC_ORDCLOSE },
                { "daoid", daoid },
                { "orderid", orderid },
            };

            var amounts = new Dictionary<string, decimal>
            {
                { LyraGlobal.OFFICIALTICKERCODE, 1 },
            };

            var result = await SendExAsync(daoid, amounts, tags);
            return result;
        }

        public async Task<AuthorizationAPIResult> CancelOTCTradeAsync(string daoid, string orderid, string tradeid)
        {
            var tags = new Dictionary<string, string>
            {
                { Block.REQSERVICETAG, BrokerActions.BRK_OTC_TRDCANCEL },
                { "tradeid", tradeid },
                { "orderid", orderid },
                { "daoid", daoid },
            };

            var amounts = new Dictionary<string, decimal>
            {
                { LyraGlobal.OFFICIALTICKERCODE, 1 },
            };

            var result = await SendExAsync(tradeid, amounts, tags);
            return result;
        }

        public async Task<AuthorizationAPIResult> OTCTradeRaiseDisputeAsync(string tradeid)
        {
            var tags = new Dictionary<string, string>
            {
                { Block.REQSERVICETAG, BrokerActions.BRK_OTC_CRDPT },
            };

            var amounts = new Dictionary<string, decimal>
            {
                { LyraGlobal.OFFICIALTICKERCODE, 1 },
            };

            var result = await SendExAsync(tradeid, amounts, tags);
            return result;
        }
        #endregion

        #region Voting
        public async Task<AuthorizationAPIResult> CreateVoteSubject(VotingSubject subject,
            VoteProposal proposal)
        {
            var tags = new Dictionary<string, string>
            {
                { Block.REQSERVICETAG, BrokerActions.BRK_VOT_CREATE },
                { "data", JsonConvert.SerializeObject(subject) },
                { "pptype", proposal.pptype.ToString() },
                { "ppdata", proposal.data },
            };

            var amounts = new Dictionary<string, decimal>
            {
                { LyraGlobal.OFFICIALTICKERCODE, 1 },
            };

            var result = await SendExAsync(subject.DaoId, amounts, tags);
            return result;
        }
        public async Task<AuthorizationAPIResult> Vote(string voteid, int voteIndex)
        {
            var tags = new Dictionary<string, string>
            {
                { Block.REQSERVICETAG, BrokerActions.BRK_VOT_VOTE },
                { "voteid", voteid },
                { "index", voteIndex.ToString() },
            };

            var amounts = new Dictionary<string, decimal>
            {
                { LyraGlobal.OFFICIALTICKERCODE, 1 },
            };

            var result = await SendExAsync(voteid, amounts, tags);
            return result;
        }

        public async Task<APIResult> ExecuteResolution(string voteid, ODRResolution resolution)
        {
            var tags = new Dictionary<string, string>
            {
                { Block.REQSERVICETAG, BrokerActions.BRK_OTC_RSLDPT },
                { "voteid", voteid },
                { "data", JsonConvert.SerializeObject(resolution) },
            };

            var amounts = new Dictionary<string, decimal>
            {
                { LyraGlobal.OFFICIALTICKERCODE, 1 },
            };

            var result = await SendExAsync(resolution.TradeId, amounts, tags);
            return result;
        }

        public async Task<APIResult> ChangeDAO(string daoid, string voteid, DAOChange change)
        {
            var tags = new Dictionary<string, string>
            {
                { Block.REQSERVICETAG, 
                    string.IsNullOrEmpty(voteid) ? 
                    BrokerActions.BRK_DAO_CHANGE :
                    BrokerActions.BRK_DAO_VOTED_CHANGE},
                { "voteid", voteid },
                { "data", JsonConvert.SerializeObject(change) },
            };

            var amounts = new Dictionary<string, decimal>
            {
                { LyraGlobal.OFFICIALTICKERCODE, 1 },
            };

            var result = await SendExAsync(daoid, amounts, tags);
            return result;
        }
        #endregion

        #region Generic Service Request Call
        public class LyraContractABI
        {
            public string svcReq { get; set; }
            public string targetAccountId { get; set; }
            public Dictionary<string, decimal> amounts { get; set; }
            public object objArgument { get; set; }
        }
        /// <summary>
        /// generic service request
        /// call to a contract's function, like ETH ABI
        /// </summary>
        /// <param name="arg">LyraContractABI</param>
        /// <returns></returns>
        public async Task<AuthorizationAPIResult> ServiceRequestAsync(LyraContractABI arg)
        {
            var tags = new Dictionary<string, string>
            {
                { Block.REQSERVICETAG, arg.svcReq },
                { "objType", arg.objArgument.GetType().Name },
                { "data", JsonConvert.SerializeObject(arg.objArgument) },
            };

            var result = await SendExAsync(arg.targetAccountId, arg.amounts, tags);
            return result;
        }
        #endregion

        public TransactionBlock GetLastSyncBlock()
        {
            return _lastSyncBlock;
        }

        public string PrintLastBlock()
        {
            if (_lastSyncBlock == null)
                return "no blocks found";
            //return JsonConvert.SerializeObject(latestBlock);
            return _lastSyncBlock.Print();
        }

        public async Task<string> PrintBlockAsync(string blockindex)
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

            var block = await GetBlockByIndexAsync(index);
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
            var svcBlockResult = await _rpcClient.GetLastServiceBlockAsync();
            if (svcBlockResult.ResultCode != APIResultCodes.Success)
            {
                throw new Exception($"Unable to retrieve the latest service block. Result Code: {svcBlockResult.ResultCode}");
            }
            return svcBlockResult.GetBlock() as ServiceBlock;
        }

        private async Task<string> GetLastServiceBlockHashAsync()
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


