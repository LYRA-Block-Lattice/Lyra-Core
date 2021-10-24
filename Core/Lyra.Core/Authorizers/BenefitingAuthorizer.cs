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
    public class BenefitingAuthorizer : BrokerAccountSendAuthorizer
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
            if (!(tblock is BenefitingBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as BenefitingBlock;

            if (block.ShareRito < 0 || block.ShareRito > 1)
                return APIResultCodes.InvalidShareRitio;

            if (block.Seats < 1 || block.Seats > 100)
                return APIResultCodes.InvalidSeatsCount;

            return APIResultCodes.Success;
        }
    }
}
