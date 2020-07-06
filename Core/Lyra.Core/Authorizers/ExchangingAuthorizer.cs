using Lyra.Core.Blocks;
using Microsoft.Extensions.Options;
using Lyra.Core.Cryptography;
using Lyra.Core.Utils;
using Lyra.Core.Accounts;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class ExchangingAuthorizer : SendTransferAuthorizer
    {
        public ExchangingAuthorizer()
        {

        }

        protected override Task<APIResultCodes> ValidateFeeAsync(DagSystem sys, TransactionBlock block)
        {
            APIResultCodes result;
            if (block.FeeType != AuthorizationFeeTypes.Regular)
                result = APIResultCodes.InvalidFeeAmount;

            if (block.Fee != ExchangingBlock.FEE)
                result = APIResultCodes.InvalidFeeAmount;

            result = APIResultCodes.Success;

            return Task.FromResult(result);
        }
    }
}
