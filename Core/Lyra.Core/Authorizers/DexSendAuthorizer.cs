using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class DexSendAuthorizer : BrokerAccountSendAuthorizer
    {
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is DexSendBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as DexSendBlock;


            // IDexWallet interface
            var brkauth = new DexWalletAuthorizer();
            var brkret = await brkauth.AuthorizeAsync(sys, tblock);
            if (brkret.Item1 == APIResultCodes.Success)
                return await MeasureAuthAsync(this.GetType().Name, base.GetType().Name, base.AuthorizeImplAsync(sys, tblock));
            else
                return brkret.Item1;
        }

        protected override bool IsManagedBlockAllowed(DagSystem sys, TransactionBlock block)
        {
            return true;
        }
    }
}
