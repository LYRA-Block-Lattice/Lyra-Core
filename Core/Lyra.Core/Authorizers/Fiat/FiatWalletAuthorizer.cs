using Lyra.Core.Blocks;
using Lyra.Data.API;
using Lyra.Data.Crypto;
using Neo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers.Fiat
{
    public class FiatWalletAuthorizer : BrokerAccountAuthorizer
    {
        public override async Task<APIResultCodes> AuthorizeAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is IFiatWallet))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as IFiatWallet;

            //if (string.IsNullOrWhiteSpace(block.IntSymbol))
            //    return APIResultCodes.InvaliFiatternalToken;

            //if (string.IsNullOrWhiteSpace(block.ExtSymbol))
            //    return APIResultCodes.InvaliFiatternalToken;

            //if (string.IsNullOrWhiteSpace(block.ExtProvider))
            //    return APIResultCodes.InvaliFiatternalToken;

            //if (string.IsNullOrWhiteSpace(block.ExtAddress))
            //    return APIResultCodes.InvaliFiatternalToken;

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeAsync(sys, tblock), "FiatWalletAuthorizer->BrokerAccountAuthorizer");
        }
    }
}
