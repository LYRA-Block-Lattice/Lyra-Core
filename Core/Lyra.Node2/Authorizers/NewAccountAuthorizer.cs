using Lyra.Core.Blocks.Transactions;

using Lyra.Core.API;
using Lyra.Core.Accounts.Node;
using Lyra.Node2.Services;
using Lyra.Core.Protos;

namespace Lyra.Node2.Authorizers
{
    public class NewAccountAuthorizer: ReceiveTransferAuthorizer
    {
        public NewAccountAuthorizer(ServiceAccount serviceAccount, IAccountCollection accountCollection): base (serviceAccount, accountCollection)
        {
        }

        public override APIResultCodes Authorize<T>(ref T tblock)
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

            Sign(ref block);

            _accountCollection.AddBlock(block);

            return base.Authorize(ref tblock);
        }
    }
}
