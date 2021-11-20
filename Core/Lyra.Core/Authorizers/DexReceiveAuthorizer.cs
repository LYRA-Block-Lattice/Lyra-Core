﻿using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class DexReceiveAuthorizer : BrokerAccountRecvAuthorizer
    {
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is DexReceiveBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as DexReceiveBlock;


            // IDexWallet interface
            var brkauth = new DexWalletAuthorizer();
            var brkret = await brkauth.AuthorizeAsync(sys, tblock);
            if (brkret.Item1 == APIResultCodes.Success)
                return await base.AuthorizeImplAsync(sys, tblock);
            else
                return brkret.Item1;
        }

        protected override bool IsManagedBlockAllowed(DagSystem sys, TransactionBlock block)
        {
            return true;
        }
    }
}
