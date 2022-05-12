using Lyra.Core.Blocks;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using Lyra.Data.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class DealerRecvAuthorizer : BrokerAccountRecvAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.DealerRecv;
        }

        protected override AuthorizationFeeTypes GetFeeType()
        {
            return AuthorizationFeeTypes.Dynamic;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is DealerRecvBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as DealerRecvBlock;



            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "DealerRecvAuthorizer->BrokerAccountRecvAuthorizer");
        }
    }

    public class DealerSendAuthorizer : BrokerAccountSendAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.DealerSend;
        }

        protected override AuthorizationFeeTypes GetFeeType()
        {
            return AuthorizationFeeTypes.Dynamic;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is DealerSendBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as DealerSendBlock;



            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "DealerSendAuthorizer->BrokerAccountRecvAuthorizer");
        }
    }

    public class DealerGenesisAuthorizer : DealerRecvAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.DealerGenesis;
        }

        //static int count = 0;

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            //if(count == 0)
            //{
            //    await Task.Delay(16000);
            //    count++;
            //}                

            if (!(tblock is DealerGenesisBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as DealerGenesisBlock;

            if(block.AccountType != AccountTypes.Server)
            {
                return APIResultCodes.InvalidAccountType;
            }

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "DealerGenesisAuthorizer->DealerRecvAuthorizer");
        }
    }
}
