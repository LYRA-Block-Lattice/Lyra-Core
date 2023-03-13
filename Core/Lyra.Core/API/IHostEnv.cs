using Lyra.Data.API;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using WorkflowCore.Interface;

namespace Lyra.Core.API
{
    public interface IHostEnv
    {
        string GetThumbPrint();
        IWorkflowHost GetWorkflowHost();
        void SetWorkflowHost(IWorkflowHost workflowHost);

        Task FireEventAsync(EventContainer ec);
    }
}
