using System;

using System.Collections.Generic;
using System.Threading.Tasks;

using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;
using Lyra.Core.Blocks.Service;

using Lyra.Core.Cryptography;
using Lyra.Core.API;

using Newtonsoft.Json;
using Lyra.Core.Protos;
using System.Diagnostics;

namespace Lyra.Core.Accounts
{
    public class Wallet : BaseAccount
    {
        // to do 
        // 1) move rpcclient to CLI
        // 2) create interface and reference rpcclient by interface here, use the same interface in server and REST API client (Shopify app)  
        //private RPCClient _rpcClient = null;
        private INodeAPI _rpcClient = null;

        private int SyncHeight = -1;
        private string SyncHash = string.Empty;

        public decimal TransferFee = 0; // in atomic units
        public decimal TokenGenerationFee = 0; // in atomic units
        public decimal TradeFee = 0; // in atomic units

        //public readonly Dictionary<string, int> TokenPrecision = new Dictionary<string, int>();

        public new TransactionBlock GetLatestBlock()
        {
            //get => base.GetLatestBlock() as TransactionBlock; 
            //set => LatestBlock = value;
            return base.GetLatestBlock() as TransactionBlock;
            // block != null ? (block as TransactionBlock) : null; 
        }

        public int GetLocalAccountHeight()
        {
            var block = GetLatestBlock();
            return block != null ? block.Index : 0;
        }

        public Wallet(IAccountDatabase storage, string NetworkId) : base(null, storage, NetworkId)
        { }

        // one-time "manual" sync up with the node 
        public async Task<APIResultCodes> Sync(INodeAPI RPCClient)
        {
            if (RPCClient != null)
                _rpcClient = RPCClient;

            if (_rpcClient != null)
            {
                var result = await SyncServiceChain();
                if (result != APIResultCodes.Success)
                    return result;

                result = await SyncAccountChain();
                if (result != APIResultCodes.Success && result != APIResultCodes.AccountDoesNotExist)
                    return result;

                result = await SyncIncomingTransfers();
                return result;
            }
            else
                return APIResultCodes.NoRpcserverConnection;

        }

        public string SignAPICall()
        {
            return Signatures.GetSignature(PrivateKey, SyncHash);
        }

        public async Task<List<string>> GetTokenNames(string keyword)
        {
            if (_rpcClient == null)
                return new List<string>();

            var result = await _rpcClient.GetTokenNames(AccountId, SignAPICall(), keyword);
            if (result.ResultCode == APIResultCodes.Success)
                return result.TokenNames;
            else
                throw new Exception("Error get Token names: " + result.ResultCode.ToString());
        }

