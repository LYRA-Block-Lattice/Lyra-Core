using System;
using System.Collections.Generic;
using Lyra.Core.Blocks;
using Lyra.Core.API;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using Lyra.Core.Utils;
using Lyra.Core.Accounts;

namespace Lyra.Core.Authorizers
{
    public class ReceiveTransferAuthorizer: BaseAuthorizer
    {
        public ReceiveTransferAuthorizer()
        {
        }

        public override async Task<(APIResultCodes, AuthorizationSignature)> AuthorizeAsync<T>(T tblock, bool WithSign = true)
        {
            var result = await AuthorizeImplAsync(tblock);
            if (APIResultCodes.Success == result)
                return (APIResultCodes.Success, Sign(tblock));
            else
                return (result, (AuthorizationSignature)null);
        }
        private async Task<APIResultCodes> AuthorizeImplAsync<T>(T tblock)
        {
            if (!(tblock is ReceiveTransferBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as ReceiveTransferBlock;

            // 1. check if the account already exists
            if (!await BlockChain.Singleton.AccountExistsAsync(block.AccountID))
                return APIResultCodes.AccountDoesNotExist;

            TransactionBlock lastBlock = await BlockChain.Singleton.FindLatestBlockAsync(block.AccountID) as TransactionBlock;
            if (lastBlock == null)
                return APIResultCodes.CouldNotFindLatestBlock;

            var result = await VerifyBlockAsync(block, lastBlock);
            if (result != APIResultCodes.Success)
                return result;

            result = await VerifyTransactionBlockAsync(block);
            if (result != APIResultCodes.Success)
                return result;

            if (!block.ValidateTransaction(lastBlock))
                return APIResultCodes.ReceiveTransactionValidationFailed;

            result = await ValidateReceiveTransAmountAsync(block, block.GetTransaction(lastBlock));
            if (result != APIResultCodes.Success)
                return result;

            result = await ValidateNonFungibleAsync(block, lastBlock);
            if (result != APIResultCodes.Success)
                return result;

            // Check duplicate receives (kind of double spending up down)
            var duplicate_block = await BlockChain.Singleton.FindBlockBySourceHashAsync(block.SourceHash);
            if (duplicate_block != null)
                return APIResultCodes.DuplicateReceiveBlock;

            return APIResultCodes.Success;
        }

        //protected override Task<APIResultCodes> ValidateFeeAsync(TransactionBlock block)
        //{
        //    if (block.FeeType != AuthorizationFeeTypes.NoFee)
        //        return Task.FromResult(APIResultCodes.InvalidFeeAmount);

        //    if (block.Fee != 0)
        //        return Task.FromResult(APIResultCodes.InvalidFeeAmount);

        //    return Task.FromResult(APIResultCodes.Success);
        //}


        protected async Task<APIResultCodes> ValidateReceiveTransAmountAsync(ReceiveTransferBlock block, TransactionInfo receiveTransaction)
        {
            //find the corresponding send block and validate the added transaction amount
            var sourceBlock = await BlockChain.Singleton.FindBlockByHashAsync(block.SourceHash) as TransactionBlock;
            if (sourceBlock == null)
                return APIResultCodes.SourceSendBlockNotFound;


            // find the actual amount of transaction 
            TransactionInfo sendTransaction;
            if (block.BlockType == BlockTypes.ReceiveTransfer || block.BlockType == BlockTypes.OpenAccountWithReceiveTransfer)
            {
                if ((sourceBlock as SendTransferBlock).DestinationAccountId != block.AccountID)
                    return APIResultCodes.InvalidDestinationAccountId;

                TransactionBlock prevToSendBlock = await BlockChain.Singleton.FindBlockByHashAsync(sourceBlock.PreviousHash) as TransactionBlock;
                if (prevToSendBlock == null)
                    return APIResultCodes.CouldNotTraceSendBlockChain;

                sendTransaction = sourceBlock.GetTransaction(prevToSendBlock);

                if (!sourceBlock.ValidateTransaction(prevToSendBlock))
                    return APIResultCodes.SendTransactionValidationFailed;
                //originallySentAmount = sendTransaction.Amount;
                //originallySentAmount = 
                //    prevToSendBlock.Balances[LyraGlobal.LYRA_TICKER_CODE] - sourceBlock.Balances[LyraGlobal.LYRA_TICKER_CODE] - (sourceBlock as IFeebleBlock).Fee;
            }
            else
            if (block.BlockType == BlockTypes.ReceiveFee || block.BlockType == BlockTypes.OpenAccountWithReceiveFee)
            {
                sendTransaction = new TransactionInfo() { TokenCode = LyraGlobal.LYRATICKERCODE, Amount = sourceBlock.Fee };
            }
            else
                return APIResultCodes.InvalidBlockType;

            if (sendTransaction.Amount != receiveTransaction.Amount)
                return APIResultCodes.TransactionAmountDoesNotMatch;

            if (sendTransaction.TokenCode != receiveTransaction.TokenCode)
                return APIResultCodes.TransactionTokenDoesNotMatch;

            return APIResultCodes.Success;
        }

        protected override async Task<APIResultCodes> ValidateNonFungibleAsync(TransactionBlock send_or_receice_block, TransactionBlock previousBlock)
        {
            var result = await base.ValidateNonFungibleAsync(send_or_receice_block, previousBlock);
            if (result != APIResultCodes.Success)
                return result;

            if (send_or_receice_block.NonFungibleToken == null)
                return APIResultCodes.Success;

            var originBlock = await BlockChain.Singleton.FindBlockByHashAsync((send_or_receice_block as ReceiveTransferBlock).SourceHash) as TransactionBlock;
            if (originBlock == null)
                return APIResultCodes.OriginNonFungibleBlockNotFound;

            if (!originBlock.ContainsNonFungibleToken())
                return APIResultCodes.OriginNonFungibleBlockNotFound;

            if (originBlock.NonFungibleToken.Hash != send_or_receice_block.NonFungibleToken.Hash)
                return APIResultCodes.OriginNonFungibleBlockHashDoesNotMatch;

            return APIResultCodes.Success;
        }


    }
}
