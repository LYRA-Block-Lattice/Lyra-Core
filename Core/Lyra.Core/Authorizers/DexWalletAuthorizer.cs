using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class DexWalletAuthorizer : BrokerAccountAuthorizer
    {
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is IDexWallet))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as IDexWallet;

            if (string.IsNullOrWhiteSpace(block.IntSymbol))
                return APIResultCodes.InvalidExternalToken;

            if (string.IsNullOrWhiteSpace(block.ExtSymbol))
                return APIResultCodes.InvalidExternalToken;

            if (string.IsNullOrWhiteSpace(block.ExtProvider))
                return APIResultCodes.InvalidExternalToken;

            if (string.IsNullOrWhiteSpace(block.ExtAddress))
                return APIResultCodes.InvalidExternalToken;

            // TODO: verify against dex server

            return await base.AuthorizeImplAsync(sys, tblock);
        }

        protected override bool IsManagedBlockAllowed(DagSystem sys, TransactionBlock block)
        {
            return true;
        }
    }
}
