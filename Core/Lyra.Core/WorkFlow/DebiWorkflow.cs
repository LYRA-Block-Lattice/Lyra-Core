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
            //Console.WriteLine($"BrokerOpsAsync called.");
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

    public enum WFState { Init, Running, Finished, ConsensusTimeout };

    public class LyraContext
    {
        public DagSystem Sys { get; init; }
        public ConsensusService Consensus { get; init; }
        public SendTransferBlock SendBlock { get; init; }
        public WorkFlowBase SubWorkflow { get; init; }

        public WFState State { get; set; }
        public TransactionBlock LastBlock { get; set; }
        public ConsensusResult? LastResult { get; set; }
        public DateTime LastTime { get; set; }
    }

    public abstract class DebiWorkflow
    {
        public abstract BrokerRecvType RecvVia { get; }
        public Action<IWorkflowBuilder<LyraContext>> letConsensus => new Action<IWorkflowBuilder<LyraContext>>(branch => branch
                
                .If(data => data.LastBlock != null).Do(then => then

                    .StartWith<CustomMessage>()
                        .Name("Log")
                        .Input(step => step.Message, data => $"Key is ({DateTime.Now:mm:ss.ff}): {data.SendBlock.Hash} Block is {data.LastBlock.GetBlockType()} Let's consensus")
                        .Output(data => data.LastTime, step => DateTime.UtcNow)
                        .Output(data => data.LastResult, step => null)
                    .Parallel()
                        .Do(then => then
                            .StartWith<CustomMessage>()
                                .Name("Log")
                                .Input(step => step.Message, data => $"Key is ({DateTime.Now:mm:ss.ff}): {data.SendBlock.Hash}, Submiting block...")
                            .Then<SubmitBlock>()
                                .Input(step => step.block, data => data.LastBlock)
                            .WaitFor("Consensus", data => data.SendBlock.Hash, data => data.LastTime)
                                .Output(data => data.LastResult, step => step.EventData)
                            .Then<CustomMessage>()
                                .Name("Log")
                                .Input(step => step.Message, data => $"Key is ({DateTime.Now:mm:ss.ff}): {data.SendBlock.Hash}, Consensus event is {data.LastResult}.")
                            )                        
                        .Do(then => then
                            .StartWith<CustomMessage>()
                                .Name("Log")
                                .Input(step => step.Message, data => $"Key is ({DateTime.Now:mm:ss.ff}): {data.SendBlock.Hash}, Consensus is monitored.")
                            .Delay(data => TimeSpan.FromSeconds(5))
                            .Then<CustomMessage>()
                                .Name("Log")
                                .Input(step => step.Message, data => $"Key is ({DateTime.Now:mm:ss.ff}): {data.SendBlock.Hash}, Consensus is timeout.")
                                .Output(data => data.LastResult, step => ConsensusResult.Uncertain)
                                .Output(data => data.State, step => WFState.ConsensusTimeout)
                            )
                    .Join()
                        .CancelCondition(data => data.LastResult != null, true)
                    .Then<CustomMessage>()
                                .Name("Log")
                                .Input(step => step.Message, data => $"Key is ({DateTime.Now:mm:ss.ff}): {data.SendBlock.Hash}, Consensus completed with {data.LastResult}")
                    )
                .If(data => data.LastBlock == null).Do(then => then
                    .StartWith<CustomMessage>()
                        .Name("Log")
                        .Input(step => step.Message, data => $"Key is ({DateTime.Now:mm:ss.ff}): {data.SendBlock.Hash}, Block is null. Terminate.")
                    .Output(data => data.State, step => WFState.Finished)
                ));

        public void Build(IWorkflowBuilder<LyraContext> builder)
        {
            builder
                .StartWith(a => {
                    a.Workflow.Reference = "start";
                    //Console.WriteLine($"{this.GetType().Name} start with {a.Workflow.Data}");
                    return ExecutionResult.Next();
                    })
                    .Output(data => data.State, step => WFState.Running)
                .If(a => RecvVia != BrokerRecvType.None)
                    .Do(a => a
                        .StartWith<ReqReceiver>()
                            .Output(data => data.LastBlock, step => step.ConfirmSvcReq)                            
                        .Then<CustomMessage>()
                            .Name("Log")
                            .Input(step => step.Message, data => $"{this.GetType().Name} generated {data.LastBlock}.")
                        .If(a => true).Do(letConsensus)
                )
                .Then<CustomMessage>()
                        .Name("Log")
                        .Input(step => step.Message, data => $"In the middle. InRuning: {data.State}")
                .While(a => a.State != WFState.Finished)
                    .Do(x => x
                        .If(data => data.State == WFState.Running).Do(then => then
                            .StartWith<Repeator>()
                                .Output(data => data.LastBlock, step => step.block)
                            .If(a => true).Do(letConsensus)
                            )
                        .If(data => data.State == WFState.ConsensusTimeout).Do(then => then
                            .StartWith<ReqViewChange>()
                            .Delay(data => TimeSpan.FromSeconds(45))
                                .Output(data => data.State, step => WFState.Running)
                            )
                        )
                .Then(a => {
                    //Console.WriteLine("Ends.");
                    a.Workflow.Reference = "end";
                })
                ;
        }
    }

    public class ReqViewChange : StepBody
    {
        public override ExecutionResult Run(IStepExecutionContext context)
        {
            var ctx = context.Workflow.Data as LyraContext;
            Console.WriteLine($"Request View Change.");
            return ExecutionResult.Next();
        }
    }

    public class SubmitBlock : StepBodyAsync
    {
        public TransactionBlock block { get; init; }

        public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
        {
            var ctx = context.Workflow.Data as LyraContext;

            //Console.WriteLine($"In SubmitBlock: {block}");
            _ = Task.Run(async () => { await ctx.Consensus.SendBlockToConsensusAndForgetAsync(block); });

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
