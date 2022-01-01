using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using Lyra.Data.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class DexTokenMintAuthorizer : TransactionAuthorizer
    {
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is TokenMintBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as TokenMintBlock;

            if (string.IsNullOrWhiteSpace(block.MintBy))
                return APIResultCodes.InvalidTokenMint;
            if (string.IsNullOrWhiteSpace(block.GenesisHash))
                return APIResultCodes.InvalidTokenMint;
            if (block.MintAmount <= 0)
                return APIResultCodes.InvalidTokenMint;

            if (block.MintBy != LyraGlobal.GetDexServerAccountID(LyraNodeConfig.GetNetworkId()))
                return APIResultCodes.InvalidDexServer;

            // IDexWallet interface
            var brkauth = new DexWalletAuthorizer();
            var brkret = await brkauth.AuthorizeAsync(sys, tblock);
            if (brkret.Item1 == APIResultCodes.Success)
                return await MeasureAuthAsync("DexTokenMintAuthorizer", "TransactionAuthorizer", base.AuthorizeImplAsync(sys, tblock));
            else
                return brkret.Item1;
        }

        protected override AuthorizationFeeTypes GetFeeType()
        {
            return AuthorizationFeeTypes.NoFee;
        }

        protected override bool IsManagedBlockAllowed(DagSystem sys, TransactionBlock block)
        {
            return true;
        }
    }
}
