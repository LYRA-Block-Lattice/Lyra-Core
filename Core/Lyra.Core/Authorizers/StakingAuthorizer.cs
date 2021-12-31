using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class StakingAuthorizer : BrokerAccountRecvAuthorizer
    {
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is StakingBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as StakingBlock;

            if (!await sys.Storage.AccountExistsAsync(block.Voting))
                return APIResultCodes.AccountDoesNotExist;

            if (block.Days < 1 || block.Days > 36500)
                return APIResultCodes.InvalidTimeRange;

            return await MeasureAuthAsync(this.GetType().Name, base.GetType().Name, base.AuthorizeImplAsync(sys, tblock));
        }

        protected override bool IsManagedBlockAllowed(DagSystem sys, TransactionBlock block)
        {
            return true;
        }
    }
}
