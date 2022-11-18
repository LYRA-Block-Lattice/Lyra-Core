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
        Task<Func<DagSystem, SendTransferBlock, Task<TransactionBlock>>[]> GetProceduresAsync(DagSystem sys, SendTransferBlock send);

        Task<WorkflowAuthResult> PreAuthAsync(DagSystem sys, SendTransferBlock send);
        Task<TransactionBlock> MainProcAsync(DagSystem sys, SendTransferBlock send, LyraContext context);
        
        ///// <summary>
        ///// send funds back if auth is failed, or any new situation emerged not allow the operation.
        ///// </summary>
        ///// <param name="sys"></param>
        ///// <param name="send"></param>
        ///// <returns></returns>
        //Task<TransactionBlock> UnReceiveAsync(DagSystem sys, SendTransferBlock send);
    }

    public class WorkflowAuthResult
    {
        public APIResultCodes Result { get; set; }
        public List<string> LockedIDs { get; set; }
    }
}
