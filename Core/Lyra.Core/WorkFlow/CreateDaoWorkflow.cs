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
    public class CreateDaoWorkflow : DebiWorkflow, IWorkflowExt
    {
        public string Id => BrokerActions.BRK_DAO_CRDAO;
        public override BrokerRecvType RecvVia => BrokerRecvType.PFRecv;

        public int Version => 1;
    }
}
