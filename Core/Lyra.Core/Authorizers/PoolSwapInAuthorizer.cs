using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class PoolSwapInAuthorizer : ReceiveTransferAuthorizer
    {
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is PoolSwapInBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as PoolSwapInBlock;

            if (block.SourceHash != block.RelatedTx)
                return APIResultCodes.InvalidRelatedTx;

            return await MeasureAuthAsync("PoolSwapInAuthorizer", "ReceiveTransferAuthorizer", base.AuthorizeImplAsync(sys, tblock));
        }
    }
}
