using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class UnStakingAuthorizer : BrokerAccountSendAuthorizer
    {
        public override async Task<(APIResultCodes, AuthorizationSignature)> AuthorizeAsync<T>(DagSystem sys, T tblock)
        {
            var br = await base.AuthorizeAsync(sys, tblock);
            APIResultCodes result;
            if (br.Item1 == APIResultCodes.Success)
                result = await AuthorizeImplAsync(sys, tblock);
            else
                result = br.Item1;

            if (APIResultCodes.Success == result)
                return (APIResultCodes.Success, Sign(sys, tblock));
            else
                return (result, (AuthorizationSignature)null);
        }
        private async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is UnStakingBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as UnStakingBlock;

            if (!await sys.Storage.AccountExistsAsync(block.Voting))
                return APIResultCodes.AccountDoesNotExist;

            if (block.Days < 1 || block.Days > 36500)
                return APIResultCodes.InvalidTimeRange;

            return APIResultCodes.Success;
        }
    }
}
