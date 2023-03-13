using Lyra.Core.Blocks;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class VOTGenesisAuthorizer : BrokerAccountRecvAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.VoteGenesis;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is VotingGenesisBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as VotingGenesisBlock;

            if (block.AccountType != AccountTypes.Voting)
            {
                return APIResultCodes.InvalidAccountType;
            }

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "VOTGenesisAuthorizer->BrokerAccountRecvAuthorizer");
        }
    }

    public class VOTVoteAuthorizer : BrokerAccountRecvAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.Voting;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is VotingBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as VotingBlock;

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "VOTVoteAuthorizer->BrokerAccountRecvAuthorizer");
        }
    }

    public class VOTRefundAuthorizer : BrokerAccountSendAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.VotingRefund;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is VotingRefundBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as VotingRefundBlock;

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "VOTRefundAuthorizer->BrokerAccountRecvAuthorizer");
        }
    }
}
