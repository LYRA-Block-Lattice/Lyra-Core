using Lyra.Core.Blocks;
using Lyra.Data.API;
using Lyra.Data.Crypto;
using Lyra.Data.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers.Fiat
{
    public class FiatWalletGenesisAuthorizer : FiatReceiveAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.FiatWalletGenesis;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is FiatWalletGenesis))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as FiatWalletGenesis;

            if (block.AccountType != AccountTypes.Fiat)
                return APIResultCodes.InvalidAccountType;

            //var dc = new FiatClient(LyraNodeConfig.GetNetworkId());
            //var asts = await dc.GetSupporteFiattTokenAsync(LyraNodeConfig.GetNetworkId());

            //var ast = asts.Asserts.Where(a => a.Symbol == block.ExtSymbol)
            //    .FirstOrDefault();
            //if (ast == null || ast.NetworkProvider != block.ExtProvider)
            //    return APIResultCodes.UnsupportedFiatToken;

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "FiatWalletGenesisAuthorizer->FiatReceiveAuthorizer");
        }

        protected override bool IsManagedBlockAllowed(DagSystem sys, TransactionBlock block)
        {
            return true;
        }
    }
}
