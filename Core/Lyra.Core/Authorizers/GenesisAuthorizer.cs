using Lyra.Core.Blocks;
using Lyra.Core.API;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Lyra.Core.Utils;
using Lyra.Core.Accounts;

namespace Lyra.Core.Authorizers
{
    public class GenesisAuthorizer: BaseAuthorizer
    {
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is LyraTokenGenesisBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as LyraTokenGenesisBlock;

            if ((block as LyraTokenGenesisBlock).Ticker != LyraGlobal.OFFICIALTICKERCODE)
                return APIResultCodes.InvalidBlockType;

            // Local node validations - before it sends it out to the authorization sample:
            // 1. check if the account already exists
            if (await sys.Storage.AccountExistsAsync(block.AccountID))
                return APIResultCodes.AccountAlreadyExists; // 

            // 2. Validate blocks
            var result = await VerifyBlockAsync(sys, block, null);
            if (result != APIResultCodes.Success)
                return result;

            // check if this token already exists
            //AccountData genesis_blocks = _accountCollection.GetAccount(AccountCollection.GENESIS_BLOCKS);
            //if (genesis_blocks.FindTokenGenesisBlock(testTokenGenesisBlock) != null)
            if (await sys.Storage.FindTokenGenesisBlockAsync(block.Hash, LyraGlobal.OFFICIALTICKERCODE) != null)
                return APIResultCodes.TokenGenesisBlockAlreadyExists;

            return await base.AuthorizeImplAsync(sys, tblock);
        }

        //protected override APIResultCodes ValidateFeeAsync(TransactionBlock block)
        //{
        //    if (block.FeeType != AuthorizationFeeTypes.NoFee)
        //        return APIResultCodes.InvalidFeeAmount;

        //    if (block.Fee != 0)
        //        return APIResultCodes.InvalidFeeAmount;

        //    return APIResultCodes.Success;
        //}



    }
}
