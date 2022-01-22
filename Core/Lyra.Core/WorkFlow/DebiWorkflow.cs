using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.Blocks;
using Microsoft.Extensions.Logging;
using Neo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WorkflowCore.Interface;
using WorkflowCore.Models;
using WorkflowCore.Services;

namespace Lyra.Core.WorkFlow
{
    public class Repeator : StepBodyAsync
    {
        public Block block;
        public bool finished;
        public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
        {
            var ctx = context.Workflow.Data as LyraContext;

            block = 
                await BrokerOperations.ReceiveViaCallback[ctx.SubWorkflow.GetDescription().RecvVia](DagSystem.Singleton, ctx.SendBlock)
                    ??
                await ctx.SubWorkflow.BrokerOpsAsync(DagSystem.Singleton, ctx.SendBlock)
                    ??
                await ctx.SubWorkflow.ExtraOpsAsync(DagSystem.Singleton, ctx.SendBlock.Hash);
            Console.WriteLine($"BrokerOpsAsync for {ctx.SendBlock.Hash} called and generated {block}");
            return ExecutionResult.Next();
        }
    }

    public enum WFState { Init, Running, Finished, ConsensusTimeout, Error };

    public class LyraContext
    {
        public SendTransferBlock SendBlock { get; set; }
        public WorkFlowBase SubWorkflow { get; set; }

        public WFState State { get; set; }
        public TransactionBlock LastBlock { get; set; }
        public ConsensusResult? LastResult { get; set; }
        public DateTime LastTime { get; set; }
    }

    public abstract class DebiWorkflow
    {
        protected List<string> LockingIds { get; init; } = new List<string>();
        public abstract BrokerRecvType RecvVia { get; }

        // submit block to consensus network
        // monitor timeout and return result 
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
                                .Input(step => step.Message, data => $"Key is ({DateTime.Now:mm:ss.ff}): {data.SendBlock.Hash}, Submiting block {data.LastBlock.Hash}...")
                            .Then<SubmitBlock>()
                                .Input(step => step.block, data => data.LastBlock)
                            .WaitFor("MgBlkDone", data => data.SendBlock.Hash, data => data.LastTime)
                                .Output(data => data.LastResult, step => step.EventData)
                            .Then<CustomMessage>()
                                .Name("Log")
                                .Input(step => step.Message, data => $"Key is ({DateTime.Now:mm:ss.ff}): {data.SendBlock.Hash}, Consensus event is {data.LastResult}.")
                            )                        
                        .Do(then => then
                            .StartWith<CustomMessage>()
                                .Name("Log")
                                .Input(step => step.Message, data => $"Key is ({DateTime.Now:mm:ss.ff}): {data.SendBlock.Hash}, Consensus is monitored.")
                            .Delay(data => TimeSpan.FromSeconds(LyraGlobal.CONSENSUS_TIMEOUT))
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
                    //ConsensusService.Singleton.LockIds(LockingIds);
                    return ExecutionResult.Next();
                    })
                    .Output(data => data.State, step => WFState.Running)
                .While(a => a.State != WFState.Finished && a.State != WFState.Error)
                    .Do(x => x
                        .If(data => data.State == WFState.Running).Do(then => then
                            .StartWith<Repeator>()
                                .Output(data => data.LastBlock, step => step.block)
                            .If(a => true).Do(letConsensus)
                            )
                        .If(data => data.LastResult == ConsensusResult.Nay).Do(then => then
                            .StartWith<CustomMessage>()
                                .Name("Log")
                                .Input(step => step.Message, data => $"Consensus Nay. workflow failed.")
                                .Output(data => data.State, step => WFState.Error)
                            )
                        .If(data => data.State == WFState.ConsensusTimeout).Do(then => then
                            .Parallel()
                                .Do(then => then
                                    .StartWith<ReqViewChange>()
                                    .WaitFor("ViewChanged", data => data.LastBlock.ServiceHash, data => DateTime.Now)
                                        .Output(data => data.LastResult, step => step.EventData)
                                        .Output(data => data.State, step => WFState.Running)
                                    .Then<CustomMessage>()
                                            .Name("Log")
                                            .Input(step => step.Message, data => $"View changed."))
                                .Do(then => then
                                    .StartWith<CustomMessage>()
                                        .Name("Log")
                                        .Input(step => step.Message, data => $"View change is monitored.")
                                    .Delay(data => TimeSpan.FromSeconds(LyraGlobal.VIEWCHANGE_TIMEOUT))
                                    .Then<CustomMessage>()
                                        .Name("Log")
                                        .Input(step => step.Message, data => $"View change is timeout.")
                                        .Output(data => data.State, step => WFState.Running)
                                    )
                                .Join()
                                    .CancelCondition(data => data.State == WFState.Running, true)
                            )
                        )
                .Then(a => {
                    //Console.WriteLine("Ends.");
                    a.Workflow.Reference = "end";
                    //ConsensusService.Singleton.UnLockIds(LockingIds);
                })
                ;
        }
    }

    public class ReqViewChange : StepBodyAsync
    {
        public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
        {
            //var ctx = context.Workflow.Data as LyraContext;
            Console.WriteLine($"Request View Change.");
            await ConsensusService.Singleton.BeginChangeViewAsync("WF Engine", ViewChangeReason.ConsensusTimeout);
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
            if(ConsensusService.Singleton.IsThisNodeLeader)
                await ConsensusService.Singleton.LeaderSendBlockToConsensusAndForgetAsync(block);

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
