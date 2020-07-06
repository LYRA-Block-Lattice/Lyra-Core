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
        public GenesisAuthorizer()
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
            if (!(tblock is LyraTokenGenesisBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as LyraTokenGenesisBlock;

            if ((block as LyraTokenGenesisBlock).Ticker != LyraGlobal.OFFICIALTICKERCODE)
                return APIResultCodes.InvalidBlockType;

            // Local node validations - before it sends it out to the authorization sample:
            // 1. check if the account already exists
                if (await DagSystem.Singleton.Storage.AccountExistsAsync(block.AccountID))
                return APIResultCodes.AccountAlreadyExists; // 

            // 2. Validate blocks
            var result = await VerifyBlockAsync(block, null);
            if (result != APIResultCodes.Success)
                return result;

            // check if this token already exists
            //AccountData genesis_blocks = _accountCollection.GetAccount(AccountCollection.GENESIS_BLOCKS);
            //if (genesis_blocks.FindTokenGenesisBlock(testTokenGenesisBlock) != null)
            if (await DagSystem.Singleton.Storage.FindTokenGenesisBlockAsync(block.Hash, LyraGlobal.OFFICIALTICKERCODE) != null)
                return APIResultCodes.TokenGenesisBlockAlreadyExists;

            return APIResultCodes.Success;
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
