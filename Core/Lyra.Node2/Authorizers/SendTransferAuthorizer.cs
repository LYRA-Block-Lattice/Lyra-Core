using System;
using System.Collections.Generic;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;

using Lyra.Core.Cryptography;
using Lyra.Core.API;
using Lyra.Core.Accounts.Node;
using Lyra.Node2.Services;
using Lyra.Core.Protos;

namespace Lyra.Node2.Authorizers
{
    public class SendTransferAuthorizer : BaseAuthorizer
    {
        public SendTransferAuthorizer(ServiceAccount serviceAccount, IAccountCollection accountCollection) : base(serviceAccount, accountCollection)
        {
        }

        public override APIResultCodes Authorize<T>(ref T tblock)
        {
            if (!(tblock is SendTransferBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as SendTransferBlock;

            // 1. check if the account already exists
            if (!_accountCollection.AccountExists(block.AccountID))
                return APIResultCodes.AccountDoesNotExist;

            TransactionBlock lastBlock = _accountCollection.FindLatestBlock(block.AccountID);
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

            Sign(ref block);

            _accountCollection.AddBlock(block);

            return base.Authorize(ref tblock);
        }

        protected override APIResultCodes ValidateFee(TransactionBlock block)
        {
            if (block.FeeType != AuthorizationFeeTypes.Regular)
                return APIResultCodes.InvalidFeeAmount;

            if (block.Fee != _serviceAccount.GetLastServiceBlock().TransferFee)
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