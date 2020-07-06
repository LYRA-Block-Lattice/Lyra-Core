using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class NullTransactionAuthorizer : BaseAuthorizer
    {
        public override async Task<(APIResultCodes, AuthorizationSignature)> AuthorizeAsync<T>(DagSystem sys, T tblock)
        {
            var result = await AuthorizeImplAsync(sys, tblock);
            if (APIResultCodes.Success == result)
                return (APIResultCodes.Success, Sign(sys, tblock));
            else
                return (result, (AuthorizationSignature)null);
        }

        protected override Task<APIResultCodes> ValidateFeeAsync(DagSystem sys, TransactionBlock block)
        {
            return Task.FromResult(APIResultCodes.Success);
        }

        private async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is NullTransactionBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as NullTransactionBlock;

            //// 1. check if the block already exists
            //if (null != await sys.Storage.GetBlockByUIndexAsync(block.UIndex))
            //    return APIResultCodes.BlockWithThisIndexAlreadyExists;

            var lastCons = await sys.Storage.GetLastConsolidationBlockAsync();
            if (lastCons == null)
                return APIResultCodes.CouldNotFindLatestBlock;

            if (block.PreviousConsolidateHash != lastCons.Hash)
                return APIResultCodes.PreviousBlockNotFound;

            var result = await VerifyBlockAsync(sys, block, null);
            if (result != APIResultCodes.Success)
                return result;

            return APIResultCodes.Success;
        }
    }
}