        private async Task<APIResultCodes> SyncServiceChain()
        {
            try
            {
                var result = await _rpcClient.GetSyncHeight();
                if (result.ResultCode != APIResultCodes.Success)
                    return result.ResultCode;

                if (NetworkId != result.NetworkId)
                    return APIResultCodes.InvalidNetworkId;

                SyncHeight = result.Height;
                SyncHash = result.SyncHash;

                if (TransferFee == 0 || TokenGenerationFee == 0 || TradeFee == 0)
                {
                    var blockresult = await _rpcClient.GetLastServiceBlock(AccountId, SignAPICall());

                    if (blockresult.ResultCode != APIResultCodes.Success)
                        return blockresult.ResultCode;
                    ServiceBlock lastServiceBlock = blockresult.GetBlock() as ServiceBlock;
                    TransferFee = lastServiceBlock.TransferFee;
                    TokenGenerationFee = lastServiceBlock.TokenGenerationFee;
                    TradeFee = lastServiceBlock.TradeFee;
                    Console.WriteLine($"Last Service Block Received {lastServiceBlock.Index}");
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

        private async Task<APIResultCodes> SyncAccountChain()
        {
            try
            {
                var result = await _rpcClient.GetAccountHeight(AccountId, SignAPICall());
                if (result.ResultCode != APIResultCodes.Success)
                    return result.ResultCode;

                if (NetworkId != result.NetworkId)
                    return APIResultCodes.InvalidNetworkId;

                int server_height = result.Height;
                int local_height = GetLocalAccountHeight();
                if (server_height > local_height)
                {
                    var block = await GetBlockByIndex(server_height);
                    if (block == null)
                        return APIResultCodes.BlockNotFound;
                }
                return APIResultCodes.Success;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in SyncAccountChain()r: " + e.Message);
                return APIResultCodes.UnknownError;
            }
        }


        private async Task<APIResultCodes> SyncIncomingTransfers()
        {
            try
            {
                var lookup_result = await _rpcClient.LookForNewTransfer(AccountId, SignAPICall());
                int max_counter = 0;

                while (lookup_result.Successful() && max_counter < 100) // we don't want to enter an endless loop...
                {
                    max_counter++;

                    Console.WriteLine($"Received new transaction, sending request for settlement...");

                    var receive_result = await ReceiveTransfer(lookup_result);
                    if (!receive_result.Successful())
                        return receive_result.ResultCode;

                    lookup_result = await _rpcClient.LookForNewTransfer(AccountId, SignAPICall());
                }

                // the fact that do one sent us any money does not mean this call failed...
                if (lookup_result.ResultCode == APIResultCodes.NoNewTransferFound)
                    return APIResultCodes.Success;

                return lookup_result.ResultCode;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in SyncIncomingTransfers(): " + e.Message);
                return APIResultCodes.UnknownError;
            }
        }

        public async Task<TradeAPIResult> LookForNewTrade(string BuyTokenCode, string SellTokenCode)
        {
            try
            {
                var lookup_result = await _rpcClient.LookForNewTrade(AccountId, BuyTokenCode, SellTokenCode, SignAPICall());

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
            {
                return new AuthorizationAPIResult() { ResultCode = trade_orders.ResultCode, ResultMessage = trade_orders.ResultMessage };
            }

            var sell_order = trade_orders.GetList()[0];

            var trade_order_result = await TradeOrder(TradeOrderTypes.Buy, sell_order.BuyTokenCode, sell_order.SellTokenCode, discount_amount, discount_amount, sell_order.Price, false, true);

            if (trade_order_result.ResultCode == APIResultCodes.Success)
            {
                var cancel_result = CancelTradeOrder(sell_order.Hash).Result;
                if (cancel_result.ResultCode != APIResultCodes.Success)
                {
                    return new AuthorizationAPIResult() { ResultCode = APIResultCodes.TradeOrderNotFound, ResultMessage = "No matching trade order found. Failed to cancel the redemption order." };
                }
                else
                {
                    return new AuthorizationAPIResult() { ResultCode = APIResultCodes.TradeOrderNotFound, ResultMessage = "No matching trade order found. Redemption order has been cancelled successfully" };
                }
            }
            else
            if (trade_order_result.ResultCode == APIResultCodes.TradeOrderMatchFound)
            {
                var trade = trade_order_result.GetBlock();

                var trade_result = await Trade(trade);

                return trade_result;
            }
            else
            {
                return new AuthorizationAPIResult() { ResultCode = trade_order_result.ResultCode, ResultMessage = trade_order_result.ResultMessage };
            }
        }

        public async Task<AuthorizationAPIResult> ExecuteSellOrder(TradeBlock trade, TradeOrderBlock order, NonFungibleToken nonfungible_token = null)
        {
            TransactionBlock previousBlock = GetLatestBlock();
            if (previousBlock == null)
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.PreviousBlockNotFound };

            int sell_precision = await FindTokenPrecision(trade.BuyTokenCode);
            if (sell_precision < 0)
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.TokenGenesisBlockNotFound };

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

            if (trade.BuyTokenCode == LyraGlobal.LYRA_TICKER_CODE)
                balance_change += fee;

            // see if we have enough tokens
            if (previousBlock.Balances[trade.BuyTokenCode] < balance_change)
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.InsufficientFunds };

            // see if we have enough LYR to pay the transfer fee
            if (fee > 0)
            {
                if (trade.BuyTokenCode != LyraGlobal.LYRA_TICKER_CODE)
                    if (previousBlock.Balances[LyraGlobal.LYRA_TICKER_CODE] < fee)
                        return new AuthorizationAPIResult() { ResultCode = APIResultCodes.InsufficientFunds };
            }

            var execute_block = new ExecuteTradeOrderBlock()
            {
                AccountID = AccountId,
                DestinationAccountId = trade.AccountID,
                Balances = new Dictionary<string, decimal>(),
                TradeId = trade.Hash,
                TradeOrderId = trade.TradeOrderId,
                SellTokenCode = trade.BuyTokenCode,
                SellAmount = trade.BuyAmount,
                Fee = fee,
                FeeType = fee_type,
                FeeCode = LyraGlobal.LYRA_TICKER_CODE
            };

            // The funds were previously locked by the sell order so we only put the difference between the previously locked amount and the actual trade amount
            var final_balance_change = order.TradeAmount - balance_change;

            // If the trade amount fully covers the order, there is no change in the balance as the entire Tx amount was previously "locked" by the original order 
            execute_block.Balances.Add(execute_block.SellTokenCode, previousBlock.Balances[execute_block.SellTokenCode] - final_balance_change);

            // for customer tokens, we pay fee in LYR (unless they are accepted by authorizers as a fee - TO DO)
            if (fee > 0)
            {
                if (execute_block.SellTokenCode != LyraGlobal.LYRA_TICKER_CODE)
                    execute_block.Balances.Add(LyraGlobal.LYRA_TICKER_CODE, previousBlock.Balances[LyraGlobal.LYRA_TICKER_CODE] - fee);
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

            execute_block.InitializeBlock(previousBlock, PrivateKey, NetworkId);

            // TO DO - override the trasanction validation method in ExecuteTradeBlock
            //if (!execute_block.ValidateTransaction(previousBlock))
            //    return new AuthorizationAPIResult() { ResultCode = APIResultCodes.SendTransactionValidationFailed };

            //execute_block.Signature = Signatures.GetSignature(PrivateKey, execute_block.Hash);

            var result = await _rpcClient.ExecuteTradeOrder(execute_block);

            if (result.ResultCode == APIResultCodes.Success)
            {
                execute_block.Authorizations = result.Authorizations;
                execute_block.ServiceHash = result.ServiceHash;
                AddBlock(execute_block);
            }
            else
            {
                //
            }
            return result;
        }

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
                    res += $"{balance.Value} {balance.Key}\n";
                }
                if (lastBlock.NonFungibleToken != null)
                {
                    var discount_token_genesis = await _rpcClient.GetTokenGenesisBlock(AccountId, lastBlock.NonFungibleToken.TokenCode, SignAPICall());
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

        public APIResult RestoreAccount(string path, string privateKey)
        {
            try
            {
                if (!Signatures.ValidatePrivateKey(privateKey))
                    return new APIResult() { ResultCode = APIResultCodes.InvalidPrivateKey };

                _storage.Open(path, AccountName);

                PrivateKey = privateKey;
                AccountId = Signatures.GetAccountIdFromPrivateKey(PrivateKey);

                _storage.StorePrivateKey(PrivateKey);
                _storage.StoreAccountId(AccountId);
                return new APIResult() { ResultCode = APIResultCodes.Success };
            }
            catch (Exception e)
            {
                return new APIResult() { ResultCode = APIResultCodes.UnknownError, ResultMessage = e.Message };
            }
        }

        public int NumberOfNonZeroBalances
        {
            get
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
        }

        public async Task<TransactionBlock> GetBlockByIndex(int Index)
        {
            var block = _storage.FindBlockByIndex(Index) as TransactionBlock;
            if (block == null)
            {
                var result = await _rpcClient.GetBlockByIndex(AccountId, Index, SignAPICall());
                if (result.ResultCode == APIResultCodes.Success)
                {
                    block = result.GetBlock() as TransactionBlock;
                    AddBlock(block);
                }
            }
            return block;
        }

        public async Task<TransactionBlock> GetBlockByHash(string Hash)
        {
            var block = _storage.FindBlockByHash(Hash) as TransactionBlock;
            if (block == null)
            {
                var result = await _rpcClient.GetBlockByHash(AccountId, Hash, SignAPICall());
                if (result.ResultCode == APIResultCodes.Success)
                {
                    block = result.GetBlock() as TransactionBlock;
                    AddBlock(block);
                }
            }
            return block;
        }

        //public async Task<AuthorizationAPIResult> ImportAccount(string ImportedAccountKey)
        //{
        //    TransactionBlock previousBlock = GetLatestBlock();
        //    if (previousBlock == null)
        //    {
        //        var import_block = new OpenAccountWithImportBlock
        //        {
        //            AccountID = AccountId,
        //            Balances = new Dictionary<string, decimal>(),
        //            //PaymentID = string.Empty,
        //            Fee = TransferFee,
        //            FeeCode = LyraGlobal.LYRA_TICKER_CODE,
        //            FeeType = AuthorizationFeeTypes.Regular
        //        }
        //        else
        //        {

        //        }
        //    }
        //    else
        //    {

        //    }


        //}

        public async Task<AuthorizationAPIResult> Send(decimal Amount, string DestinationAccountId, string ticker = LyraGlobal.LYRA_TICKER_CODE, bool ToExchange = false)
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

            if (ticker == LyraGlobal.LYRA_TICKER_CODE)
                balance_change += fee;

            // see if we have enough tokens
            if (previousBlock.Balances[ticker] < balance_change)
            {
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.InsufficientFunds };
                //throw new ApplicationException("Insufficient funds");
            }

            // see if we have enough LYR to pay the transfer fee
            if (ticker != LyraGlobal.LYRA_TICKER_CODE)
                if (!previousBlock.Balances.ContainsKey(LyraGlobal.LYRA_TICKER_CODE) || previousBlock.Balances[LyraGlobal.LYRA_TICKER_CODE] < fee)
                {
                    //throw new ApplicationException("Insufficient funds to pay transfer fee");
                    return new AuthorizationAPIResult() { ResultCode = APIResultCodes.InsufficientFunds };
                }

            SendTransferBlock sendBlock;
            if (ToExchange)
            {
                sendBlock = new ExchangingBlock()
                {
                    AccountID = AccountId,
                    ServiceHash = string.Empty,
                    DestinationAccountId = DestinationAccountId,
                    Balances = new Dictionary<string, decimal>(),
                    //PaymentID = string.Empty,
                    Fee = fee,
                    FeeCode = LyraGlobal.LYRA_TICKER_CODE,
                    FeeType = AuthorizationFeeTypes.Regular
                };
            }
            else
            {
                sendBlock = new SendTransferBlock()
                {
                    AccountID = AccountId,
                    ServiceHash = string.Empty,
                    DestinationAccountId = DestinationAccountId,
                    Balances = new Dictionary<string, decimal>(),
                    //PaymentID = string.Empty,
                    Fee = fee,
                    FeeCode = LyraGlobal.LYRA_TICKER_CODE,
                    FeeType = AuthorizationFeeTypes.Regular
                };
            };

            sendBlock.Balances.Add(ticker, previousBlock.Balances[ticker] - balance_change);
            //sendBlock.Transaction = transaction;

            // for customer tokens, we pay fee in LYR (unless they are accepted by authorizers as a fee - TO DO)
            if (ticker != LyraGlobal.LYRA_TICKER_CODE)
                sendBlock.Balances.Add(LyraGlobal.LYRA_TICKER_CODE, previousBlock.Balances[LyraGlobal.LYRA_TICKER_CODE] - fee);

            // transfer unchanged token balances from the previous block
            foreach (var balance in previousBlock.Balances)
                if (!(sendBlock.Balances.ContainsKey(balance.Key)))
                    sendBlock.Balances.Add(balance.Key, balance.Value);

            sendBlock.InitializeBlock(previousBlock, PrivateKey, NetworkId);

            if (!sendBlock.ValidateTransaction(previousBlock))
            {
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.SendTransactionValidationFailed };
                //throw new ApplicationException("ValidateTransaction failed");
            }

