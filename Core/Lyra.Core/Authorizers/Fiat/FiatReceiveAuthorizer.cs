using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers.Fiat
{
    public class FiatReceiveAuthorizer : BrokerAccountRecvAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.FiatRecvToken;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is FiatReceiveBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as FiatReceiveBlock;


            // IFiatWallet interface
            var brkauth = new FiatWalletAuthorizer();
            var brkret = await brkauth.AuthorizeAsync(sys, tblock);
            if (brkret == APIResultCodes.Success)
                return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "FiatReceiveAuthorizer->BrokerAccountRecvAuthorizer");
            else
                return brkret;
        }

        protected override bool IsManagedBlockAllowed(DagSystem sys, TransactionBlock block)
        {
            return true;
        }
    }
}
