using Lyra.Core.API;
using Lyra.Core.Blocks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lyra.Data.Utils;

namespace Noded.Services
{
    public class KeylessWallet
    {
        private string _accountId;
        private SignHandler _signer;
        private INodeAPI _rpcClient;
        INodeTransactionAPI _trans;

        public string AccountId => _accountId;

        public KeylessWallet(string accountId, SignHandler signer, INodeAPI client, INodeTransactionAPI trans)
        {
            _accountId = accountId;
            _signer = signer;

            _rpcClient = client;
            _trans = trans;
        }

        public async Task<Dictionary<string, long>> GetBalanceAsync()
        {
            var lastTx = await GetLatestBlockAsync();
            if (lastTx == null)
                return null;
            return lastTx.Balances;
        }

        public async Task<APIResultCodes> ReceiveAsync()
        {
            var blockresult = await _rpcClient.GetLastServiceBlock();

            if (blockresult.ResultCode != APIResultCodes.Success)
                return blockresult.ResultCode;

            ServiceBlock lastServiceBlock = blockresult.GetBlock() as ServiceBlock;
            //TransferFee = lastServiceBlock.TransferFee;
            //TokenGenerationFee = lastServiceBlock.TokenGenerationFee;
            //TradeFee = lastServiceBlock.TradeFee;

            try
            {
                var lookup_result = await _rpcClient.LookForNewTransfer2(AccountId, null);
                int max_counter = 0;

                while (lookup_result.Successful() && max_counter < 100) // we don't want to enter an endless loop...
                {
                    max_counter++;

                    //PrintConLine($"Received new transaction, sending request for settlement...");

                    var receive_result = await ReceiveTransfer(lookup_result);
                    if (!receive_result.Successful())
                        return receive_result.ResultCode;

                    lookup_result = await _rpcClient.LookForNewTransfer2(AccountId, null);
                }

                // the fact that do one sent us any money does not mean this call failed...
                if (lookup_result.ResultCode == APIResultCodes.NoNewTransferFound)
                    return APIResultCodes.Success;

                if (lookup_result.ResultCode == APIResultCodes.AccountAlreadyImported)
                {
                    //PrintConLine($"This account was imported (merged) to another account.");
                    //AccountAlreadyImported = true;
                    return lookup_result.ResultCode;
                }

                return lookup_result.ResultCode;
            }
            catch (Exception e)
            {
                //PrintConLine("Exception in SyncIncomingTransfers(): " + e.Message);
                return APIResultCodes.UnknownError;
            }
        }

