using Lyra.Core.Blocks;
using Microsoft.Extensions.Options;
using Lyra.Core.Cryptography;
using Lyra.Core.Utils;
using Lyra.Core.Accounts;

namespace Lyra.Core.Authorizers
{
    public class ExchangingAuthorizer : SendTransferAuthorizer
    {
        public ExchangingAuthorizer()
        {

        }

        protected override APIResultCodes ValidateFee(TransactionBlock block)
        {
            if (block.FeeType != AuthorizationFeeTypes.Regular)
                return APIResultCodes.InvalidFeeAmount;

            if (block.Fee != ExchangingBlock.FEE)
                return APIResultCodes.InvalidFeeAmount;

            return APIResultCodes.Success;
        }
    }
}
