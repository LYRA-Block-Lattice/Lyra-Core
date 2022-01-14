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
    public class ReqReceiver : StepBodyAsync
    {
        public ReceiveTransferBlock ConfirmSvcReq { get; set; }

        public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
        {
            var ctx = context.Workflow.Data as LyraContext;
            ConfirmSvcReq = await BrokerOperations.ReceiveViaCallback[ctx.SubWorkflow.GetDescription().RecvVia](ctx.Sys, ctx.SendBlock);
            return ExecutionResult.Next();
        }
    }

    public class Repeator : StepBodyAsync
    {
        public Block block;
        public bool finished;
        public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
        {
            var ctx = context.Workflow.Data as LyraContext;
            Console.WriteLine($"BrokerOpsAsync called.");
            block = await ctx.SubWorkflow.BrokerOpsAsync(ctx.Sys, ctx.SendBlock);
            return ExecutionResult.Next();
        }

        private Task HandlerAsync(Block b)
        {
            block = b;
            return Task.CompletedTask;
        }
    }

    public interface IWorkflowExt : IWorkflow<LyraContext>
    {
        BrokerRecvType RecvVia { get; }
    }

    public class LyraContext
    {
        public DagSystem Sys { get; init; }
        public ConsensusService Consensus { get; init; }
        public SendTransferBlock SendBlock { get; init; }
        public WorkFlowBase SubWorkflow { get; init; }

        public TransactionBlock LastBlock { get; init; }
        public ConsensusResult? LastResult { get; init; }
    }

    public class CreateDaoWorkflow : IWorkflowExt
    {
        public string Id => BrokerActions.BRK_DAO_CRDAO;
        public BrokerRecvType RecvVia => BrokerRecvType.PFRecv;

        public int Version => 1;

        public void Build(IWorkflowBuilder<LyraContext> builder)
        {
            var letConsensus = new Action<IWorkflowBuilder<LyraContext>>(branch => branch
                .If(data => data.LastBlock != null).Do(then => then
                    .StartWith<SubmitBlock>()
                        .Input(step => step.block, data => data.LastBlock)
                    .WaitFor("Consensus", data => data.SendBlock.Hash)
                        .Output(data => data.LastResult, step => step.EventData)
                    .Then<CustomMessage>()
                        .Name("Log")
                        .Input(step => step.Message, data => $"Consensus result is {data.LastResult}.")
                        )
                .If(data => data.LastBlock == null).Do(then => then
                    .StartWith<CustomMessage>()
                        .Name("Log")
                        .Input(step => step.Message, data => $"Block is null.")
                ));

            builder
                .StartWith(a => { Console.WriteLine($"CreateDaoWorkflow start with {a.Workflow.Data}"); })
                .Then<ReqReceiver>()
                    .Output(data => data.LastBlock, step => step.ConfirmSvcReq)
                .Then<CustomMessage>()
                    .Name("Log")
                    .Input(step => step.Message, data => $"CreateDaoWorkflow generated {data.LastBlock}.")
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

    public class SubmitBlock : StepBodyAsync
    {
        public TransactionBlock block { get; init; }

        public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
        {
            var ctx = context.Workflow.Data as LyraContext;

            Console.WriteLine($"In SubmitBlock: {block}");
            await ctx.Consensus.SendBlockToConsensusAndForgetAsync(block);

            return ExecutionResult.Next();
        }
    }

    public class CustomMessage : StepBody
    {

        public string Message { get; set; }

        public override ExecutionResult Run(IStepExecutionContext context)
        {
            Console.WriteLine(Message);
            return ExecutionResult.Next();
        }
    }
}
