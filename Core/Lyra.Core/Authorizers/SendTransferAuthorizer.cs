using System;
using System.Collections.Generic;
using Lyra.Core.Blocks;
using Lyra.Core.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Lyra.Core.Utils;
using Lyra.Core.Accounts;

namespace Lyra.Core.Authorizers
{
    public class SendTransferAuthorizer : BaseAuthorizer
    {
        public SendTransferAuthorizer()
        {
        }

        public override (APIResultCodes, AuthorizationSignature) Authorize<T>(T tblock)
        {
            var result = AuthorizeImpl(tblock);
            if (APIResultCodes.Success == result)
                return (APIResultCodes.Success, Sign(tblock));
            else
                return (result, (AuthorizationSignature)null);
        }
        private APIResultCodes AuthorizeImpl<T>(T tblock)
        {
            if (!(tblock is SendTransferBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as SendTransferBlock;

            // 1. check if the account already exists
            if (!BlockChain.Singleton.AccountExists(block.AccountID))
                return APIResultCodes.AccountDoesNotExist;

            TransactionBlock lastBlock = BlockChain.Singleton.FindLatestBlock(block.AccountID);
            if (lastBlock == null)
                return APIResultCodes.CouldNotFindLatestBlock;

            var result = VerifyBlock(block, lastBlock);
            if (result != APIResultCodes.Success)
                return result;

            //if (lastBlock.Balances[LyraGlobal.LYRA_TICKER_CODE] <= block.Balances[LyraGlobal.LYRA_TICKER_CODE] + block.Fee)
            //    return AuthorizationResultCodes.NegativeTransactionAmount;

            // Validate the destination account id
            if (!Signatures.ValidateAccountId(block.DestinationAccountId))
                return APIResultCodes.InvalidDestinationAccountId;

            result = VerifyTransactionBlock(block);
            if (result != APIResultCodes.Success)
                return result;

            if (!block.ValidateTransaction(lastBlock))
                return APIResultCodes.SendTransactionValidationFailed;

            result = ValidateNonFungible(block, lastBlock);
            if (result != APIResultCodes.Success)
                return result;

            return APIResultCodes.Success;
        }

        protected override APIResultCodes ValidateFee(TransactionBlock block)
        {
            if (block.FeeType != AuthorizationFeeTypes.Regular)
                return APIResultCodes.InvalidFeeAmount;

            if (block.Fee != BlockChain.Singleton.GetLastServiceBlock().TransferFee)
                return APIResultCodes.InvalidFeeAmount;

            return APIResultCodes.Success;
        }

        protected override APIResultCodes ValidateNonFungible(TransactionBlock send_or_receice_block, TransactionBlock previousBlock)
        {
            var result = base.ValidateNonFungible(send_or_receice_block, previousBlock);
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