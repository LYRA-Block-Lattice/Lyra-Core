﻿using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class DexWalletGenesisAuthorizer : DexReceiveAuthorizer
    {
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is DexWalletGenesis))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as DexWalletGenesis;


            return await base.AuthorizeImplAsync(sys, tblock);
        }

        protected override bool IsManagedBlockAllowed(DagSystem sys, TransactionBlock block)
        {
            return true;
        }
    }
}
