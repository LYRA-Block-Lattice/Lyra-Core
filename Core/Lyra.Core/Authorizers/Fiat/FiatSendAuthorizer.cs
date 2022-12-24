using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers.Fiat
{
    public class FiatSendAuthorizer : BrokerAccountSendAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.FiatSendToken;

        }
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is FiatSendBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as FiatSendBlock;


            // IFiatWallet interface
            var brkauth = new FiatWalletAuthorizer();
            var brkret = await brkauth.AuthorizeAsync(sys, tblock);
            if (brkret == APIResultCodes.Success)
                return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "FiatSendAuthorizer->BrokerAccountSendAuthorizer");
            else
                return brkret;
        }

        protected override bool IsManagedBlockAllowed(DagSystem sys, TransactionBlock block)
        {
            return true;
        }
    }
}
