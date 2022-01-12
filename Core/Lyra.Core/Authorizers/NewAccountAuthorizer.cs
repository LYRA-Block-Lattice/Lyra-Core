using System.Threading.Tasks;
using Lyra.Core.Blocks;
using Microsoft.Extensions.Options;
using Lyra.Core.Utils;
using Lyra.Core.Accounts;

namespace Lyra.Core.Authorizers
{
    public class NewAccountAuthorizer: ReceiveTransferAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.OpenAccountWithReceiveTransfer;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is OpenWithReceiveTransferBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as OpenWithReceiveTransferBlock;

            // 1. check if the account already exists
            if (await sys.Storage.AccountExistsAsync(block.AccountID))
                return APIResultCodes.AccountAlreadyExists;

            if (await sys.Storage.WasAccountImportedAsync(block.AccountID))
                return APIResultCodes.AccountAlreadyExists;

            // This is redundant but just in case
            if (await sys.Storage.FindLatestBlockAsync(block.AccountID) != null)
                return APIResultCodes.AccountBlockAlreadyExists;

            var result = await VerifyTransactionBlockAsync(sys, block);
            if (result != APIResultCodes.Success)
                return result;

            result = await ValidateReceiveTransAmountAsync(sys, block as ReceiveTransferBlock, block.GetBalanceChanges(null));
            if (result != APIResultCodes.Success)
                return result;

            result = await ValidateNonFungibleAsync(sys, block, null);
            if (result != APIResultCodes.Success)
                return result;

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "NewAccountAuthorizer->ReceiveTransferAuthorizer");
        }
    }
}
