using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WorkflowCore.Interface;

namespace Lyra.Core.WorkFlow
{
    public interface IDebiWorkFlow
    {
        WorkFlowDescription GetDescription();
        Task<WrokflowAuthResult> PreAuthAsync(DagSystem sys, SendTransferBlock send, TransactionBlock last);
        Task<TransactionBlock> MainProcAsync(DagSystem sys, SendTransferBlock send, LyraContext context);
    }

    public class WrokflowAuthResult
    {
        public APIResultCodes Result { get; set; }
        public List<string> LockedIDs { get; set; }
    }
}
