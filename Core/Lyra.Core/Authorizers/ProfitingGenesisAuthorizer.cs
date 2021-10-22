using Lyra.Core.Blocks;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class ProfitingGenesisAuthorizer : ProfitingAuthorizer
    {
        public override async Task<(APIResultCodes, AuthorizationSignature)> AuthorizeAsync<T>(DagSystem sys, T tblock)
        {
            var result0 = base.AuthorizeAsync(sys, tblock);
            var result = await AuthorizeImplAsync(sys, tblock);

            if (APIResultCodes.Success == result && result0.IsCompletedSuccessfully)
                return (APIResultCodes.Success, Sign(sys, tblock));
            else
                return (result, (AuthorizationSignature)null);
        }
        private async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is ProfitingGenesis))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as ProfitingGenesis;

            if (block.AccountType != AccountTypes.Profiting)
                return APIResultCodes.InvalidBlockType;

            return APIResultCodes.Success;
        }
    }
}
