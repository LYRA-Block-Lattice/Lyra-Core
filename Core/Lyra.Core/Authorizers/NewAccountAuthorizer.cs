using System.Threading.Tasks;
using Lyra.Core.Blocks;
using Microsoft.Extensions.Options;
using Lyra.Core.Cryptography;
using Lyra.Core.Utils;
using Lyra.Core.Accounts;

namespace Lyra.Core.Authorizers
{
    public class NewAccountAuthorizer: ReceiveTransferAuthorizer
    {
        public NewAccountAuthorizer()
        {
        }

        public override (APIResultCodes, AuthorizationSignature) Authorize<T>(T tblock, bool WithSign = true)
        {
            var result = AuthorizeImpl(tblock);
            if (APIResultCodes.Success == result)
                return (APIResultCodes.Success, Sign(tblock));
            else
                return (result, (AuthorizationSignature)null);
        }
        private APIResultCodes AuthorizeImpl<T>(T tblock)
        {
            if (!(tblock is OpenWithReceiveTransferBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as OpenWithReceiveTransferBlock;

            // 1. check if the account already exists
            if (BlockChain.Singleton.AccountExists(block.AccountID))
                return APIResultCodes.AccountAlreadyExists;

            // This is redundant but just in case
            if (BlockChain.Singleton.FindLatestBlock(block.AccountID) != null)
                return APIResultCodes.AccountBlockAlreadyExists;

            var result = VerifyBlock(block, null);
            if (result != APIResultCodes.Success)
                return result;

            result = VerifyTransactionBlock(block);
            if (result != APIResultCodes.Success)
                return result;

            result = ValidateReceiveTransAmount(block as ReceiveTransferBlock, block.GetTransaction(null));
            if (result != APIResultCodes.Success)
                return result;

            result = ValidateNonFungible(block, null);
            if (result != APIResultCodes.Success)
                return result;

            return APIResultCodes.Success;
        }
    }
}
