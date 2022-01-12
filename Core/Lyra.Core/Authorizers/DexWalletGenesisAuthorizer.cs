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
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.DexWalletGenesis;
        }

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

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "DexWalletGenesisAuthorizer->DexReceiveAuthorizer");
        }

        protected override bool IsManagedBlockAllowed(DagSystem sys, TransactionBlock block)
        {
            return true;
        }
    }
}
