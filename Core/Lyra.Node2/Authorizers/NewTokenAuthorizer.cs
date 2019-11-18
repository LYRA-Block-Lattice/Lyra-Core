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
    public class NewTokenAuthorizer: BaseAuthorizer
    {
        public NewTokenAuthorizer(ServiceAccount serviceAccount, IAccountCollection accountCollection): base (serviceAccount, accountCollection)
        {
        }

        public override APIResultCodes Authorize<T>(ref T tblock)
        {

            if (!(tblock is TokenGenesisBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as TokenGenesisBlock;

            // Local node validations - before it sends it out to the authorization sample:
            // 1. check if the account already exists
            if (!_accountCollection.AccountExists(block.AccountID))
                return APIResultCodes.AccountDoesNotExist; // 

            TransactionBlock lastBlock = _accountCollection.FindLatestBlock(block.AccountID);
            if (lastBlock == null)
                return APIResultCodes.CouldNotFindLatestBlock;

            // 2. Validate blocks
            var result = VerifyBlock(block, lastBlock);
            if (result != APIResultCodes.Success)
                return result;

            result = VerifyTransactionBlock(block);
            if (result != APIResultCodes.Success)
                return result;

            // check LYR balance
            if (lastBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE] != block.Balances[TokenGenesisBlock.LYRA_TICKER_CODE] + block.Fee)
                return APIResultCodes.InvalidNewAccountBalance;

            // check if this token already exists
            //AccountData genesis_blocks = _accountCollection.GetAccount(AccountCollection.GENESIS_BLOCKS);
            //if (genesis_blocks.FindTokenGenesisBlock(testTokenGenesisBlock) != null)
            if (_accountCollection.FindTokenGenesisBlock(block.Hash, block.Ticker) != null)
                return APIResultCodes.TokenGenesisBlockAlreadyExists;

            if (block.Fee != _serviceAccount.GetLastServiceBlock().TokenGenerationFee)
                return APIResultCodes.InvalidFeeAmount;

            if (block.IsNonFungible)
            {
                if (!Signatures.ValidateAccountId(block.NonFungibleKey))
                    return APIResultCodes.InvalidNonFungiblePublicKey;
            }

            if (block.RenewalDate > DateTime.Now.Add(TimeSpan.FromDays(366)) || block.RenewalDate < DateTime.Now)
                return APIResultCodes.InvalidTokenRenewalDate;

            // sign with the authorizer key
            Sign(ref block);

            _accountCollection.AddBlock(block);

            return APIResultCodes.Success;


        }

        protected override APIResultCodes ValidateFee(TransactionBlock block)
        {
            if (block.FeeType != AuthorizationFeeTypes.Regular)
                return APIResultCodes.InvalidFeeAmount;

            if (block.Fee != _serviceAccount.GetLastServiceBlock().TokenGenerationFee)
                return APIResultCodes.InvalidFeeAmount;

            return APIResultCodes.Success;
        }



    }
}
