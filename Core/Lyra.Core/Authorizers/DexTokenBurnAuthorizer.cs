﻿using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class DexTokenBurnAuthorizer : TransactionAuthorizer
    {
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is TokenBurnBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as TokenBurnBlock;

            return await base.AuthorizeImplAsync(sys, tblock);
        }
        protected override AuthorizationFeeTypes GetFeeType()
        {
            return AuthorizationFeeTypes.NoFee;
        }

        protected override bool IsManagedBlockAllowed(DagSystem sys, TransactionBlock block)
        {
            return true;
        }
    }
}
