﻿using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class PoolFactoryAuthorizer : BaseAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.PoolFactory;
        }
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is PoolFactoryBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as PoolFactoryBlock;

            if (block.AccountID != PoolFactoryBlock.FactoryAccount)
                return APIResultCodes.InvalidAccountId;

            if (await sys.Storage.AccountExistsAsync(block.AccountID))
                return APIResultCodes.AccountAlreadyExists;

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "PoolFactoryAuthorizer->BaseAuthorizer");
        }
    }
}
