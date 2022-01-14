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

        public void Build(IWorkflowBuilder<LyraContext> builder)
        {
            builder
                .StartWith(a => {
                    a.Workflow.Reference = "start";
                    Console.WriteLine($"{this.GetType().Name} start with {a.Workflow.Data}"); 
                })
                .Then<ReqReceiver>()
                    .Output(data => data.LastBlock, step => step.ConfirmSvcReq)
                .Then<CustomMessage>()
                    .Name("Log")
                    .Input(step => step.Message, data => $"{this.GetType().Name} generated {data.LastBlock}.")
                .If(a => true).Do(letConsensus)
                .Then<Repeator>()
                    .Output(data => data.LastBlock, step => step.block)
                .If(a => true).Do(letConsensus)
                .Then(a => { 
                    Console.WriteLine("Ends.");
                    a.Workflow.Reference = "end";
                })
                ;
        }
    }
}
