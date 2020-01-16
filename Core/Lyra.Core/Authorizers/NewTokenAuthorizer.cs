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
            if (!(tblock is TokenGenesisBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as TokenGenesisBlock;

            // Local node validations - before it sends it out to the authorization sample:
            // 1. check if the account already exists
            if (!await BlockChain.Singleton.AccountExistsAsync(block.AccountID))
                return APIResultCodes.AccountDoesNotExist; // 

            TransactionBlock lastBlock = await BlockChain.Singleton.FindLatestBlockAsync(block.AccountID);
            if (lastBlock == null)
                return APIResultCodes.CouldNotFindLatestBlock;

            // 2. Validate blocks
            var result = await VerifyBlockAsync(block, lastBlock);
            if (result != APIResultCodes.Success)
                return result;

            result = await VerifyTransactionBlockAsync(block);
            if (result != APIResultCodes.Success)
                return result;

            // check LYR balance
            if (lastBlock.Balances[LyraGlobal.LYRATICKERCODE] != block.Balances[LyraGlobal.LYRATICKERCODE] + block.Fee)
                return APIResultCodes.InvalidNewAccountBalance;

            // check if this token already exists
            //AccountData genesis_blocks = _accountCollection.GetAccount(AccountCollection.GENESIS_BLOCKS);
            //if (genesis_blocks.FindTokenGenesisBlock(testTokenGenesisBlock) != null)
            if (BlockChain.Singleton.FindTokenGenesisBlockAsync(block.Hash, block.Ticker) != null)
                return APIResultCodes.TokenGenesisBlockAlreadyExists;

            if (block.Fee != (await BlockChain.Singleton.GetLastServiceBlockAsync()).TokenGenerationFee)
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

        protected override async Task<APIResultCodes> ValidateFeeAsync(TransactionBlock block)
        {
            if (block.FeeType != AuthorizationFeeTypes.Regular)
                return APIResultCodes.InvalidFeeAmount;

            if (block.Fee != (await BlockChain.Singleton.GetLastServiceBlockAsync()).TokenGenerationFee)
                return APIResultCodes.InvalidFeeAmount;

            return APIResultCodes.Success;
        }



    }
}
