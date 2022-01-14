using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.Blocks;
using Neo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Lyra.Core.WorkFlow
{
    public class CreateOTCOrderWorkflow : DebiWorkflow, IWorkflowExt
    {
        public string Id => BrokerActions.BRK_OTC_CRODR;
        public override BrokerRecvType RecvVia => BrokerRecvType.DaoRecv;

        public int Version => 1;
    }

    public class CreateOTCTradeWorkflow : DebiWorkflow, IWorkflowExt
    {
        public string Id => BrokerActions.BRK_OTC_CRTRD;
        public override BrokerRecvType RecvVia => BrokerRecvType.DaoRecv;

        public int Version => 1;
    }

    public class OTCTradePaymentSentWorkflow : DebiWorkflow, IWorkflowExt
    {
        public string Id => BrokerActions.BRK_OTC_TRDPAYSENT;
        public override BrokerRecvType RecvVia => BrokerRecvType.None;

        public int Version => 1;
    }

    public class OTCTradePaymentGotWorkflow : DebiWorkflow, IWorkflowExt
    {
        public string Id => BrokerActions.BRK_OTC_TRDPAYGOT;
        public override BrokerRecvType RecvVia => BrokerRecvType.None;

        public int Version => 1;
    }
}