            //sendBlock.Signature = Signatures.GetSignature(PrivateKey, sendBlock.Hash);
            AuthorizationAPIResult result;
            if(ToExchange)
                result = await _rpcClient.SendExchangeTransfer((ExchangingBlock)sendBlock);
            else
                result = await _rpcClient.SendTransfer(sendBlock);

            if (result.ResultCode == APIResultCodes.Success)
            {
                sendBlock.Authorizations = result.Authorizations;
                sendBlock.ServiceHash = result.ServiceHash;
                AddBlock(sendBlock);
            }
            else
            {
                Console.WriteLine($"BlockSignatureValidationFailed");
                Console.WriteLine("Error Message: ");
                Console.WriteLine(result.ResultMessage);
                Console.WriteLine("Local Block: ");
                Console.WriteLine(sendBlock.Print());
            }
            return result;
        }

        public async Task<TradeOrderAuthorizationAPIResult> TradeOrder(
            TradeOrderTypes orderType, string SellToken, string BuyToken, decimal MaxAmount, decimal MinAmount, decimal Price, bool CoverAnotherTradersFee, bool AnotherTraderWillCoverFee)
        {
            TransactionBlock previousBlock = GetLatestBlock();
            if (previousBlock == null)
                return new TradeOrderAuthorizationAPIResult() { ResultCode = APIResultCodes.PreviousBlockNotFound };

            int sell_token_precision = await FindTokenPrecision(SellToken);
            if (sell_token_precision < 0)
                return new TradeOrderAuthorizationAPIResult() { ResultCode = APIResultCodes.TokenGenesisBlockNotFound };

            int buy_token_precision = await FindTokenPrecision(BuyToken);
            if (buy_token_precision < 0)
                return new TradeOrderAuthorizationAPIResult() { ResultCode = APIResultCodes.TokenGenesisBlockNotFound };

            //var atomic_amount; // that's the amount we "lock" (send to no one) no matter it's buy or sell order


            // For sell order: how many buy tokens are needed to buy one sell token
            // For buy order: how many sell tokens are needed to buy one buy token
            //long atomic_price; // that's the price (selling or buying depending on order); the price of the order and the trade should match

            //long max_atomic_amount; // that's the max buy for buy order and max sell for sell order
            //long min_atomic_amount; // that's the minimum amount of the trade (we don;t want to pay fees for a thousand of trades with frusctions
            decimal balance_change;

            if (orderType == TradeOrderTypes.Sell)
            {
                // sell order locks (sends to nowhere) the MaxAmount or sell tokens
                //atomic_amount = (long)(MaxAmount * (decimal)Math.Pow(10, sell_token_precision));
                //max_atomic_amount = atomic_amount;

                //min_atomic_amount = (long)(MinAmount * (decimal)Math.Pow(10, sell_token_precision));

                // For sell order: how many buy tokens are needed to buy one sell token
                //atomic_price = (long)(Price * (decimal)Math.Pow(10, buy_token_precision));
                balance_change = MaxAmount;
            }
            else
            {
                // For buy order: how many sell tokens are needed to buy one buy token
                //atomic_price = (long)(Price * (decimal)Math.Pow(10, sell_token_precision));

                // buy order locks (sends to nowhere) the MaxAmount of Sell tokens multiplied by Price
                //atomic_amount = (long)(MaxAmount * Price * (decimal)Math.Pow(10, sell_token_precision));

                //max_atomic_amount = (long)(MaxAmount * (decimal)Math.Pow(10, buy_token_precision));

                //min_atomic_amount = (long)(MinAmount * (decimal)Math.Pow(10, buy_token_precision));
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

            if (SellToken == LyraGlobal.LYRA_TICKER_CODE)
                balance_change += trading_fee;

            // see if we have enough LYR to pay the transfer fee
            if (trading_fee > 0 && SellToken != LyraGlobal.LYRA_TICKER_CODE)
            {
                if (!previousBlock.Balances.ContainsKey(LyraGlobal.LYRA_TICKER_CODE))
                    return new TradeOrderAuthorizationAPIResult() { ResultCode = APIResultCodes.InsufficientFunds };

                if (previousBlock.Balances[LyraGlobal.LYRA_TICKER_CODE] < trading_fee)
                    return new TradeOrderAuthorizationAPIResult() { ResultCode = APIResultCodes.InsufficientFunds };
            }
            // see if we have enough tokens to sell.
            // For both sell and buy orders we send sell tokens, as different order types sell "opposite" tokens
            if (previousBlock.Balances[SellToken] < balance_change)
                return new TradeOrderAuthorizationAPIResult() { ResultCode = APIResultCodes.InsufficientFunds };

            var tradeBlock = new TradeOrderBlock
            {
                AccountID = AccountId,
                ServiceHash = string.Empty,
                DestinationAccountId = string.Empty, // we are sending to nowhere
                Balances = new Dictionary<string, decimal>(),
                //PaymentID = string.Empty,
                Fee = 0, // We don't pay fees for placing orders
                FeeCode = LyraGlobal.LYRA_TICKER_CODE,
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

            tradeBlock.Balances.Add(SellToken, previousBlock.Balances[SellToken] - balance_change);
            //sendBlock.Transaction = transaction;

            // We have to count for the fee here to make sure we lock enough funds to pay fee later in ExecuteTradeOrder or Trade Block.
            // for customer tokens, we pay fee in LYR (unless they are accepted by authorizers as a fee - TO DO)
            if (trading_fee > 0 && SellToken != LyraGlobal.LYRA_TICKER_CODE)
                tradeBlock.Balances.Add(LyraGlobal.LYRA_TICKER_CODE, previousBlock.Balances[LyraGlobal.LYRA_TICKER_CODE] - trading_fee);

            // transfer unchanged token balances from the previous block
            foreach (var balance in previousBlock.Balances)
                if (!(tradeBlock.Balances.ContainsKey(balance.Key)))
                    tradeBlock.Balances.Add(balance.Key, balance.Value);

            tradeBlock.InitializeBlock(previousBlock, PrivateKey, NetworkId);

            if (!tradeBlock.ValidateTransaction(previousBlock))
                return new TradeOrderAuthorizationAPIResult() { ResultCode = APIResultCodes.TradeOrderValidationFailed };

            //tradeBlock.Signature = Signatures.GetSignature(PrivateKey, tradeBlock.Hash);

            var result = await _rpcClient.TradeOrder(tradeBlock);

            if (result.ResultCode == APIResultCodes.Success)
            {
                tradeBlock.Authorizations = result.Authorizations;
                tradeBlock.ServiceHash = result.ServiceHash;
                AddBlock(tradeBlock);
            }

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

            if (trade.SellTokenCode == LyraGlobal.LYRA_TICKER_CODE)
                balance_change = balance_change + trade.Fee;

            trade.Balances.Add(trade.SellTokenCode, previousBlock.Balances[trade.SellTokenCode] - balance_change);

            // transfer unchanged token balances from the previous block
            foreach (var balance in previousBlock.Balances)
                if (!(trade.Balances.ContainsKey(balance.Key)))
                    trade.Balances.Add(balance.Key, balance.Value);

            trade.InitializeBlock(previousBlock, PrivateKey, NetworkId);

            var trade_result = await _rpcClient.Trade(trade);

            if (trade_result.ResultCode == APIResultCodes.Success)
            {
                trade.Authorizations = trade_result.Authorizations;
                trade.ServiceHash = trade_result.ServiceHash;
                AddBlock(trade);
            }
            else
            if (trade_result.ResultCode == APIResultCodes.BlockSignatureValidationFailed)
            {
                Console.WriteLine($"BlockSignatureValidationFailed");
                Console.WriteLine("Error Message: ");
                Console.WriteLine(trade_result.ResultMessage);
                Console.WriteLine("Local Block: ");
                Console.WriteLine(trade.Print());
            }
            else
            { }

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
                ServiceHash = string.Empty,
                Balances = new Dictionary<string, decimal>(),
                FeeType = AuthorizationFeeTypes.NoFee,
                TradeOrderId = OrderId
            };

            var previous_to_order_block = await GetBlockByHash(order.PreviousHash);
            if (previous_to_order_block == null)
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.PreviousBlockNotFound };

            var order_transaction = order.GetTransaction(previous_to_order_block);

            var previousBlock = GetLatestBlock();

            cancelBlock.Balances.Add(order.SellTokenCode, previousBlock.Balances[order.SellTokenCode] + order_transaction.TotalBalanceChange);

            // transfer unchanged token balances from the previous block
            foreach (var balance in previousBlock.Balances)
                if (!(cancelBlock.Balances.ContainsKey(balance.Key)))
                    cancelBlock.Balances.Add(balance.Key, balance.Value);

            cancelBlock.InitializeBlock(previousBlock, PrivateKey, NetworkId);

            var result = await _rpcClient.CancelTradeOrder(cancelBlock);

            if (result.ResultCode == APIResultCodes.Success)
            {
                cancelBlock.Authorizations = result.Authorizations;
                cancelBlock.ServiceHash = result.ServiceHash;
                AddBlock(cancelBlock);
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
            return await _rpcClient.GetActiveTradeOrders(AccountId, SellToken, BuyToken, OrderType, SignAPICall());
        }

        public string PrintActiveTradeOrders()
        {
            var orders_result = _rpcClient.GetActiveTradeOrders(AccountId, null, null, TradeOrderListTypes.All, SignAPICall()).Result;
            if (orders_result.ResultCode != APIResultCodes.Success)
            {
                return "No active trade orders found";
            }

            var orders = orders_result.GetList();

            string result = "Sell Orders:\n";
            foreach (var order in orders)
                if (order.OrderType == TradeOrderTypes.Sell)
                    result += $"Sell: {order.TradeAmount} {order.SellTokenCode} Price: {order.Price} {order.BuyTokenCode}\n";

            Console.WriteLine("Buy Orders:");
            foreach (var order in orders)
                if (order.OrderType == TradeOrderTypes.Buy)
                    result += $"Buy: {order.TradeAmount} {order.BuyTokenCode} Price: {order.Price} {order.SellTokenCode}\n";

            return result;
        }


        private async Task<int> FindTokenPrecision(string token)
        {
            int precision = -1;

            // see if we have this already in local storage
            var genesisBlock = _storage.GetTokenInfo(token);
            if (genesisBlock == null)
            {
                var result = await _rpcClient.GetTokenGenesisBlock(AccountId, token, SignAPICall());
                if (result.ResultCode == APIResultCodes.Success)
                {
                    genesisBlock = result.GetBlock() as TokenGenesisBlock;
                    SaveTokenInfoBlock(genesisBlock);

                    //Console.WriteLine($"Found Token Genesis Block for {genesisBlock.Ticker}");
                    //Console.WriteLine("Balance: " + GetDisplayBalances());
                    //Console.Write(string.Format("{0}> ", AccountName));
                }
            }

            if (genesisBlock != null)
                precision = (int)genesisBlock.Precision;

            return precision;
        }

        private void SaveTokenInfoBlock(TokenGenesisBlock block)
        {
            _storage.SaveTokenInfo(block);
        }

        private async Task<AuthorizationAPIResult> OpenStandardAccountWithReceiveBlock(NewTransferAPIResult new_transfer_info)
        {
            var openReceiveBlock = new OpenWithReceiveTransferBlock
            {
                AccountType = AccountTypes.Standard,
                AccountID = AccountId,
                ServiceHash = string.Empty,
                SourceHash = new_transfer_info.SourceHash,
                Balances = new Dictionary<string, decimal>(),
                Fee = 0,
                FeeType = AuthorizationFeeTypes.NoFee,
                NonFungibleToken = new_transfer_info.NonFungibleToken
            };

            openReceiveBlock.Balances.Add(new_transfer_info.Transfer.TokenCode, new_transfer_info.Transfer.Amount);
            openReceiveBlock.InitializeBlock(null, PrivateKey, NetworkId);

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
                Console.WriteLine($"Receive transfer block has been authorized successfully");
                // request the authorized block and save it locally 
                openReceiveBlock.Authorizations = result.Authorizations;
                openReceiveBlock.ServiceHash = result.ServiceHash;
                AddBlock(openReceiveBlock);
                Console.WriteLine("Balance: " + await GetDisplayBalancesAsync());
            }
            Console.Write(string.Format("{0}> ", AccountName));
            return result;
        }

