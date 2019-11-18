using System;
using System.Collections.Generic;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;

using Lyra.Core.Accounts.Node;
using Lyra.Core.API;
using Lyra.Node2.Services;
using Lyra.Core.Protos;

namespace Lyra.Node2.Authorizers
{
    public class GenesisAuthorizer: BaseAuthorizer
    {
        public GenesisAuthorizer(ServiceAccount serviceAccount, IAccountCollection accountCollection): base (serviceAccount, accountCollection)
        {
        }

        public override APIResultCodes Authorize<T>(ref T tblock)
        {

            if (!(tblock is LyraTokenGenesisBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as LyraTokenGenesisBlock;

            if ((block as LyraTokenGenesisBlock).Ticker != TokenGenesisBlock.LYRA_TICKER_CODE)
                return APIResultCodes.InvalidBlockType;

            // Local node validations - before it sends it out to the authorization sample:
            // 1. check if the account already exists
                if (_accountCollection.AccountExists(block.AccountID))
                return APIResultCodes.AccountAlreadyExists; // 

            // 2. Validate blocks
            var result = VerifyBlock(block, null);
            if (result != APIResultCodes.Success)
                return result;

            // check if this token already exists
            //AccountData genesis_blocks = _accountCollection.GetAccount(AccountCollection.GENESIS_BLOCKS);
            //if (genesis_blocks.FindTokenGenesisBlock(testTokenGenesisBlock) != null)
            if (_accountCollection.FindTokenGenesisBlock(block.Hash, TokenGenesisBlock.LYRA_TICKER_CODE) != null)
                return APIResultCodes.TokenGenesisBlockAlreadyExists;

            // sign with the authorizer key
            Sign(ref block);

            _accountCollection.AddBlock(block);

            return APIResultCodes.Success;
        }

        protected override APIResultCodes ValidateFee(TransactionBlock block)
        {
            if (block.FeeType != AuthorizationFeeTypes.NoFee)
                return APIResultCodes.InvalidFeeAmount;

            if (block.Fee != 0)
                return APIResultCodes.InvalidFeeAmount;

            return APIResultCodes.Success;
        }



    }
}
