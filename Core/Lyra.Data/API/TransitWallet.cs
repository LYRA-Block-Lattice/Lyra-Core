using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Lyra.Data.API
{
    public class TransitWallet
    {
        private readonly string _signingPrivateKey;
        private readonly string _signingAccountId;
        private readonly string _accountId;
        private readonly Func<string, Task<string>> _signer;

        private readonly LyraRestClient _rpcClient;

        // for signature handler
        public string AccountId => _accountId;

        public string LastTxHash { get; private set; }

        public TransitWallet(string AccountId, string signingPrivateKey, LyraRestClient client)
        {
            _signingPrivateKey = signingPrivateKey;
            _signingAccountId = Signatures.GetAccountIdFromPrivateKey(_signingPrivateKey);

            _accountId = AccountId;

            _rpcClient = client;

            _signer = (hash) => Task.FromResult(Signatures.GetSignature(_signingPrivateKey, hash, _signingAccountId));
        }

        public async Task<SortedDictionary<string, long>> GetBalanceAsync()
        {
            var lastTx = await GetLatestBlockAsync();
            if (lastTx == null)
                return null;
            return lastTx.Balances;
        }

        public async Task<APIResultCodes> ReceiveAsync()
        {
            var blockresult = await _rpcClient.GetLastServiceBlockAsync();

            if (blockresult.ResultCode != APIResultCodes.Success)
                return blockresult.ResultCode;

            ServiceBlock lastServiceBlock = blockresult.GetBlock() as ServiceBlock;
            try
            {
                var signature = await _signer(lastServiceBlock.Hash);
                var lookup_result = await _rpcClient.LookForNewTransfer2Async(_accountId, signature);
                int max_counter = 0;

                while (lookup_result.Successful() && max_counter < 100) // we don't want to enter an endless loop...
                {
                    max_counter++;

                    //PrintConLine($"Received new transaction, sending request for settlement...");

                    var receive_result = await ReceiveTransferAsync(lookup_result);
                    if (!receive_result.Successful())
                        return receive_result.ResultCode;

                    lookup_result = await _rpcClient.LookForNewTransfer2Async(_accountId, await _signer(lastServiceBlock.Hash));
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
            catch
            {
                //PrintConLine("Exception in SyncIncomingTransfers(): " + e.Message);
                return APIResultCodes.UnknownError;
            }
        }

        public async Task<APIResultCodes> SendAsync(Dictionary<string, decimal> Amounts, string destAccount)
        {
            if (Amounts.Any(a => a.Value <= 0))
                throw new Exception("Amount must > 0");

            TransactionBlock previousBlock = await GetLatestBlockAsync();
            if (previousBlock == null)
            {
                return APIResultCodes.InsufficientFunds;
            }

            if (Amounts.Any(a => !previousBlock.Balances.ContainsKey(a.Key) || previousBlock.Balances[a.Key].ToBalanceDecimal() < a.Value))
                return APIResultCodes.InsufficientFunds;

            var blockresult = await _rpcClient.GetLastServiceBlockAsync();

            if (blockresult.ResultCode != APIResultCodes.Success)
                return blockresult.ResultCode;

            ServiceBlock lastServiceBlock = blockresult.GetBlock() as ServiceBlock;

            var fee = lastServiceBlock.TransferFee;

            SendTransferBlock sendBlock;
            sendBlock = new SendTransferBlock()
            {
                AccountID = _accountId,
                VoteFor = null,
                ServiceHash = lastServiceBlock.Hash,
                DestinationAccountId = destAccount,
                Balances = new SortedDictionary<string, long>(),
                //PaymentID = string.Empty,
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
            sendBlock.Balances[LyraGlobal.OFFICIALTICKERCODE] = (sendBlock.Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal() - fee).ToBalanceLong();

            await sendBlock.InitializeBlockAsync(previousBlock, _signer);

            if (!sendBlock.ValidateTransaction(previousBlock))
            {
                return APIResultCodes.SendTransactionValidationFailed;
                //throw new Exception("ValidateTransaction failed");
            }

            AuthorizationAPIResult result;
            result = await _rpcClient.SendTransferAsync(sendBlock);
            if(result.ResultCode == APIResultCodes.Success)
            {
                LastTxHash = sendBlock.Hash;
            }
            else
            {
                LastTxHash = "";
            }

            return result.ResultCode;
        }

        private async Task<AuthorizationAPIResult> ReceiveTransferAsync(NewTransferAPIResult2 new_transfer_info)
        {
            if (await GetLocalAccountHeightAsync() == 0) // if this is new account with no blocks
                return await OpenStandardAccountWithReceiveBlockAsync(new_transfer_info);

            var svcBlockResult = await _rpcClient.GetLastServiceBlockAsync();
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
                Balances = new SortedDictionary<string, long>(),
                Fee = 0,
                FeeType = AuthorizationFeeTypes.NoFee,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
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

            await receiveBlock.InitializeBlockAsync(latestBlock, _signer);

            if (!receiveBlock.ValidateTransaction(latestBlock))
                throw new Exception("ValidateTransaction failed");

            //receiveBlock.Signature = Signatures.GetSignature(PrivateKey, receiveBlock.Hash);

            var result = await _rpcClient.ReceiveTransferAsync(receiveBlock);

            return result;
        }

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
                AccountID = _accountId,
                ServiceHash = svcBlockResult.GetBlock().Hash,
                SourceHash = new_transfer_info.SourceHash,
                Balances = new SortedDictionary<string, long>(),
                Fee = 0,
                FeeType = AuthorizationFeeTypes.NoFee,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                NonFungibleToken = new_transfer_info.NonFungibleToken,
                VoteFor = null
            };

            foreach (var chg in new_transfer_info.Transfer.Changes)
            {
                openReceiveBlock.Balances.Add(chg.Key, chg.Value.ToBalanceLong());
            }
            await openReceiveBlock.InitializeBlockAsync(null, _signer);

            //openReceiveBlock.Signature = Signatures.GetSignature(PrivateKey, openReceiveBlock.Hash);

            var result = await _rpcClient.ReceiveTransferAndOpenAccountAsync(openReceiveBlock);

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
            var blockResult = await _rpcClient.GetLastBlockAsync(_accountId);
            if (blockResult.ResultCode == APIResultCodes.Success)
            {
                return blockResult.GetBlock() as TransactionBlock;
            }
            else
                return null;
        }
    }
}
