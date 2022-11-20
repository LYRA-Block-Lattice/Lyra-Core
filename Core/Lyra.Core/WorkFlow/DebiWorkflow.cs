using Akka.Util;
using Loyc.Collections;
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
        public TransactionBlock? block { get; set; }
        public int count { get; set; }

        private ILogger<Repeator> _logger;

        public Repeator(ILogger<Repeator> logger)
        {
            _logger = logger;
        }


        // +------------------------+-----------+-------------------------+----------------------+
        // | Account Type/Send Type |  Normal   |       Service REQ       |      Management      |
        // +------------------------+-----------+-------------------------+----------------------+
        // | Normal                 | No Action | Dataflow Action, new WF | Never                |
        // | Broker                 | No Action | Dataflow Action, new WF | Continue in workflow |
        // +------------------------+-----------+-------------------------+----------------------+

        // the spirit of workflow:
        // 1. pure function.
        //      a workflow should change nothing. it's input is immutable database: the blockchain.
        // 2. refundable.
        //      any broker account must support refund (with send)

        public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
        {
            var ctx = context.Workflow.Data as LyraContext;

            context.Workflow.Reference = ctx.State.ToString();

            _logger.LogInformation($"In Repeator, State: {ctx.State } Key: {ctx.GetSendHash}");
            try
            {
                var SubWorkflow = BrokerFactory.DynWorkFlows[ctx.GetSvcRequest()];


                // send block has tow catalog: having svcreq or not. workflow only process the ones having svcreq.
                // so there are a lot of queued send blocks, triggering corresponding workflow.
                // first try to lock the resource (get from send)
                // if can't, delay random 10 ~ 100 ms.
                // auth the send.
                // if auth failed, release the lock, create an unreceive block, procceed next
                // if auth ok, do full workflow, 
                // when workflow done, unlock resources. procceed next
                if(ctx.State == WFState.Init)
                {
                    ctx.AuthResult = await SubWorkflow.PreAuthAsync(DagSystem.Singleton, ctx);
                    if(ctx.AuthResult.Result != APIResultCodes.Success)
                        _logger.LogWarning($"CTX Auth result: {ctx.AuthResult.Result}");

                    if (ctx.AuthResult.Result == APIResultCodes.Success)
                    {
                        ctx.State = WFState.NormalReceive;
                    }
                    else
                    {
                        ctx.State = WFState.RefundReceive;
                    }
                }

                block = ctx.State switch
                {
                    WFState.NormalReceive => await SubWorkflow.NormalReceiveAsync(DagSystem.Singleton, ctx),
                    WFState.RefundReceive => await SubWorkflow.RefundReceiveAsync(DagSystem.Singleton, ctx),
                    WFState.Refund => await SubWorkflow.RefundSendAsync(DagSystem.Singleton, ctx),
                    WFState.Running => await SubWorkflow.MainProcAsync(DagSystem.Singleton, ctx),
                    _ => throw new Exception($"Unaccepted wf state: {ctx.State}")
                };       

                _logger.LogInformation($"Key is ({DateTime.Now:mm:ss.ff}): {ctx.GetSendHash()}, {ctx.Count}/, BrokerOpsAsync called and generated {block}");

                if (block != null)
                {
                    count++;
                    if (!block.ContainsTag(Block.MANAGEDTAG))
                        throw new Exception("Missing MANAGEDTAG");

                    if (!Enum.TryParse(block.Tags[Block.MANAGEDTAG], out WFState mgdstate))
                        throw new Exception("Invalid MANAGEDTAG: illeagle state");

                    if (mgdstate != ctx.State)
                        throw new Exception("Invalid MANAGEDTAG: not current state");

                    ctx.State = ctx.State switch
                    {
                        WFState.NormalReceive => WFState.Running,
                        WFState.RefundReceive => WFState.Refund,
                        WFState.Refund => WFState.Finished,
                        WFState.Running => WFState.Running,
                        _ => ctx.State
                    };
                }
                else
                    ctx.State = WFState.Finished;
            }
            catch (Exception ex)
            {
                _logger.LogCritical($"Fatal: Workflow can't generate block: {ex}");
                Console.WriteLine($"Fatal: Workflow can't generate block: {ex}");
                block = null;
                ctx.State = WFState.Error;
            }

            return ExecutionResult.Next();
        }
    }
        
    //public class ContextSerializer : SerializerBase<LyraContext>
    //{
    //    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, LyraContext value)
    //    {
    //        base.Serialize(context, args, value);
    //    }

    //    public override LyraContext Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    //    {
    //        return base.Deserialize(context, args);
    //    }
    //}

    /// <summary>
    /// control the workflow itself.
    /// </summary>
    public enum WFState
    {
        /// <summary>
        /// workflow just created. next: receive
        /// </summary>
        Init, 

        /// <summary>
        /// Authorized OK, get svc req and process
        /// </summary>
        NormalReceive,

        /// <summary>
        /// we receive the request whatever it is. next: authorize to decide main loop or refund exit.
        /// </summary>
        RefundReceive,

        /// <summary>
        /// failed to authorize the request. we do refund.
        /// </summary>
        Refund,

        /// <summary>
        /// entering main loop & extra proc
        /// </summary>
        Running, 

        /// <summary>
        /// all done.
        /// </summary>
        Finished, 

        /// <summary>
        /// special case
        /// </summary>
        ConsensusTimeout,
        
        /// <summary>
        /// exception happened. code failed or so.
        /// </summary>
        Error, 

        /// <summary>
        /// workflow terminated.
        /// </summary>
        Exited,
    };

    [BsonIgnoreExtraElements]
    public class LyraContext
    {
        public SendTransferBlock Send { get; set; } = null!;
        public string GetOwnerAccountId() => Send.AccountID;
        public string GetSvcRequest() => Send.Tags![Block.REQSERVICETAG];
        public string GetSendHash() => Send.Hash!;
        public WorkflowAuthResult? AuthResult { get; set; }
        public WFState State { get; set; }

        public BlockTypes LastBlockType { get; set; }
        public string? LastBlockJson { get; set; }
        public ConsensusResult? LastResult { get; set; }
        public long TimeTicks { get; set; }

        public int Count { get; set; }
        public int ViewChangeReqCount { get; set; }

        /// <summary>
        /// private data, shared across all steps of the workflow.
        /// </summary>
        public string? spec { get; set; }

        public LyraContext()
        {
            State = WFState.Init;
        }

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
                LastBlockType = block.BlockType;
                LastBlockJson = JsonConvert.SerializeObject(block);
            }
        }
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
                            .StartWith<SubmitBlock>()
                                .Input(step => step.block, data => data.GetLastBlock())
                            .Then<CustomMessage>()
                                .Name("Log")
                                .Input(step => step.Message, data => $"Block {data.GetLastBlock().Hash} submitted. Waiting for result...")
                            .WaitFor("MgBlkDone", data => data.GetSendHash(), data => new DateTime(data.TimeTicks, DateTimeKind.Utc))
                                .Output(data => data.LastResult, step => step.EventData)
                            .Then<CustomMessage>()
                                .Name("Log")
                                .Input(step => step.Message, data => $"Consensus event is {data.LastResult}.")
                            )
                        .Do(then => then
                            .StartWith<CustomMessage>()
                                .Name("Log")
                                .Input(step => step.Message, data => $"Consensus is monitored.")
                            .Delay(data => TimeSpan.FromSeconds(15 + LyraGlobal.CONSENSUS_TIMEOUT * 3))
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
                    a.Workflow.Reference = "Start";
                    //Console.WriteLine($"{this.GetType().Name} start with {a.Workflow.Data}");
                    //ConsensusService.Singleton.LockIds(LockingIds);
                    return ExecutionResult.Next();
                })
                    //.Output(data => data.State, step => WFState.Running)
                    .Then<CustomMessage>()
                        .Name("Log")
                        .Input(step => step.Message, data => $"State Changed.")
                    .While(a => a.State != WFState.Finished && a.State != WFState.Error)
                        .Do(x => x
                            .If(data => true)//data.State == WFState.Running)
                                .Do(then => then
                                .StartWith<Repeator>()      // WF to generate new block
                                    .Input(step => step.count, data => data.Count)
                                    .Output(data => data.Count, step => step.count)
                                    .Output(data => data.LastBlockType, step => LyraContext.ParseBlock(step.block).type)
                                    .Output(data => data.LastBlockJson, step => LyraContext.ParseBlock(step.block).json)
                                .If(data => data.LastBlockType != BlockTypes.Null).Do(letConsensus))    // send to consensus network
                            .If(data => data.LastBlockType != BlockTypes.Null && data.LastResult == ConsensusResult.Nay)
                                .Do(then => then
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
                                            .Output(data => data.State, step => step.PermanentFailed ? WFState.Error : WFState.Running)
                                        .WaitFor("ViewChanged", data => data.GetLastBlock().ServiceHash, data => DateTime.Now)
                                            .Output(data => data.LastResult, step => step.EventData)
                                            //.Output(data => data.State, step => WFState.Running)
                                        .Then<CustomMessage>()
                                                .Name("Log")
                                                .Input(step => step.Message, data => $"View changed.")
                                        )
                                    .Do(then => then
                                        .StartWith<CustomMessage>()
                                            .Name("Log")
                                            .Input(step => step.Message, data => $"View change is monitored.")
                                        .Delay(data => TimeSpan.FromSeconds(LyraGlobal.VIEWCHANGE_TIMEOUT * 3))
                                        .Then<CustomMessage>()
                                            .Name("Log")
                                            .Input(step => step.Message, data => $"View change is timeout.")
                                        )
                                    .Join()
                                        .CancelCondition(data => data.State == WFState.Running, true)
                                )
                            ) // do
                .Then<CustomMessage>()
                    .Name("Log")
                    .Input(step => step.Message, data => $"Workflow is done.")
                .Then(a =>
                {
                    //Console.WriteLine("WF Ends.");
                    a.Workflow.Reference = "Exited";
                    //ConsensusService.Singleton.UnLockIds(LockingIds);
                })
                ;
        }
    }

    public class ReqViewChange : StepBodyAsync
    {
        private ILogger<ReqViewChange> _logger;

        public bool PermanentFailed { get; set; }

        public ReqViewChange(ILogger<ReqViewChange> logger)
        {
            _logger = logger;
            PermanentFailed = false;
        }

        public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
        {
            _logger.LogInformation($"WF Request View Change.");

            var ctx = context.Workflow.Data as LyraContext;

            ctx.ViewChangeReqCount++;
            if (ctx.ViewChangeReqCount > 10)
            {
                _logger.LogInformation($"View change req more than 10 times. Permanent error. Key: {ctx.GetSvcRequest}: {ctx.GetSendHash}");
                ctx.State = WFState.Error;
                PermanentFailed = true;
            }
            else
            {
                await ConsensusService.Singleton.BeginChangeViewAsync("WF Engine", ViewChangeReason.ConsensusTimeout);
            }

            return ExecutionResult.Next();
        }
    }

    public class SubmitBlock : StepBodyAsync
    {
        private ILogger<SubmitBlock> _logger;

        public TransactionBlock? block { get; set; }

        public SubmitBlock(ILogger<SubmitBlock> logger)
        {
            _logger = logger;
        }

        public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
        {
            var ctx = context.Workflow.Data as LyraContext;

            _logger.LogInformation($"In SubmitBlock: Leader? {ConsensusService.Singleton.IsThisNodeLeader} {block.BlockType} {block.Hash} {ctx.State}");

            if (ConsensusService.Singleton.IsThisNodeLeader)
                await ConsensusService.Singleton.LeaderSendBlockToConsensusAndForgetAsync(block);

            return ExecutionResult.Next();
        }
    }

    public class CustomMessage : StepBodyAsync
    {
        public string Message { get; set; }

        private ILogger<CustomMessage> _logger;

        public CustomMessage(ILogger<CustomMessage> logger)
        {
            _logger = logger;
        }

        public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
        {
            var ctx = context.Workflow.Data as LyraContext;
            var log = $"([WF] {DateTime.Now:mm:ss.ff}) Key is: {ctx.GetSendHash()}, {ctx.Count}/{ctx.State.ToString()}, {Message}";
            _logger.LogInformation(log);
            //Console.WriteLine(log);

            await ConsensusService.Singleton.FireSignalrWorkflowEventAsync(new WorkflowEvent
            {
                Owner = ctx.GetOwnerAccountId(),
                State = Message == "Workflow is done." ? "Exited" : ctx.State.ToString(),
                Name = ctx.GetSvcRequest(),
                Key = ctx.GetSendHash(),
                Action = ctx.LastBlockType.ToString(),
                Result = ctx.LastResult.ToString(),
                Message = Message,
            });

            return ExecutionResult.Next();
        }
    }
}
