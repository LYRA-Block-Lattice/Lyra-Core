using System;
using System.Collections.Generic;
using System.Text;
using WorkflowCore.Interface;

namespace Lyra.Core.API
{
    public interface IHostEnv
    {
        string GetThumbPrint();
        IWorkflowHost GetWorkflowHost();
        void SetWorkflowHost(IWorkflowHost workflowHost);
    }
}
