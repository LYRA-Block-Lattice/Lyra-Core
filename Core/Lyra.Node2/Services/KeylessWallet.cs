using Lyra.Core.API;
using Lyra.Core.Blocks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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