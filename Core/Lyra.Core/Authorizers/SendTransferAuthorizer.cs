using System;
using System.Collections.Generic;
using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Lyra.Core.Utils;
using Lyra.Core.Accounts;
using System.Diagnostics;

namespace Lyra.Core.Authorizers
{
    public class SendTransferAuthorizer : BaseAuthorizer
    {
        public SendTransferAuthorizer()
        {
        }

        public override async Task<(APIResultCodes, AuthorizationSignature)> AuthorizeAsync<T>(DagSystem sys, T tblock)
        {
            var result = await AuthorizeImplAsync(sys, tblock);
            if (APIResultCodes.Success == result)
                return (APIResultCodes.Success, Sign(sys, tblock));
            else
                return (result, (AuthorizationSignature)null);
        }
        private async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is SendTransferBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as SendTransferBlock;

            if (block.AccountID.Equals(block.DestinationAccountId))
                return APIResultCodes.CannotSendToSelf;

            //// 1. check if the account already exists
            //if (!await sys.Storage.AccountExists(block.AccountID))
            //    return APIResultCodes.AccountDoesNotExist;
            //var stopwatch = Stopwatch.StartNew();

            //TransactionBlock lastBlock = null;
            //int count = 50;
            //while(count-- > 0)
            //{
            //    lastBlock = await sys.Storage.FindBlockByHashAsync(block.PreviousHash);
            //    if (lastBlock != null)
            //        break;
            //    Task.Delay(100).Wait();
            //}

            if (await sys.Storage.WasAccountImportedAsync(block.AccountID))
                return APIResultCodes.CannotModifyImportedAccount;

            TransactionBlock lastBlock = await sys.Storage.FindBlockByHashAsync(block.PreviousHash) as TransactionBlock;

            //TransactionBlock lastBlock = await sys.Storage.FindLatestBlock(block.AccountID);
            if (lastBlock == null)
                return APIResultCodes.PreviousBlockNotFound;
            
            var result = await VerifyBlockAsync(sys, block, lastBlock);
            //stopwatch.Stop();
            //Console.WriteLine($"SendTransfer VerifyBlock takes {stopwatch.ElapsedMilliseconds} ms.");

            if (result != APIResultCodes.Success)
                return result;

            //if (lastBlock.Balances[LyraGlobal.LYRA_TICKER_CODE] <= block.Balances[LyraGlobal.LYRA_TICKER_CODE] + block.Fee)
            //    return AuthorizationResultCodes.NegativeTransactionAmount;

            // Validate the destination account id
            if (!Signatures.ValidateAccountId(block.DestinationAccountId))
                return APIResultCodes.InvalidDestinationAccountId;

            //var stopwatch2 = Stopwatch.StartNew();
            result = await VerifyTransactionBlockAsync(sys, block);
            //stopwatch2.Stop();
            //Console.WriteLine($"SendTransfer VerifyTransactionBlock takes {stopwatch2.ElapsedMilliseconds} ms.");
            if (result != APIResultCodes.Success)
                return result;

            //var stopwatch3 = Stopwatch.StartNew();
            if (!block.ValidateTransaction(lastBlock))
                return APIResultCodes.SendTransactionValidationFailed;

            result = await ValidateNonFungibleAsync(sys, block, lastBlock);
            if (result != APIResultCodes.Success)
                return result;

            //stopwatch3.Stop();
            //Console.WriteLine($"SendTransfer ValidateTransaction & ValidateNonFungible takes {stopwatch3.ElapsedMilliseconds} ms.");

            return APIResultCodes.Success;
        }

        protected override async Task<APIResultCodes> ValidateFeeAsync(DagSystem sys, TransactionBlock block)
        {
            APIResultCodes result = APIResultCodes.Success;
            if (block.FeeType != AuthorizationFeeTypes.Regular)
                result = APIResultCodes.InvalidFeeAmount;

            if (block.Fee != (await sys.Storage.GetLastServiceBlockAsync()).TransferFee)
                result = APIResultCodes.InvalidFeeAmount;

            return result;
        }

        protected override async Task<APIResultCodes> ValidateNonFungibleAsync(DagSystem sys, TransactionBlock send_or_receice_block, TransactionBlock previousBlock)
        {
            var result = await base.ValidateNonFungibleAsync(sys, send_or_receice_block, previousBlock);
            if (result != APIResultCodes.Success)
                return result;

            if (send_or_receice_block.NonFungibleToken == null)
                return APIResultCodes.Success;

            //if (send_or_receice_block.NonFungibleToken.OriginHash != send_or_receice_block.Hash)
            //    return APIResultCodes.OriginNonFungibleBlockHashDoesNotMatch;


            return APIResultCodes.Success;
        }
    }
}