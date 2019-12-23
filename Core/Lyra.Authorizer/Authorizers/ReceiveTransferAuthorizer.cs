using System;
using System.Collections.Generic;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;

using Lyra.Core.Cryptography;
using Lyra.Core.API;
using Lyra.Core.Accounts.Node;
using Lyra.Authorizer.Services;
using Lyra.Authorizer.Decentralize;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using Lyra.Core.Utils;

namespace Lyra.Authorizer.Authorizers
{
    public class ReceiveTransferAuthorizer: BaseAuthorizer
    {
        public ReceiveTransferAuthorizer(IOptions<LyraNodeConfig> config, ServiceAccount serviceAccount, IAccountCollection accountCollection)
            : base(config, serviceAccount, accountCollection)
        {
        }

        public override Task<APIResultCodes> Authorize<T>(T tblock)
        {
            return AuthorizeImplAsync<T>(tblock);
        }
        private async Task<APIResultCodes> AuthorizeImplAsync<T>(T tblock)
        {
            if (!(tblock is ReceiveTransferBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as ReceiveTransferBlock;

            // 1. check if the account already exists
            if (!_accountCollection.AccountExists(block.AccountID))
                return APIResultCodes.AccountDoesNotExist;

            TransactionBlock lastBlock = _accountCollection.FindLatestBlock(block.AccountID);
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

            result = ValidateReceiveTransAmount(block, block.GetTransaction(lastBlock));
            if (result != APIResultCodes.Success)
                return result;

            result = await ValidateNonFungibleAsync(block, lastBlock);
            if (result != APIResultCodes.Success)
                return result;

            // Check duplicate receives (kind of double spending up down)
            var duplicate_block = _accountCollection.FindBlockBySourceHash(block.SourceHash);
            if (duplicate_block != null)
                return APIResultCodes.DuplicateReceiveBlock;

            var signed = await Sign(block);
            if (signed)
            {
                _accountCollection.AddBlock(block);
                return APIResultCodes.Success;
            }
            else
            {
                return APIResultCodes.NotAllowedToSign;
            }
        }

        protected override APIResultCodes ValidateFee(TransactionBlock block)
        {
            if (block.FeeType != AuthorizationFeeTypes.NoFee)
                return APIResultCodes.InvalidFeeAmount;

            if (block.Fee != 0)
                return APIResultCodes.InvalidFeeAmount;

            return APIResultCodes.Success;
        }


        protected APIResultCodes ValidateReceiveTransAmount(ReceiveTransferBlock block, TransactionInfo receiveTransaction)
        {
            //find the corresponding send block and validate the added transaction amount
            var sourceBlock = _accountCollection.FindBlockByHash(block.SourceHash);
            if (sourceBlock == null)
                return APIResultCodes.SourceSendBlockNotFound;


            // find the actual amount of transaction 
            TransactionInfo sendTransaction;
            if (block.BlockType == BlockTypes.ReceiveTransfer || block.BlockType == BlockTypes.OpenAccountWithReceiveTransfer)
            {
                if ((sourceBlock as SendTransferBlock).DestinationAccountId != block.AccountID)
                    return APIResultCodes.InvalidDestinationAccountId;

                TransactionBlock prevToSendBlock = _accountCollection.FindBlockByHash(sourceBlock.PreviousHash);
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
                sendTransaction = new TransactionInfo() { TokenCode = LyraGlobal.LYRA_TICKER_CODE, Amount = sourceBlock.Fee };
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

            var originBlock = _accountCollection.FindBlockByHash((send_or_receice_block as ReceiveTransferBlock).SourceHash);
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
