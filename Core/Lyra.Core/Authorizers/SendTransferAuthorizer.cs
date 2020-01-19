using System;
using System.Collections.Generic;
using Lyra.Core.Blocks;
using Lyra.Core.Cryptography;
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

        public override async Task<(APIResultCodes, AuthorizationSignature)> AuthorizeAsync<T>(T tblock, bool WithSign = true)
        {
            var result = await AuthorizeImplAsync(tblock);
            if (APIResultCodes.Success == result)
                return (APIResultCodes.Success, await SignAsync(tblock));
            else
                return (result, (AuthorizationSignature)null);
        }
        private async Task<APIResultCodes> AuthorizeImplAsync<T>(T tblock)
        {
            if (!(tblock is SendTransferBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as SendTransferBlock;

            //// 1. check if the account already exists
            //if (!await BlockChain.Singleton.AccountExists(block.AccountID))
            //    return APIResultCodes.AccountDoesNotExist;
            var stopwatch = Stopwatch.StartNew();

            //TransactionBlock lastBlock = null;
            //int count = 50;
            //while(count-- > 0)
            //{
            //    lastBlock = await BlockChain.Singleton.FindBlockByHashAsync(block.PreviousHash);
            //    if (lastBlock != null)
            //        break;
            //    Task.Delay(100).Wait();
            //}

            TransactionBlock lastBlock = await BlockChain.Singleton.FindBlockByHashAsync(block.PreviousHash);

            //TransactionBlock lastBlock = await BlockChain.Singleton.FindLatestBlock(block.AccountID);
            if (lastBlock == null)
                return APIResultCodes.CouldNotFindLatestBlock;
            
            var result = await VerifyBlockAsync(block, lastBlock);
            stopwatch.Stop();
            Console.WriteLine($"SendTransfer VerifyBlock takes {stopwatch.ElapsedMilliseconds} ms.");

            if (result != APIResultCodes.Success)
                return result;

            //if (lastBlock.Balances[LyraGlobal.LYRA_TICKER_CODE] <= block.Balances[LyraGlobal.LYRA_TICKER_CODE] + block.Fee)
            //    return AuthorizationResultCodes.NegativeTransactionAmount;

            // Validate the destination account id
            if (!Signatures.ValidateAccountId(block.DestinationAccountId))
                return APIResultCodes.InvalidDestinationAccountId;

            var stopwatch2 = Stopwatch.StartNew();
            result = await VerifyTransactionBlockAsync(block);
            stopwatch2.Stop();
            Console.WriteLine($"SendTransfer VerifyTransactionBlock takes {stopwatch2.ElapsedMilliseconds} ms.");
            if (result != APIResultCodes.Success)
                return result;

            var stopwatch3 = Stopwatch.StartNew();
            if (!block.ValidateTransaction(lastBlock))
                return APIResultCodes.SendTransactionValidationFailed;

            result = await ValidateNonFungibleAsync(block, lastBlock);
            if (result != APIResultCodes.Success)
                return result;

            stopwatch3.Stop();
            Console.WriteLine($"SendTransfer ValidateTransaction & ValidateNonFungible takes {stopwatch3.ElapsedMilliseconds} ms.");

            return APIResultCodes.Success;
        }

        protected override async Task<APIResultCodes> ValidateFeeAsync(TransactionBlock block)
        {
            APIResultCodes result;
            if (block.FeeType != AuthorizationFeeTypes.Regular)
                result = APIResultCodes.InvalidFeeAmount;

            if (block.Fee != (await BlockChain.Singleton.GetLastServiceBlockAsync()).TransferFee)
                result = APIResultCodes.InvalidFeeAmount;

            result = APIResultCodes.Success;

            return result;
        }

        protected override async Task<APIResultCodes> ValidateNonFungibleAsync(TransactionBlock send_or_receice_block, TransactionBlock previousBlock)
        {
            var result = await base.ValidateNonFungibleAsync(send_or_receice_block, previousBlock);
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