        public async Task<APIResultCodes> SendAsync(decimal Amount, string destAccount, string ticker = LyraGlobal.OFFICIALTICKERCODE)
        {
            if (Amount <= 0)
                throw new Exception("Amount must > 0");

            TransactionBlock previousBlock = await GetLatestBlockAsync();
            if (previousBlock == null)
            {
                throw new Exception("No balance");
            }

            var blockresult = await _rpcClient.GetLastServiceBlock();

            if (blockresult.ResultCode != APIResultCodes.Success)
                return blockresult.ResultCode;

            ServiceBlock lastServiceBlock = blockresult.GetBlock() as ServiceBlock;

            //int precision = await FindTokenPrecision(ticker);
            //if (precision < 0)
            //{

            //    return new AuthorizationAPIResult() { ResultCode = APIResultCodes.TokenGenesisBlockNotFound };
            //}

            //long atomicamount = (long)(Amount * (decimal)Math.Pow(10, precision));
            var balance_change = Amount;

            //var transaction = new TransactionInfo() { TokenCode = ticker, Amount = atomicamount };

            var fee = lastServiceBlock.TransferFee;

            if (ticker == LyraGlobal.OFFICIALTICKERCODE)
                balance_change += fee;

            // see if we have enough tokens
            if (previousBlock.Balances[ticker] < balance_change.ToBalanceLong())
            {
                return APIResultCodes.InsufficientFunds;
                //throw new ApplicationException("Insufficient funds");
            }

            // see if we have enough LYR to pay the transfer fee
            if (ticker != LyraGlobal.OFFICIALTICKERCODE)
                if (!previousBlock.Balances.ContainsKey(LyraGlobal.OFFICIALTICKERCODE) || previousBlock.Balances[LyraGlobal.OFFICIALTICKERCODE] < fee.ToBalanceLong())
                {
                    //throw new ApplicationException("Insufficient funds to pay transfer fee");
                    return APIResultCodes.InsufficientFunds;
                }

            //var svcBlockResult = await _rpcClient.GetLastServiceBlock(AccountId, SignAPICallAsync());
            //if (svcBlockResult.ResultCode != APIResultCodes.Success)
            //{
            //    throw new Exception("Unable to get latest service block.");
            //}

            SendTransferBlock sendBlock;
            sendBlock = new SendTransferBlock()
            {
                AccountID = _accountId,
                VoteFor = null,
                ServiceHash = lastServiceBlock.Hash,
                DestinationAccountId = destAccount,
                Balances = new Dictionary<string, long>(),
                //PaymentID = string.Empty,
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

            sendBlock.InitializeBlock(previousBlock, _signer);

            if (!sendBlock.ValidateTransaction(previousBlock))
            {
                return APIResultCodes.SendTransactionValidationFailed;
                //throw new ApplicationException("ValidateTransaction failed");
            }

            //sendBlock.Signature = Signatures.GetSignature(PrivateKey, sendBlock.Hash);
            AuthorizationAPIResult result;
            //var stopwatch = Stopwatch.StartNew();
            result = await _trans.SendTransfer(sendBlock);
            //stopwatch.Stop();
            //PrintConLine($"_rpcClient.SendTransfer: {stopwatch.ElapsedMilliseconds} ms.");

            return result.ResultCode;
        }

        private async Task<AuthorizationAPIResult> ReceiveTransfer(NewTransferAPIResult2 new_transfer_info)
        {

            // *** Slava - July 19, 2020 - I am not sure if we need this call anymore? 
            //await FindTokenPrecision(new_transfer_info.Transfer.TokenCode);
            // ***

            if (await GetLocalAccountHeightAsync() == 0) // if this is new account with no blocks
                return await OpenStandardAccountWithReceiveBlock(new_transfer_info);

            var svcBlockResult = await _rpcClient.GetLastServiceBlock();
            if (svcBlockResult.ResultCode != APIResultCodes.Success)
            {
                throw new Exception("Unable to get latest service block.");
            }

            var receiveBlock = new ReceiveTransferBlock
            {
                AccountID = _accountId,
                VoteFor = null,
                ServiceHash = svcBlockResult.GetBlock().Hash,
                SourceHash = new_transfer_info.SourceHash,
                Balances = new Dictionary<string, long>(),
                Fee = 0,
                FeeType = AuthorizationFeeTypes.NoFee,
                NonFungibleToken = new_transfer_info.NonFungibleToken
            };

            TransactionBlock latestBlock = await GetLatestBlockAsync();

            var latestBalances = latestBlock.Balances.ToDecimalDict();
            var recvBalances = latestBlock.Balances.ToDecimalDict();
            foreach (var chg in new_transfer_info.Transfer.Changes)
            {
                if (recvBalances.ContainsKey(chg.Key))
                    recvBalances[chg.Key] += chg.Value;
                else
                    recvBalances.Add(chg.Key, chg.Value);
            }

            receiveBlock.Balances = recvBalances.ToLongDict();

            receiveBlock.InitializeBlock(latestBlock, _signer);

            if (!receiveBlock.ValidateTransaction(latestBlock))
                throw new ApplicationException("ValidateTransaction failed");

            //receiveBlock.Signature = Signatures.GetSignature(PrivateKey, receiveBlock.Hash);

            var result = await _trans.ReceiveTransfer(receiveBlock);

            return result;
        }

        private async Task<AuthorizationAPIResult> OpenStandardAccountWithReceiveBlock(NewTransferAPIResult2 new_transfer_info)
        {
            var svcBlockResult = await _rpcClient.GetLastServiceBlock();
            if (svcBlockResult.ResultCode != APIResultCodes.Success)
            {
                throw new Exception("Unable to get latest service block.");
            }

            var openReceiveBlock = new OpenWithReceiveTransferBlock
            {
                AccountType = AccountTypes.Standard,
                AccountID = _accountId,
                ServiceHash = svcBlockResult.GetBlock().Hash,
                SourceHash = new_transfer_info.SourceHash,
                Balances = new Dictionary<string, long>(),
                Fee = 0,
                FeeType = AuthorizationFeeTypes.NoFee,
                NonFungibleToken = new_transfer_info.NonFungibleToken,
                VoteFor = null
            };

            foreach (var chg in new_transfer_info.Transfer.Changes)
            {
                openReceiveBlock.Balances.Add(chg.Key, chg.Value.ToBalanceLong());
            }
            openReceiveBlock.InitializeBlock(null, _signer);

            //openReceiveBlock.Signature = Signatures.GetSignature(PrivateKey, openReceiveBlock.Hash);

            var result = await _trans.ReceiveTransferAndOpenAccount(openReceiveBlock);

            //PrintCon(string.Format("{0}> ", AccountName));
            return result;
        }

        public async Task<AuthorizationAPIResult> SendEx(string DestinationAccountId, Dictionary<string, decimal> Amounts, Dictionary<string, string> tags)
        {
            if (Amounts.Any(a => a.Value <= 0m))
                throw new Exception("Amount must > 0");

            TransactionBlock previousBlock = await GetLatestBlockAsync();
            if (previousBlock == null)
            {
                //throw new ApplicationException("Previous block not found");
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.PreviousBlockNotFound };
            }

            // check tokens exists
            if (Amounts.Keys.Any(a => !previousBlock.Balances.ContainsKey(a)))
            {
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.TokenGenesisBlockNotFound };
            }

