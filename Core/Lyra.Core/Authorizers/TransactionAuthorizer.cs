using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class TransactionAuthorizer : BaseAuthorizer
    {
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            var tx = tblock as TransactionBlock;
            if (tx == null)
                return APIResultCodes.InvalidBlockType;

            if (!Signatures.ValidateAccountId(tx.AccountID))
                return APIResultCodes.InvalidAccountId;

            if (tx.Balances == null)
                return APIResultCodes.InvalidBalance;

            if (tx.Balances.Values.Any(x => x < 0))
                return APIResultCodes.InvalidBalance;

            if (tx.Fee < 0)
                return APIResultCodes.InvalidFeeAmount;

            if (tx.FeeCode != LyraGlobal.OFFICIALTICKERCODE)
                return APIResultCodes.InvalidFeeTicker;

            if (tx.FeeType != GetFeeType())
                return APIResultCodes.InvalidFeeType;

            if (tx.FeeType == AuthorizationFeeTypes.NoFee && tx.Fee > 0)
                return APIResultCodes.InvalidFeeAmount;

            if (tx.FeeType == AuthorizationFeeTypes.Regular)
                if (tx.Fee != GetFeeAmount())
                    return APIResultCodes.InvalidFeeAmount;

            var vf = await ValidateFeeAsync(sys, tx);
            if (vf != APIResultCodes.Success)
                return vf;

            return await base.AuthorizeImplAsync(sys, tblock);
        }

        protected virtual decimal GetFeeAmount()
        {
            return 0;
        }
        protected virtual AuthorizationFeeTypes GetFeeType()
        {
            throw new NotImplementedException();
        }

        protected virtual Task<APIResultCodes> ValidateFeeAsync(DagSystem sys, TransactionBlock block)
        {
            return Task.FromResult(APIResultCodes.Success);
        }
    }
}
