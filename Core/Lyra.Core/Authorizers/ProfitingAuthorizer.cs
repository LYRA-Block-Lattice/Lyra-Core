﻿using Lyra.Core.Blocks;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class ProfitingAuthorizer : BrokerAccountRecvAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.Profiting;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is ProfitingBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as ProfitingBlock;

            if (block.ShareRito < 0 || block.ShareRito > 1)
                return APIResultCodes.InvalidShareRitio;

            if (block.Seats < 0 || block.Seats > 100)
                return APIResultCodes.InvalidSeatsCount;

            if (block.ShareRito == 0 && block.Seats != 0)
                return APIResultCodes.InvalidSeatsCount;

            if(block.ShareRito > 0 && block.Seats == 0)
                return APIResultCodes.InvalidSeatsCount;

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "ProfitingAuthorizer->BrokerAccountRecvAuthorizer");
        }
    }
}
