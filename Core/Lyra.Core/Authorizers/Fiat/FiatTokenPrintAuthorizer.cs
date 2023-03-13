using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using Lyra.Data.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers.Fiat
{
    public class FiatTokenPrintAuthorizer : TransactionAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.FiatTokenPrint;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is FiatPrintBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as FiatPrintBlock;

            if (string.IsNullOrWhiteSpace(block.GenesisHash))
                return APIResultCodes.InvalidTokenMint;
            if (block.MintAmount <= 0)
                return APIResultCodes.InvalidTokenMint;

             // IFiatWallet interface
            var brkauth = new FiatWalletAuthorizer();
            var brkret = await brkauth.AuthorizeAsync(sys, tblock);
            if (brkret == APIResultCodes.Success)
                return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "FiatTokenMintAuthorizer->TransactionAuthorizer");
            else
                return brkret;
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
