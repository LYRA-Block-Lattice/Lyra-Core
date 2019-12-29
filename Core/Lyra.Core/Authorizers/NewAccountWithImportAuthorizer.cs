using Lyra.Core.Blocks.Transactions;
using Lyra.Core.API;
using Lyra.Core.Accounts.Node;
using Lyra.Core.Decentralize;
using System.Threading.Tasks;
using Lyra.Core.Blocks;
using Microsoft.Extensions.Options;
using Lyra.Core.Cryptography;
using Lyra.Core.Utils;
using Lyra.Core.Services;

namespace Lyra.Core.Authorizers
{
    public class NewAccountWithImportAuthorizer : ReceiveTransferAuthorizer
    {
        public NewAccountWithImportAuthorizer(IOptions<LyraNodeConfig> config, ServiceAccount serviceAccount, IAccountCollection accountCollection)
            : base(config, serviceAccount, accountCollection)
        {
        }

        public override Task<(APIResultCodes, AuthorizationSignature)> Authorize<T>(T tblock)
        {
            var result = AuthorizeImpl(tblock);
            if (APIResultCodes.Success == result)
                return Task.FromResult((APIResultCodes.Success, Sign(tblock)));
            else
                return Task.FromResult((result, (AuthorizationSignature)null));
        }
        private APIResultCodes AuthorizeImpl<T>(T tblock)
        {
            if (!(tblock is OpenWithReceiveTransferBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as OpenWithReceiveTransferBlock;

            // 1. check if the account already exists
            if (_accountCollection.AccountExists(block.AccountID))
                return APIResultCodes.AccountAlreadyExists;

            // This is redundant but just in case
            if (_accountCollection.FindLatestBlock(block.AccountID) != null)
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
