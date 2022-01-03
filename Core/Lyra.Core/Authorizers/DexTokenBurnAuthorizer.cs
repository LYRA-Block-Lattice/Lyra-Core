using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class DexTokenBurnAuthorizer : TransactionAuthorizer
    {
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is TokenBurnBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as TokenBurnBlock;

            if (string.IsNullOrWhiteSpace(block.BurnBy))
                return APIResultCodes.InvalidTokenBurn;
            if (string.IsNullOrWhiteSpace(block.GenesisHash))
                return APIResultCodes.InvalidTokenBurn;
            if (block.BurnAmount <= 0)
                return APIResultCodes.InvalidTokenBurn;

            // IDexWallet interface
            var brkauth = new DexWalletAuthorizer();
            var brkret = await brkauth.AuthorizeAsync(sys, tblock);
            if (brkret.Item1 == APIResultCodes.Success)
                return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "DexTokenBurnAuthorizer->TransactionAuthorizer");
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

    public class DexWithdrawAuthorizer : DexTokenBurnAuthorizer
    {
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is TokenWithdrawBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as TokenWithdrawBlock;

            if (string.IsNullOrWhiteSpace(block.WithdrawToExtAddress))
                return APIResultCodes.InvalidWithdrawToAddress;

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "DexWithdrawAuthorizer->DexTokenBurnAuthorizer");
        }
    }
}
