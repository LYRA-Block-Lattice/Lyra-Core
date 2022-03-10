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
        public TransactionBlock block { get; set; }
        public int count { get; set; }

        private ILogger _logger;

        public Repeator(ILogger<Repeator> logger)
        {
            _logger = logger;
        }

        public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
        {
            var ctx = context.Workflow.Data as LyraContext;

            try
            {
                var SubWorkflow = BrokerFactory.DynWorkFlows[ctx.SvcRequest];

                SendTransferBlock? sendBlock = null;
                for (int i = 0; i < 20; i++)
                {
                    sendBlock = await DagSystem.Singleton.Storage.FindBlockByHashAsync(ctx.SendHash)
                        as SendTransferBlock;

                    if (sendBlock != null)
                        break;

                    await Task.Delay(100);
                }

                if (sendBlock == null)
                {

                }
                else
                {
                    block =
                        await BrokerOperations.ReceiveViaCallback[SubWorkflow.GetDescription().RecvVia](DagSystem.Singleton, sendBlock)
                            ??
                        await SubWorkflow.MainProcAsync(DagSystem.Singleton, sendBlock, ctx);
                    
                    _logger.LogInformation($"Key is ({DateTime.Now:mm:ss.ff}): {ctx.SendHash}, {ctx.Count}/, BrokerOpsAsync called and generated {block}");

                    if (block != null)
                        count++;
                    else
                        ctx.State = WFState.Finished;
                }
            }
            catch(Exception ex)
            {
                _logger.LogCritical($"Fatal: Workflow can't generate block: {ex}");
                block = null;
                ctx.State = WFState.Error;
            }

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
        public string OwnerAccountId { get; set; }
        public string SvcRequest { get; set; }
        public string SendHash { get; set; }

        public WFState State { get; set; }

        public BlockTypes LastBlockType { get; set; }
        public string LastBlockJson { get; set; }
        public ConsensusResult? LastResult { get; set; }
        public long TimeTicks { get; set; }

        public int Count { get; set; }
        public int ViewChangeReqCount { get; set; }

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
        //public void SetLastBlock(TransactionBlock block)
        //{
        //    if (block == null)
        //    {
        //        LastBlockType = BlockTypes.Null;
        //    }
        //    else
        //    {
        //        LastBlockType = block.GetBlockType();
        //        LastBlockJson = JsonConvert.SerializeObject(block);
        //    }
        //}
        public static (BlockTypes type, string json) ParseBlock(TransactionBlock tx)
        {
            if (tx == null)
                return (BlockTypes.Null, null);

            return (tx.BlockType, JsonConvert.SerializeObject(tx));
        }
    }

    public abstract class DebiWorkflow
    {
        // submit block to consensus network
        // monitor timeout and return result 
        public Action<IWorkflowBuilder<LyraContext>> letConsensus => new Action<IWorkflowBuilder<LyraContext>>(branch => branch

                .If(data => data.GetLastBlock() != null).Do(then => then
                    .StartWith<CustomMessage>()
                        .Name("Log")
                        .Input(step => step.Message, data => $"Block is {data.LastBlockType} Let's consensus")
                        .Output(data => data.TimeTicks, step => DateTime.UtcNow.Ticks)
                        .Output(data => data.LastResult, step => null)
                    .Parallel()
                        .Do(then => then
                            .StartWith<CustomMessage>()
                                .Name("Log")
                                .Input(step => step.Message, data => $"Submiting block {data.GetLastBlock().Hash}...")
                            .Then<SubmitBlock>()
                                .Input(step => step.block, data => data.GetLastBlock())
                            .WaitFor("MgBlkDone", data => data.SendHash, data => new DateTime(data.TimeTicks, DateTimeKind.Utc))
                                .Output(data => data.LastResult, step => step.EventData)
                            .Then<CustomMessage>()
                                .Name("Log")
                                .Input(step => step.Message, data => $"Consensus event is {data.LastResult}.")
                            )
                        .Do(then => then
                            .StartWith<CustomMessage>()
                                .Name("Log")
                                .Input(step => step.Message, data => $"Consensus is monitored.")
                            .Delay(data => TimeSpan.FromSeconds(LyraGlobal.CONSENSUS_TIMEOUT))
                            .Then<CustomMessage>()
                                .Name("Log")
                                .Input(step => step.Message, data => $"Consensus is timeout.")
                                .Output(data => data.LastResult, step => ConsensusResult.Uncertain)
                                .Output(data => data.State, step => WFState.ConsensusTimeout)
                            .Then<CustomMessage>()
                                .Name("Log")
                                .Input(step => step.Message, data => $"State Changed.")
                            )
                    .Join()
                        .CancelCondition(data => data.LastResult != null, true)
                    .Then<CustomMessage>()
                                .Name("Log")
                                .Input(step => step.Message, data => $"Consensus completed with {data.LastResult}")
                    )
                .If(data => data.GetLastBlock() == null).Do(then => then
                    .StartWith<CustomMessage>()
                        .Name("Log")
                        .Input(step => step.Message, data => $"Block is null. Terminate.")
                    .Output(data => data.State, step => WFState.Finished)
                    .Then<CustomMessage>()
                         .Name("Log")
                         .Input(step => step.Message, data => $"State Changed.")
                ));

        public void Build(IWorkflowBuilder<LyraContext> builder)
        {
            builder
                .StartWith(a =>
                {
                    a.Workflow.Reference = "start";
                    //Console.WriteLine($"{this.GetType().Name} start with {a.Workflow.Data}");
                    //ConsensusService.Singleton.LockIds(LockingIds);
                    return ExecutionResult.Next();
                })
                    .Output(data => data.State, step => WFState.Running)
                    .Then<CustomMessage>()
                        .Name("Log")
                        .Input(step => step.Message, data => $"State Changed.")
                    .While(a => a.State != WFState.Finished && a.State != WFState.Error)
                        .Do(x => x
                            .If(data => data.State == WFState.Running)
                                .Do(then => then
                                .StartWith<Repeator>()
                                    .Input(step => step.count, data => data.Count)
                                    .Output(data => data.Count, step => step.count)
                                    .Output(data => data.LastBlockType, step => LyraContext.ParseBlock(step.block).type)
                                    .Output(data => data.LastBlockJson, step => LyraContext.ParseBlock(step.block).json)
                                .If(data => data.LastBlockType != BlockTypes.Null).Do(letConsensus))
                            .If(data => data.LastBlockType != BlockTypes.Null && data.LastResult == ConsensusResult.Nay).Do(then => then
                                .StartWith<CustomMessage>()
                                    .Name("Log")
                                    .Input(step => step.Message, data => $"Consensus Nay. workflow failed.")
                                    .Output(data => data.State, step => WFState.Error)
                                .Then<CustomMessage>()
                                      .Name("Log")
                                      .Input(step => step.Message, data => $"State Changed.")
                                )
                            .If(data => data.LastBlockType != BlockTypes.Null && data.State == WFState.ConsensusTimeout)
                                .Do(then => then
                                .Parallel()
                                    .Do(then => then
                                        .StartWith<ReqViewChange>()
                                        .WaitFor("ViewChanged", data => data.GetLastBlock().ServiceHash, data => DateTime.Now)
                                            .Output(data => data.LastResult, step => step.EventData)
                                            .Output(data => data.State, step => WFState.Running)
                                        .Then<CustomMessage>()
                                                .Name("Log")
                                                .Input(step => step.Message, data => $"View changed.")
                                        .Then<CustomMessage>()
                                            .Name("Log")
                                            .Input(step => step.Message, data => $"State Changed.")
                                        )
                                    .Do(then => then
                                        .StartWith<CustomMessage>()
                                            .Name("Log")
                                            .Input(step => step.Message, data => $"View change is monitored.")
                                        .Delay(data => TimeSpan.FromSeconds(LyraGlobal.VIEWCHANGE_TIMEOUT))
                                        .Then<CustomMessage>()
                                            .Name("Log")
                                            .Input(step => step.Message, data => $"View change is timeout.")
                                            .Output(data => data.State, step => WFState.Running)
                                        .Then<CustomMessage>()
                                            .Name("Log")
                                            .Input(step => step.Message, data => $"State Changed.")
                                        )
                                    .Join()
                                        .CancelCondition(data => data.State == WFState.Running, true)
                                )
                            ) // do
                .Then(a =>
                {
                    //Console.WriteLine("Ends.");
                    a.Workflow.Reference = "end";
                    //ConsensusService.Singleton.UnLockIds(LockingIds);
                })
                ;
        }
    }

    public class ReqViewChange : StepBodyAsync
    {
        private ILogger _logger;

        public ReqViewChange(ILogger<ReqViewChange> logger)
        {
            _logger = logger;
        }

        public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
        {
            var ctx = context.Workflow.Data as LyraContext;

            ctx.ViewChangeReqCount++;
            if (ctx.ViewChangeReqCount > 10)
            {
                _logger.LogInformation($"View change req more than 10 times. Permanent error. Key: {ctx.SvcRequest}: {ctx.SendHash}");
                ctx.State = WFState.Error;
            }
            else
            {
                _logger.LogInformation($"Request View Change.");
                await ConsensusService.Singleton.BeginChangeViewAsync("WF Engine", ViewChangeReason.ConsensusTimeout);
            }

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
            if (ConsensusService.Singleton.IsThisNodeLeader)
                await ConsensusService.Singleton.LeaderSendBlockToConsensusAndForgetAsync(block);

            return ExecutionResult.Next();
        }
    }

    public class CustomMessage : StepBodyAsync
    {
        public string Message { get; set; }

        private ILogger _logger;

        public CustomMessage(ILogger<CustomMessage> logger)
        {
            _logger = logger;
        }

        public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
        {
            var ctx = context.Workflow.Data as LyraContext;
            var log = $"([WF] {DateTime.Now:mm:ss.ff}) Key is: {ctx.SendHash}, {ctx.Count}/, {Message}";
            _logger.LogInformation(log);

            await ConsensusService.Singleton.FireSignalrWorkflowEventAsync(new WorkflowEvent
            {
                Owner = ctx.OwnerAccountId,
                State = ctx.State.ToString(),
                Name = ctx.SvcRequest,
                Key = ctx.SendHash,
                Action = ctx.LastBlockType.ToString(),
                Result = ctx.LastResult.ToString(),
                Message = Message,
            });
            return ExecutionResult.Next();
        }
    }
}
