using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lyra.Core.Accounts.Node;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;
using Lyra.Core.Decentralize;
using Microsoft.Extensions.Options;
using Lyra.Core.Cryptography;
using Lyra.Core.Utils;
using Lyra.Core.Services;

namespace Lyra.Core.Authorizers
{
    public class ExchangingAuthorizer : SendTransferAuthorizer
    {
        public ExchangingAuthorizer(IOptions<LyraNodeConfig> config, ServiceAccount serviceAccount, IAccountCollection accountCollection)
            : base(config, serviceAccount, accountCollection)
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