        private async Task<AuthorizationAPIResult> ReceiveTransfer(NewTransferAPIResult new_transfer_info)
        {

            await FindTokenPrecision(new_transfer_info.Transfer.TokenCode);

            if (GetLocalAccountHeight() == 0) // if this is new account with no blocks
                return await OpenStandardAccountWithReceiveBlock(new_transfer_info);

            var receiveBlock = new ReceiveTransferBlock
            {
                AccountID = AccountId,
                ServiceHash = string.Empty,
                SourceHash = new_transfer_info.SourceHash,
                Balances = new Dictionary<string, decimal>(),
                Fee = 0,
                FeeType = AuthorizationFeeTypes.NoFee,
                NonFungibleToken = new_transfer_info.NonFungibleToken
            };

            TransactionBlock latestBlock = GetLatestBlock();

            var newBalance = new_transfer_info.Transfer.Amount;
            // if the recipient's account has this token already, add the transaction amount to the existing balance
            if (latestBlock.Balances.ContainsKey(new_transfer_info.Transfer.TokenCode))
                newBalance += latestBlock.Balances[new_transfer_info.Transfer.TokenCode];

            receiveBlock.Balances.Add(new_transfer_info.Transfer.TokenCode, newBalance);

            // transfer unchanged token balances from the previous block
            foreach (var balance in latestBlock.Balances)
                if (!(receiveBlock.Balances.ContainsKey(balance.Key)))
                    receiveBlock.Balances.Add(balance.Key, balance.Value);

            receiveBlock.InitializeBlock(latestBlock, PrivateKey, NetworkId);

            if (!receiveBlock.ValidateTransaction(latestBlock))
                throw new ApplicationException("ValidateTransaction failed");

            //receiveBlock.Signature = Signatures.GetSignature(PrivateKey, receiveBlock.Hash);

            var result = await _rpcClient.ReceiveTransfer(receiveBlock);

            if (result.ResultCode == APIResultCodes.BlockSignatureValidationFailed)
            {
                Console.WriteLine($"BlockSignatureValidationFailed");
                Console.WriteLine("Error Message: ");
                Console.WriteLine(result.ResultMessage);
                Console.WriteLine("Local Block: ");
                Console.WriteLine(receiveBlock.Print());
            }
            else
            if (result.ResultCode != APIResultCodes.Success)
            {
                Console.WriteLine($"Failed to authorize receive transfer block with error code: {result.ResultCode}");
                Console.WriteLine("Error Message: " + result.ResultMessage);

            }
            else
            {
                Console.WriteLine($"Receive transfer block has been authorized successfully");
                //// request the authorized block and save it locally 
                //GetBlock(receiveBlock.Index);
                receiveBlock.Authorizations = result.Authorizations;
                receiveBlock.ServiceHash = result.ServiceHash;
                AddBlock(receiveBlock);
                Console.WriteLine("Balance: " + await GetDisplayBalancesAsync());
            }
            Console.Write(string.Format("{0}> ", AccountName));
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
            // initiate test coins
            var openTokenGenesisBlock = new LyraTokenGenesisBlock
            {
                AccountType = AccountTypes.Standard,
                Ticker = LyraGlobal.LYRA_TICKER_CODE,
                DomainName = "Lyra",
                ContractType = ContractTypes.Cryptocurrency,
                Description = "Lyra Permissioned Gas Token",
                Precision = LyraGlobal.LYRA_PRECISION,
                //Balances.Add // =  //10000000000, 
                IsFinalSupply = true,
                //CustomFee = 0,
                //CustomFeeAccountId = string.Empty,
                AccountID = AccountId,
                Balances = new Dictionary<string, decimal>(),
                ServiceHash = null,
                Fee = 0,
                FeeType = AuthorizationFeeTypes.NoFee,
                Icon = "https://i.imgur.com/L3h0J1K.png",
                Image = "https://i.imgur.com/B8l4ZG5.png",
                RenewalDate = DateTime.MaxValue,
            };
            // TO DO - set service hash
            var transaction = new TransactionInfo() { TokenCode = openTokenGenesisBlock.Ticker, Amount = 1800000000 };
            //var transaction = new TransactionInfo() { TokenCode = openTokenGenesisBlock.Ticker, Amount = 150000000 };

            openTokenGenesisBlock.Balances.Add(transaction.TokenCode, transaction.Amount); // This is current supply in atomic units (1,000,000.00)
            //openTokenGenesisBlock.Transaction = transaction;
            openTokenGenesisBlock.InitializeBlock(null, PrivateKey, NetworkId);

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
                Console.WriteLine($"Genesis block has been authorized successfully");
                // request the authorized block and save it locally 
                //GetBlock(1);
                openTokenGenesisBlock.Authorizations = result.Authorizations;
                openTokenGenesisBlock.ServiceHash = result.ServiceHash;
                AddBlock(openTokenGenesisBlock);
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

            string ticker = domainName + "." + tokenName;

            TransactionBlock latestBlock = GetLatestBlock();
            if (latestBlock == null || latestBlock.Balances[LyraGlobal.LYRA_TICKER_CODE] < TokenGenerationFee)
            {
                //throw new ApplicationException("Insufficent funds");
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.InsufficientFunds };
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
                Balances = new Dictionary<string, decimal>(),
                ServiceHash = string.Empty,
                Fee = TokenGenerationFee,
                FeeType = AuthorizationFeeTypes.Regular,
                Owner = owner,
                Address = address,
                Currency = currency,
                Tags = tags,
                RenewalDate = DateTime.Now.Add(TimeSpan.FromDays(365)),
                ContractType = contractType,
            };
            // TO DO - set service hash

