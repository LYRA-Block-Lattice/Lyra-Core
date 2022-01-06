using Lyra.Core.Authorizers;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize.WorkFlow.OTC;
using Lyra.Data.API;
using Lyra.Data.Blocks;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize.WorkFlow
{
    [LyraWorkFlow]
    public class WFOtcCreate : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_OTC_CRODR,
                RecvVia = BrokerRecvType.DaoRecv,
                Blocks = new[]{
                    new BlockDesc
                    {
                        BlockType = BlockTypes.OTCOrderGenesis,
                        TheBlock = typeof(OtcGenesis),
                        AuthorizerType = typeof(OtcGenesisAuthorizer),
                    }
                }
            };
        }

        public override Task<TransactionBlock> BrokerOpsAsync(DagSystem sys, SendTransferBlock send)
        {
            throw new NotImplementedException();
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock send, TransactionBlock last)
        {
            var order = JsonConvert.DeserializeObject<OTCOrder>(send.Tags["data"]);
            var dao = await sys.Storage.FindFirstBlockAsync(order.daoid) as DaoGenesis;
            if (dao == null)
                return APIResultCodes.InvalidOrgnization;

            return APIResultCodes.Success;
        }
    }
}
