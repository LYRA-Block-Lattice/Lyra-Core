using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.Blocks;
using Microsoft.Extensions.Logging;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Serializers;
using Neo;
using Newtonsoft.Json;
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
        public TransactionBlock block;
        public bool finished;
        public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
        {
            var ctx = context.Workflow.Data as LyraContext;
            var SubWorkflow = BrokerFactory.DynWorkFlows[ctx.SvcRequest];

            var sendBlock = await DagSystem.Singleton.Storage.FindBlockByHashAsync(ctx.SendHash)
                as SendTransferBlock;
            block = 
                await BrokerOperations.ReceiveViaCallback[SubWorkflow.GetDescription().RecvVia](DagSystem.Singleton, sendBlock)
                    ??
                await SubWorkflow.BrokerOpsAsync(DagSystem.Singleton, sendBlock)
                    ??
                await SubWorkflow.ExtraOpsAsync(DagSystem.Singleton, ctx.SendHash);
            Console.WriteLine($"BrokerOpsAsync for {ctx.SendHash} called and generated {block}");

            ctx.SetLastBlock(block);

            return ExecutionResult.Next();
        }
    }

    public enum WFState { Init, Running, Finished, ConsensusTimeout, Error };

    public class ContextSerializer : SerializerBase<LyraContext>
    {
        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, LyraContext value)
        {
            base.Serialize(context, args, value);
        }

        public override LyraContext Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            return base.Deserialize(context, args);
        }
    }

    [BsonIgnoreExtraElements]
    public class LyraContext
    {
        public string SvcRequest { get; set; }
        public string SendHash { get; set; }

        public WFState State { get; set; }

        public BlockTypes LastBlockType { get; set; }
        public string LastBlockJson { get; set; }
        public ConsensusResult? LastResult { get; set; }
        public long TimeTicks { get; set; }

        private int count { get; set; }

        public TransactionBlock GetLastBlock()
        {
            if (LastBlockType == BlockTypes.Null)
                return null;

            var br = new BlockAPIResult
            {
                ResultBlockType = LastBlockType,
                BlockData = LastBlockJson,
            };
            return br.GetBlock() as TransactionBlock;
        }
        public void SetLastBlock(TransactionBlock block)
        {
            if (block == null)
            {
                LastBlockType = BlockTypes.Null;
            }
            else
            {
                LastBlockType = block.GetBlockType();
                LastBlockJson = JsonConvert.SerializeObject(block);
                count++;
            }
        }
        public int GetCount() => count;
    }

    public abstract class DebiWorkflow
    {
        protected List<string> LockingIds { get; init; } = new List<string>();
        public abstract BrokerRecvType RecvVia { get; }

        // submit block to consensus network
        // monitor timeout and return result 
        public Action<IWorkflowBuilder<LyraContext>> letConsensus => new Action<IWorkflowBuilder<LyraContext>>(branch => branch
                
                .If(data => data.GetLastBlock() != null).Do(then => then
                    .StartWith<CustomMessage>()
                        .Name("Log")
                        .Input(step => step.Message, data => $"Key is ({DateTime.Now:mm:ss.ff}): {data.SendHash}, {data.GetCount()}/, Block is {data.LastBlockType} Let's consensus")
                        .Output(data => data.TimeTicks, step => DateTime.UtcNow.Ticks)
                        .Output(data => data.LastResult, step => null)
                    .Parallel()
                        .Do(then => then
                            .StartWith<CustomMessage>()
                                .Name("Log")
                                .Input(step => step.Message, data => $"Key is ({DateTime.Now:mm:ss.ff}): {data.SendHash}, {data.GetCount()}/, Submiting block {data.GetLastBlock().Hash}...")
                            .Then<SubmitBlock>()
                                .Input(step => step.block, data => data.GetLastBlock())
                            .WaitFor("MgBlkDone", data => data.SendHash, data => new DateTime(data.TimeTicks, DateTimeKind.Utc))
                                .Output(data => data.LastResult, step => step.EventData)
                            .Then<CustomMessage>()
                                .Name("Log")
                                .Input(step => step.Message, data => $"Key is ({DateTime.Now:mm:ss.ff}): {data.SendHash}, {data.GetCount()}/,Consensus event is {data.LastResult}.")
                            )                        
                        .Do(then => then
                            .StartWith<CustomMessage>()
                                .Name("Log")
                                .Input(step => step.Message, data => $"Key is ({DateTime.Now:mm:ss.ff}): {data.SendHash}, {data.GetCount()}/,Consensus is monitored.")
                            .Delay(data => TimeSpan.FromSeconds(LyraGlobal.CONSENSUS_TIMEOUT))
                            .Then<CustomMessage>()
                                .Name("Log")
                                .Input(step => step.Message, data => $"Key is ({DateTime.Now:mm:ss.ff}): {data.SendHash}, {data.GetCount()}/,Consensus is timeout.")
                                .Output(data => data.LastResult, step => ConsensusResult.Uncertain)
                                .Output(data => data.State, step => WFState.ConsensusTimeout)
                            )
                    .Join()
                        .CancelCondition(data => data.LastResult != null, true)
                    .Then<CustomMessage>()
                                .Name("Log")
                                .Input(step => step.Message, data => $"Key is ({DateTime.Now:mm:ss.ff}): {data.SendHash}, {data.GetCount()}/, Consensus completed with {data.LastResult}")
                    )
                .If(data => data.GetLastBlock() == null).Do(then => then
                    .StartWith<CustomMessage>()
                        .Name("Log")
                        .Input(step => step.Message, data => $"Key is ({DateTime.Now:mm:ss.ff}): {data.SendHash}, {data.GetCount()}/, Block is null. Terminate.")
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
                                //.Output(data => data.LastBlock, step => step.block) // already set
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
                                    .WaitFor("ViewChanged", data => data.GetLastBlock().ServiceHash, data => DateTime.Now)
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
