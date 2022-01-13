using Lyra.Core.Decentralize;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Lyra.Core.WorkFlow
{
    public class Repeator : StepBody
    {
        public string sendHash;
        public bool success;
        public override ExecutionResult Run(IStepExecutionContext context)
        {
            throw new NotImplementedException();
        }
    }
    public class BlockRunner : IWorkflow<BrokerBlueprint>
    {
        public string Id => "DebiMain";

        public int Version => 1;

        public void Build(IWorkflowBuilder<BrokerBlueprint> builder)
        {
            builder.While(data => !data.FullDone)
                .Do(x => x
                    .StartWith(a => { Console.WriteLine("repeat block start"); })
                    .Then<Repeator>()
                        .Input(step => step.sendHash, data => data.svcReqHash)
                        .Output(data => data.laststepresult, step => step.success)
                    .Then(a => { Console.WriteLine("repeat block start"); }))
                .Then(a => { Console.WriteLine("block completed"); });
        }
    }
}
