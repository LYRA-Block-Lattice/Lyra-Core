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
        Task<Func<DagSystem, LyraContext, Task<TransactionBlock>>[]> GetProceduresAsync(DagSystem sys, LyraContext context);

        Task<WorkflowAuthResult> PreAuthAsync(DagSystem sys, LyraContext context);
        Task<TransactionBlock> MainProcAsync(DagSystem sys, LyraContext context);

        Task<ReceiveTransferBlock> NormalReceiveAsync(DagSystem sys, LyraContext context);
        Task<ReceiveTransferBlock> RefundReceiveAsync(DagSystem sys, LyraContext context);
        Task<SendTransferBlock> RefundSendAsync(DagSystem sys, LyraContext context);
    }

    public class WorkflowAuthResult
    {
        public APIResultCodes Result { get; set; }
        public List<string> LockedIDs { get; set; }
    }
}
