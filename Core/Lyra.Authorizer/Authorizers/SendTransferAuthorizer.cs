using System;
using System.Collections.Generic;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;

using Lyra.Core.Cryptography;
using Lyra.Core.API;
using Lyra.Core.Accounts.Node;
using Lyra.Authorizer.Services;
using Lyra.Authorizer.Decentralize;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Lyra.Authorizer.Authorizers
{
    public class SendTransferAuthorizer : BaseAuthorizer
    {
        public SendTransferAuthorizer(IOptions<LyraConfig> config, ServiceAccount serviceAccount, IAccountCollection accountCollection)
            : base(config, serviceAccount, accountCollection)
        {
        }

        public override Task<APIResultCodes> Authorize<T>(T tblock)
        {
            return AuthorizeImplAsync<T>(tblock);
        }
        private async Task<APIResultCodes> AuthorizeImplAsync<T>(T tblock)
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

            var result = await VerifyBlockAsync(block, lastBlock);
            if (result != APIResultCodes.Success)
                return result;

            //if (lastBlock.Balances[LyraGlobal.LYRA_TICKER_CODE] <= block.Balances[LyraGlobal.LYRA_TICKER_CODE] + block.Fee)
            //    return AuthorizationResultCodes.NegativeTransactionAmount;

            // Validate the destination account id
            if (!SignaturesBase.ValidateAccountId(block.DestinationAccountId))
                return APIResultCodes.InvalidDestinationAccountId;

            result = await VerifyTransactionBlockAsync(block);
            if (result != APIResultCodes.Success)
                return result;

            if (!block.ValidateTransaction(lastBlock))
                return APIResultCodes.SendTransactionValidationFailed;

            result = await ValidateNonFungibleAsync(block, lastBlock);
            if (result != APIResultCodes.Success)
                return result;

            return APIResultCodes.Success;
        }

        protected override APIResultCodes ValidateFee(TransactionBlock block)
        {
            if (block.FeeType != AuthorizationFeeTypes.Regular)
                return APIResultCodes.InvalidFeeAmount;

            if (block.Fee != _serviceAccount.GetLastServiceBlock().TransferFee)
                return APIResultCodes.InvalidFeeAmount;

            return APIResultCodes.Success;
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