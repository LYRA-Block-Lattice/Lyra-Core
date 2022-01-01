using Lyra.Core.Blocks;
using Lyra.Data.API;
using Lyra.Data.Crypto;
using Lyra.Data.Utils;
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

            if (block.AccountType != AccountTypes.DEX)
                return APIResultCodes.InvalidAccountType;

            var dc = new DexClient(LyraNodeConfig.GetNetworkId());
            var asts = await dc.GetSupportedExtTokenAsync(LyraNodeConfig.GetNetworkId());

            var ast = asts.Asserts.Where(a => a.Symbol == block.ExtSymbol)
                .FirstOrDefault();
            if (ast == null || ast.NetworkProvider != block.ExtProvider)
                return APIResultCodes.UnsupportedDexToken;

            return await MeasureAuthAsync("DexWalletGenesisAuthorizer", "DexReceiveAuthorizer", base.AuthorizeImplAsync(sys, tblock));
        }

        protected override bool IsManagedBlockAllowed(DagSystem sys, TransactionBlock block)
        {
            return true;
        }
    }
}
