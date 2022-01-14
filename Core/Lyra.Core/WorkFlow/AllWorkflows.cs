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
    #region crypto OTC
    public class CreateDaoWorkflow : DebiWorkflow, IWorkflowExt
    {
        public string Id => BrokerActions.BRK_DAO_CRDAO;
        public override BrokerRecvType RecvVia => BrokerRecvType.PFRecv;

        public int Version => 1;
    }

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

    public class OTCOrderCloseWorkflow : DebiWorkflow, IWorkflowExt
    {
        public string Id => BrokerActions.BRK_OTC_ORDCLOSE;
        public override BrokerRecvType RecvVia => BrokerRecvType.DaoRecv;

        public int Version => 1;
    }
    #endregion

    #region Pool
    public class PoolCreateWorkflow : DebiWorkflow, IWorkflowExt
    {
        public string Id => BrokerActions.BRK_POOL_CRPL;
        public override BrokerRecvType RecvVia => BrokerRecvType.PFRecv;

        public int Version => 1;
    }
    public class PoolAddLiquidateWorkflow : DebiWorkflow, IWorkflowExt
    {
        public string Id => BrokerActions.BRK_POOL_ADDLQ;
        public override BrokerRecvType RecvVia => BrokerRecvType.None;

        public int Version => 1;
    }

    public class PoolRemoveLiquidateWorkflow : DebiWorkflow, IWorkflowExt
    {
        public string Id => BrokerActions.BRK_POOL_RMLQ;
        public override BrokerRecvType RecvVia => BrokerRecvType.PFRecv;

        public int Version => 1;
    }
    public class PoolSwapWorkflow : DebiWorkflow, IWorkflowExt
    {
        public string Id => BrokerActions.BRK_POOL_SWAP;
        public override BrokerRecvType RecvVia => BrokerRecvType.None;

        public int Version => 1;
    }
    #endregion

    #region Profiting
    public class CreateProfitingWorkflow : DebiWorkflow, IWorkflowExt
    {
        public string Id => BrokerActions.BRK_PFT_CRPFT;
        public override BrokerRecvType RecvVia => BrokerRecvType.PFRecv;

        public int Version => 1;
    }
    public class GetProfitWorkflow : DebiWorkflow, IWorkflowExt
    {
        public string Id => BrokerActions.BRK_PFT_GETPFT;
        public override BrokerRecvType RecvVia => BrokerRecvType.PFRecv;

        public int Version => 1;
    }
    #endregion

    #region Staking
    public class CreateStakingWorkflow : DebiWorkflow, IWorkflowExt
    {
        public string Id => BrokerActions.BRK_STK_CRSTK;
        public override BrokerRecvType RecvVia => BrokerRecvType.PFRecv;

        public int Version => 1;
    }
    public class AddStakingWorkflow : DebiWorkflow, IWorkflowExt
    {
        public string Id => BrokerActions.BRK_STK_ADDSTK;
        public override BrokerRecvType RecvVia => BrokerRecvType.None;

        public int Version => 1;
    }

    public class UnStakingWorkflow : DebiWorkflow, IWorkflowExt
    {
        public string Id => BrokerActions.BRK_STK_UNSTK;
        public override BrokerRecvType RecvVia => BrokerRecvType.PFRecv;

        public int Version => 1;
    }
    #endregion
}
