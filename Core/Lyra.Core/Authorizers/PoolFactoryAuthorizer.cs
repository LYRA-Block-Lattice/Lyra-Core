using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class PoolFactoryAuthorizer : BaseAuthorizer
    {
        public override async Task<(APIResultCodes, AuthorizationSignature)> AuthorizeAsync<T>(DagSystem sys, T tblock)
        {
            var result = await AuthorizeImplAsync(sys, tblock);

            if (result == APIResultCodes.Success)
                result = (await base.AuthorizeAsync(sys, tblock)).Item1;

            if (APIResultCodes.Success == result)
                return (APIResultCodes.Success, Sign(sys, tblock));
            else
                return (result, (AuthorizationSignature)null);
        }
        private async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is PoolFactoryBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as PoolFactoryBlock;

            // Validate blocks
            var result = await VerifyBlockAsync(sys, block, null);
            if (result != APIResultCodes.Success)
                return result;

            if (await sys.Storage.AccountExistsAsync(block.AccountID))
                return APIResultCodes.AccountAlreadyExists;

            return APIResultCodes.Success;
        }
    }
}