            var blockresult = await _rpcClient.GetLastServiceBlock();

            if (blockresult.ResultCode != APIResultCodes.Success)
                return new AuthorizationAPIResult() { ResultCode = blockresult.ResultCode };

            ServiceBlock lastServiceBlock = blockresult.GetBlock() as ServiceBlock;

            var fee = lastServiceBlock.TransferFee;

            SendTransferBlock sendBlock = new SendTransferBlock()
            {
                AccountID = AccountId,
                VoteFor = null,
                ServiceHash = lastServiceBlock.Hash,
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

            sendBlock.InitializeBlock(previousBlock, _signer);

            if (!sendBlock.ValidateTransaction(previousBlock))
            {
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.SendTransactionValidationFailed };
                //throw new ApplicationException("ValidateTransaction failed");
            }

            //sendBlock.Signature = Signatures.GetSignature(PrivateKey, sendBlock.Hash);
            AuthorizationAPIResult result;
            //var stopwatch = Stopwatch.StartNew();
            result = await _trans.SendTransfer(sendBlock);

            return result;
        }

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

            var blockresult = await _rpcClient.GetLastServiceBlock();

            if (blockresult.ResultCode != APIResultCodes.Success)
                return new AuthorizationAPIResult() { ResultCode = blockresult.ResultCode };

            ServiceBlock lastServiceBlock = blockresult.GetBlock() as ServiceBlock;

