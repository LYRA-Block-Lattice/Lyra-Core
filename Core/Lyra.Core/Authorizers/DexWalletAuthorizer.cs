using Lyra.Core.Blocks;
using Lyra.Data.API;
using Lyra.Data.Crypto;
using Neo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class DexWalletAuthorizer : BrokerAccountAuthorizer
    {
        public override async Task<APIResultCodes> AuthorizeAsync<T>(DagSystem sys, T tblock)
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

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeAsync(sys, tblock), "DexWalletAuthorizer->BrokerAccountAuthorizer");
        }
    }
}