            //var transaction = new TransactionInfo() { TokenCode = ticker, Amount = supply * (long)Math.Pow(10, precision) };
            var transaction = new TransactionInfo() { TokenCode = ticker, Amount = supply };

            tokenBlock.Balances.Add(transaction.TokenCode, transaction.Amount); // This is current supply in atomic units (1,000,000.00)
            tokenBlock.Balances.Add(LyraGlobal.LYRA_TICKER_CODE, latestBlock.Balances[LyraGlobal.LYRA_TICKER_CODE] - TokenGenerationFee);
            //tokenBlock.Transaction = transaction;
            // transfer unchanged token balances from the previous block
            foreach (var balance in latestBlock.Balances)
                if (!(tokenBlock.Balances.ContainsKey(balance.Key)))
                    tokenBlock.Balances.Add(balance.Key, balance.Value);

            tokenBlock.InitializeBlock(latestBlock, PrivateKey, NetworkId);

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
                tokenBlock.Authorizations = result.Authorizations;
                tokenBlock.ServiceHash = result.ServiceHash;
                AddBlock(tokenBlock);
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
            return Signatures.ValidatePrivateKey(private_key);
        }

        public override void Dispose()
        {
            base.Dispose();
            //if (_rpcClient != null)
            //_rpcClient.Dispose();
        }
    }

}