            TransactionBlock latestBlock = await GetLatestBlockAsync();
            if (latestBlock == null || latestBlock.Balances[LyraGlobal.OFFICIALTICKERCODE] < lastServiceBlock.TokenGenerationFee.ToBalanceLong())
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
                Fee = lastServiceBlock.TokenGenerationFee,
                FeeType = AuthorizationFeeTypes.Regular,
                Owner = owner,
                Address = address,
                Currency = currency,
                Tags = tags,
                RenewalDate = DateTime.UtcNow.Add(TimeSpan.FromDays(3650)),
                ContractType = contractType,
                VoteFor = null
            };
            // TO DO - set service hash

            //var transaction = new TransactionInfo() { TokenCode = ticker, Amount = supply * (long)Math.Pow(10, precision) };
            var transaction = new TransactionInfo() { TokenCode = ticker, Amount = supply };

            tokenBlock.Balances.Add(transaction.TokenCode, transaction.Amount.ToBalanceLong()); // This is current supply in atomic units (1,000,000.00)
            tokenBlock.Balances.Add(LyraGlobal.OFFICIALTICKERCODE, latestBlock.Balances[LyraGlobal.OFFICIALTICKERCODE] - lastServiceBlock.TokenGenerationFee.ToBalanceLong());
            //tokenBlock.Transaction = transaction;
            // transfer unchanged token balances from the previous block
            foreach (var balance in latestBlock.Balances)
                if (!(tokenBlock.Balances.ContainsKey(balance.Key)))
                    tokenBlock.Balances.Add(balance.Key, balance.Value);

            tokenBlock.InitializeBlock(latestBlock, _signer);

            //tokenBlock.Signature = Signatures.GetSignature(PrivateKey, tokenBlock.Hash);

            var result = await _trans.CreateToken(tokenBlock);

            return result;
        }

        private async Task<string[]> GetProperTokenNameAsync(string[] tokenNames)
        {
            var result = await tokenNames.SelectAsync(async a => await _rpcClient.GetTokenGenesisBlock(AccountId, a, null));
            return result.Select(a => a.GetBlock() as TokenGenesisBlock)
                .Select(b => b?.Ticker)
                .OrderBy(a => a)
                .ToArray();
        }

        public async Task<PoolInfoAPIResult> GetLiquidatePoolAsync(string token0, string token1)
        {
            var result = await _rpcClient.GetPool(token0, token1);
            return result;
        }

        public async Task<APIResult> CreateLiquidatePoolAsync(string token0, string token1)
        {
            var tokenNames = await GetProperTokenNameAsync(new[] { token0, token1 });

            if (tokenNames.Any(a => a == null))
                return new APIResult { ResultCode = APIResultCodes.TokenGenesisBlockNotFound };

            var pool = await _rpcClient.GetPool(tokenNames[0], tokenNames[1]);
            if (pool.PoolAccountId != null)
                return new APIResult { ResultCode = APIResultCodes.PoolAlreadyExists };

            var tags = new Dictionary<string, string>();
            tags.Add("token0", tokenNames[0]);
            tags.Add("token1", tokenNames[1]);
            tags.Add(Block.REQSERVICETAG, "");
            var amounts = new Dictionary<string, decimal>();
            amounts.Add(LyraGlobal.OFFICIALTICKERCODE, PoolFactoryBlock.PoolCreateFee);
            return await SendEx(pool.PoolFactoryAccountId, amounts, tags);
        }

        public async Task<APIResult> AddLiquidateToPoolAsync(string token0, decimal token0Amount, string token1, decimal token1Amount)
        {
            var pool = await _rpcClient.GetPool(token0, token1);
            if (pool.PoolAccountId == null)
                return new APIResult { ResultCode = APIResultCodes.PoolNotExists };

            var amountsDeposit = new Dictionary<string, decimal>();
            amountsDeposit.Add(token0, token0Amount);
            amountsDeposit.Add(token1, token1Amount);

            var tags = new Dictionary<string, string>();
            tags.Add("token0", pool.Token0);
            tags.Add("token1", pool.Token1);
            tags.Add(Block.REQSERVICETAG, "");

            var poolDepositResult = await SendEx(pool.PoolAccountId, amountsDeposit, tags);
            return poolDepositResult;
        }

        public async Task<APIResult> RemoveLiquidateFromPoolAsync(string token0, string token1)
        {
            var pool = await _rpcClient.GetPool(token0, token1);
            if (pool.PoolAccountId == null)
                return new APIResult { ResultCode = APIResultCodes.PoolNotExists };

            var tags = new Dictionary<string, string>();
            tags.Add(Block.REQSERVICETAG, "poolwithdraw");
            tags.Add("poolid", pool.PoolAccountId);
            tags.Add("token0", pool.Token0);
            tags.Add("token1", pool.Token1);
            var amounts = new Dictionary<string, decimal>();
            amounts.Add(LyraGlobal.OFFICIALTICKERCODE, 1m);
            var poolWithdrawResult = await SendEx(pool.PoolFactoryAccountId, amounts, tags);
            return poolWithdrawResult;
        }

        public async Task<APIResult> SwapToken(string token0, string token1, string tokenToSwap, decimal amountToSwap, decimal amountToGet)
        {
            var pool = await _rpcClient.GetPool(token0, token1);
            if (pool.PoolAccountId == null)
                return new APIResult { ResultCode = APIResultCodes.PoolNotExists };

            var tags = new Dictionary<string, string>();
            tags.Add(Block.REQSERVICETAG, "swaptoken");
            tags.Add("poolid", pool.PoolAccountId);
            tags.Add("token0", pool.Token0);
            tags.Add("token1", pool.Token1);
            tags.Add("minrecv", $"{amountToGet.ToBalanceLong()}");
            var amounts = new Dictionary<string, decimal>();
            amounts.Add(tokenToSwap, amountToSwap);
            var swapTokenResult = await SendEx(pool.PoolAccountId, amounts, tags);
            return swapTokenResult;
        }

        public async Task<long> GetLocalAccountHeightAsync()
        {
            var lastTrans = await GetLatestBlockAsync();
            if (lastTrans != null)
                return lastTrans.Height;
            else
                return 0;
        }

        private async Task<TransactionBlock> GetLatestBlockAsync()
        {
            var blockResult = await _rpcClient.GetLastBlock(_accountId);
            if (blockResult.ResultCode == APIResultCodes.Success)
            {
                return blockResult.GetBlock() as TransactionBlock;
            }
            else
                return null;
        }
    }
}