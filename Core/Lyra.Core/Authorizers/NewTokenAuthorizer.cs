using System;
using System.Collections.Generic;
using Lyra.Core.Blocks;
using Lyra.Core.Cryptography;
using Lyra.Core.API;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Lyra.Core.Utils;
using Lyra.Core.Accounts;

namespace Lyra.Core.Authorizers
{
    public class NewTokenAuthorizer: BaseAuthorizer
    {
        public NewTokenAuthorizer()
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
            if (!(tblock is TokenGenesisBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as TokenGenesisBlock;

            // Local node validations - before it sends it out to the authorization sample:
            // 1. check if the account already exists
            if (!BlockChain.Singleton.AccountExists(block.AccountID))
                return APIResultCodes.AccountDoesNotExist; // 

            TransactionBlock lastBlock = BlockChain.Singleton.FindLatestBlock(block.AccountID);
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
            if (lastBlock.Balances[LyraGlobal.LYRA_TICKER_CODE] != block.Balances[LyraGlobal.LYRA_TICKER_CODE] + block.Fee)
                return APIResultCodes.InvalidNewAccountBalance;

            // check if this token already exists
            //AccountData genesis_blocks = _accountCollection.GetAccount(AccountCollection.GENESIS_BLOCKS);
            //if (genesis_blocks.FindTokenGenesisBlock(testTokenGenesisBlock) != null)
            if (BlockChain.Singleton.FindTokenGenesisBlock(block.Hash, block.Ticker) != null)
                return APIResultCodes.TokenGenesisBlockAlreadyExists;

            if (block.Fee != BlockChain.Singleton.GetLastServiceBlock().TokenGenerationFee)
                return APIResultCodes.InvalidFeeAmount;

            if (block.IsNonFungible)
            {
                if (!Signatures.ValidateAccountId(block.NonFungibleKey))
                    return APIResultCodes.InvalidNonFungiblePublicKey;
            }

            if (block.RenewalDate > DateTime.Now.Add(TimeSpan.FromDays(366)) || block.RenewalDate < DateTime.Now)
                return APIResultCodes.InvalidTokenRenewalDate;

            return APIResultCodes.Success;
        }

        protected override APIResultCodes ValidateFee(TransactionBlock block)
        {
            if (block.FeeType != AuthorizationFeeTypes.Regular)
                return APIResultCodes.InvalidFeeAmount;

            if (block.Fee != BlockChain.Singleton.GetLastServiceBlock().TokenGenerationFee)
                return APIResultCodes.InvalidFeeAmount;

            return APIResultCodes.Success;
        }



    }
}
