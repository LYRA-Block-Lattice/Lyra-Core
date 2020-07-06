using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class ServiceAuthorizer : BaseAuthorizer
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
            if (!(tblock is ServiceBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as ServiceBlock;

            //// 1. check if the block already exists
            //if (null != await DagSystem.Singleton.Storage.GetBlockByUIndexAsync(block.UIndex))
            //    return APIResultCodes.BlockWithThisIndexAlreadyExists;

            // service specifice feature
            //block.

            var prevBlock = await sys.Storage.FindBlockByHashAsync(block.PreviousHash);

            var result = await VerifyBlockAsync(sys, block, prevBlock);
            if (result != APIResultCodes.Success)
                return result;

            return APIResultCodes.Success;
        }
    }
}
