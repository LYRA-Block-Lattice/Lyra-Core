﻿using Lyra.Core.API;
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
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.UnStaking;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is UnStakingBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as UnStakingBlock;

            if (!await sys.Storage.AccountExistsAsync(block.Voting))
                return APIResultCodes.AccountDoesNotExist;

            if (block.Days < 1 || block.Days > 36500)
                return APIResultCodes.InvalidTimeRange;

            var send = await sys.Storage.FindBlockByHashAsync(block.RelatedTx) as SendTransferBlock;
            if (send == null || send.DestinationAccountId != LyraGlobal.GUILDACCOUNTID)
                return APIResultCodes.InvalidMessengerAccount;

            if (block.DestinationAccountId != send.AccountID)
                return APIResultCodes.InvalidDestinationAccountId;

            var processed = await sys.Storage.FindBlocksByRelatedTxAsync(block.RelatedTx);
            if (processed.Any(a => a is UnStakingBlock))
                return APIResultCodes.InvalidUnstaking;

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "UnStakingAuthorizer->BrokerAccountSendAuthorizer");
        }

        protected override bool IsManagedBlockAllowed(DagSystem sys, TransactionBlock block)
        {
            return true;
        }

        protected override AuthorizationFeeTypes GetFeeType()
        {
            return AuthorizationFeeTypes.Dynamic;
        }
    }
}
