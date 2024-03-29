﻿using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class PoolSwapInAuthorizer : ReceiveTransferAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.PoolSwapIn;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is PoolSwapInBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as PoolSwapInBlock;

            if (block.SourceHash != block.RelatedTx)
                return APIResultCodes.InvalidRelatedTx;

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "PoolSwapInAuthorizer->ReceiveTransferAuthorizer");
        }
    }

    public class PoolRefundRecvAuthorizer : ReceiveTransferAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.PoolRefundRecv;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is PoolRefundReceiveBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as PoolRefundReceiveBlock;

            if (block.SourceHash != block.RelatedTx)
                return APIResultCodes.InvalidRelatedTx;

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "PoolSwapInAuthorizer->ReceiveTransferAuthorizer");
        }
    }

    public class PoolRefundSendAuthorizer : SendTransferAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.PoolRefundSend;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is PoolRefundSendBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as PoolRefundSendBlock;

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "PoolSwapInAuthorizer->ReceiveTransferAuthorizer");
        }
    }
}
