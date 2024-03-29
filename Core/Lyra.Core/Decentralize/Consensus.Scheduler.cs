﻿using Akka.Actor;
using Lyra.Core.Utils;
using Lyra.Core.WorkFlow;
using Lyra.Data.API;
using Lyra.Shared;
using Microsoft.Extensions.Logging;
using Neo.Network.P2P;
using Quartz;
using Quartz.Impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using ZstdSharp.Unsafe;
using IScheduler = Quartz.IScheduler;

namespace Lyra.Core.Decentralize
{
    /* The task scheduler for consensus network
     * 
     * every 100 ms: check block consensus timeout
     * every even 2s: check leader tasks (cons, svc queue)
     * every even 1m0s: 
     * every even 10m0s: check new player or wellcome them to join
     */
    public partial class ConsensusService
    {
        private IScheduler _sched;
        // Init the scheduler
        private async Task InitJobSchedulerAsync()
        {
            if (_sched != null)
                return;

            //Quartz.Logging.LogContext.SetCurrentLogProvider(SimpleLogger.Factory);

            // First we must get a reference to a scheduler
            ISchedulerFactory sf = new StdSchedulerFactory();
            _sched = await sf.GetScheduler();
            _sched.Context.Add("cs", this);

            // computer a time that is on the next round minute
            DateTimeOffset runTime = DateBuilder.EvenMinuteDate(DateTimeOffset.UtcNow);

            // define the jobs
            var jobGroup = "consensus service jobs";

            // Tell quartz to schedule the job using our trigger
            await CreateJobAsync(TimeSpan.FromSeconds(24), typeof(HeartBeater), "Heart Beat", jobGroup);
            await CreateJobAsync(TimeSpan.FromMilliseconds(1000), typeof(BlockAuthorizationMonitor), "Block Monitor", jobGroup);
            await CreateJobAsync("0/2 * * * * ?", typeof(LeaderTaskMonitor), "Leader Monitor", jobGroup);
            await CreateJobAsync(TimeSpan.FromMinutes(17), typeof(IdleWorks), "Idle Works", jobGroup);

            // 10 min view change, 30 min fetch balance.
            //if (Neo.Settings.Default.LyraNode.Lyra.NetworkId == "devnet")
            //{
            //    // need a quick debug test
            //    await CreateJobAsync("0 0/2 * * * ?", typeof(NewPlayerMonitor), "Player Monitor", jobGroup);
            //    await CreateJobAsync(TimeSpan.FromMinutes(5), typeof(FetchBalance), "Fetch Balance", jobGroup);
            //}
            //else
            //{
                await CreateJobAsync("0 0/10 * * * ?", typeof(NewPlayerMonitor), "Player Monitor", jobGroup);
                await CreateJobAsync(TimeSpan.FromHours(12), typeof(FetchBalance), "Fetch Balance", jobGroup);
            //}

            // Start up the scheduler (nothing can actually run until the
            // scheduler has been started)
            await _sched.Start();
        }

        //private async Task CloseJobScheduler()
        //{
        //    // shut down the scheduler
        //    await _sched.Shutdown(true);
        //}



        // jobs
        [DisallowConcurrentExecution]
        private class HeartBeater : IJob
        {
            public async Task Execute(IJobExecutionContext context)
            {
                var cs = context.Scheduler.Context.Get("cs") as ConsensusService;
                if (cs == null)
                    return;

                try
                {
                    if (Neo.Settings.Default.LyraNode.Lyra.Mode == Data.Utils.NodeMode.Normal)
                    {
                        await cs.HeartBeatAsync();
                    }

                    // useless
                    //if(Neo.Settings.Default.LyraNode.Lyra.NetworkId != "devnet")    // devnet < 80%
                    //{
                    //    // connect to more peer if connected peer count < 86%
                    //    var status = await cs.GetNodeStatusAsync();
                    //    if (status.connectedPeers / (float)status.activePeers < 0.86)
                    //    {
                    //        Neo.Network.P2P.LocalNode.Singleton.ConnectMoreNodes(status.activePeers - status.connectedPeers);
                    //    }
                    //}
                }
                catch(Exception ex)
                {
                    cs._log.LogError($"In HeartBeater: {ex}");
                }
            }
        }

        [DisallowConcurrentExecution]
        private class BlockAuthorizationMonitor : IJob
        {
            public async Task Execute(IJobExecutionContext context)
            {
                var cs = context.Scheduler.Context.Get("cs") as ConsensusService;

                try
                {
                    // routine maintains
                    var outDated = cs._failedLeaders.Where(x => x.Value < DateTime.UtcNow.AddMinutes(-5)).ToList();
                    if(outDated.Any())
                    {
                        foreach (var od in outDated)
                            cs._failedLeaders.TryRemove(od.Key, out _);
                    }

                    // end routine maintains
                    // app mode view change handler is null
                    if (cs._viewChangeHandler != null && cs._viewChangeHandler.IsViewChanging)
                    {
                        // view change mode
                        if(cs._viewChangeHandler.IsTimeout)
                        {
                            // two reasons: consensus timeout, new leader create service block timeout.
                            // consensus timeout we can simply redo.
                            // for leader timeout we need put it into black list.
                            // continue put node into black list will cause trouble if black list too many nodes.
                            // maybe a attack vector.
                            // so limit blacklisted number to f/2 
                            // need to replace the bad leader with some 'random', like sha256(view id) mod total

                            // view change timeout
                            //if(cs._viewChangeHandler.selectedSuccess)
                            //{
                            //    // leader failure
                            //    cs.AddFailedLeader(cs._viewChangeHandler.nextLeader);

                            //    await cs.BeginChangeViewAsync("view change monitor", ViewChangeReason.NewLeaderFailedCreatingView);
                            //}
                            //else if(cs._viewChangeHandler.LastViewChangeReason != ViewChangeReason.ViewChangeTimeout)
                            //{
                            //    // view change voting failure
                            //    await cs.BeginChangeViewAsync("view change monitor", ViewChangeReason.ViewChangeTimeout);
                            //}
                            //else
                            //{
                            //    cs._log.LogInformation("Stop doing endless view change.");
                            //    cs._viewChangeHandler.StopViewChange();
                            //}
                            cs._log.LogInformation("Stop view change.");
                            cs._viewChangeHandler.StopViewChange();
                        }
                    }
                    else
                    {
                        // normal 
                        foreach (var worker in cs._activeConsensus.Values.ToArray())
                        {
                            if (worker.State == null)
                                continue;

                            // check to see if anyone wait for view change
                            if(worker.Status == ConsensusWorker.ConsensusWorkerStatus.WaitForViewChanging)
                            {
                                cs._log.LogWarning("View changed. recovery failed block(s)...");
                                worker.Status = ConsensusWorker.ConsensusWorkerStatus.InAuthorizing;

                                await worker.RedoBlockAuthorizingAsync();
                            }

                            if (worker.IsTimeout)
                            {
                                if(worker.State.InputMsg?.TimeStamp < DateTime.UtcNow.AddSeconds(-60))
                                {
                                    // should not happen on normal status. so try to resync.
                                    if (cs.CurrentState == BlockChainState.Almighty)
                                        cs._stateMachine.Fire(cs._engageTriggerConsolidateFailed, worker.Hash);

                                    cs._activeConsensus.TryRemove(worker.Hash, out _);
                                    worker.Dispose();
                                }
                                else if (worker.State.IsCommited)
                                {
                                    // no close. use dotnet's dispose.
                                    cs._activeConsensus.TryRemove(worker.Hash, out _);
                                    worker.Dispose();
                                }
                                else
                                {
                                    //cs._log.LogWarning($"Block {worker.State.InputMsg.Block.Hash.Shorten()} {worker.State.InputMsg.Block.BlockType} {worker.State.InputMsg.Block.Height} failed. do view change...");
                                    //// consensus failed. change view and redo later
                                    //worker.Status = ConsensusWorker.ConsensusWorkerStatus.WaitForViewChanging;

                                    //await cs.BeginChangeViewAsync("block monitor", ViewChangeReason.ConsensusTimeout);
                                }
                            }
                        }

                        //if(!cs._activeConsensus.Any(a => a.Value.Status == ConsensusWorker.ConsensusWorkerStatus.WaitForViewChanging))
                        await cs.ConsolidateBlocksAsync();

                        // monitor workflow and lockups
                        if(cs._lockers.Count > 0)
                        {
                            foreach (var lckdto in cs._lockers.Values.ToArray())
                            {
                                if (lckdto.haswf && lckdto.workflowid != null)
                                {
                                    var wf = cs._hostEnv.GetWorkflowHost().PersistenceStore.GetWorkflowInstance(lckdto.workflowid);
                                    if (wf == null)
                                    {
                                        cs.RemoveLockerDTO(lckdto.reqhash);
                                        cs._log.LogWarning($"remove locker for wf exit {lckdto.reqhash}.");
                                    }
                                    else
                                    {
                                        // check wf error
                                        var lc = wf.Result.Data as LyraContext;
                                        if(lc.State == WFState.Error)
                                        {
                                            cs._log.LogWarning($"WF error for {lc.Send.Hash}.");
                                        }
                                    }
                                }
                                else
                                {
                                    if (!cs._activeConsensus.ContainsKey(lckdto.reqhash))
                                    {
                                        cs.RemoveLockerDTO(lckdto.reqhash);
                                        cs._log.LogWarning($"remove locker for consensus out {lckdto.reqhash}.");

                                    }
                                }
                            }

                            cs._log.LogWarning($"Locker count: {cs._lockers.Count}");
                            //foreach(var lkr in cs._lockers)
                            //{
                            //    // dump lockers
                            //    cs._log.LogWarning($"{lkr.Key}:");
                            //    foreach(var id in lkr.Value.lockedups)
                            //    {
                            //        cs._log.LogWarning($"\t-> {id}");
                            //    }
                            //}
                        }
                    }
                }
                catch (Exception e)
                {
                    cs._log.LogError($"In BlockAuthorizationMonitor: {e}");
                }
            }
        }

        [DisallowConcurrentExecution]
        private class LeaderTaskMonitor : IJob
        {
            public Task Execute(IJobExecutionContext context)
            {
                var cs = context.Scheduler.Context.Get("cs") as ConsensusService;

                try
                {
                    if(cs.CurrentState == Data.API.BlockChainState.Almighty)
                    {

                    }


                    // check consolidation block
                }
                catch (Exception e)
                {
                    cs._log.LogError($"In LeaderTaskMonitor: {e}");
                }

                return Task.CompletedTask;
            }
        }

        [DisallowConcurrentExecution]
        private class NewPlayerMonitor : IJob
        {
            public async Task Execute(IJobExecutionContext context)
            {
                var cs = context.Scheduler.Context.Get("cs") as ConsensusService;

                try
                {
                    cs.GetDagSystem().LocalNode.Tell(new LocalNode.NeedSeeds());

                    if (!cs.CanDoConsense)
                        return;

                    await cs.DeclareConsensusNodeAsync();

                    // update profiting account
                    foreach(var node in cs._board.ActiveNodes)
                    {
                        if (node.ProfitingAccountId == null)
                        {
                            var pfts = await cs._sys.Storage.FindAllProfitingAccountForOwnerAsync(node.AccountID);
                            var pft = pfts.Where(a => a.PType == Blocks.ProfitingType.Node)
                                .FirstOrDefault();

                            if (pft != null)
                                node.ProfitingAccountId = pft.AccountID;
                        }
                    }

                    // make sure peers update its status
                    await Task.Delay(10000);

                    await cs.CheckCreateNewViewAsync();               
                }
                catch (Exception e)
                {
                    cs._log.LogError($"In NewPlayerMonitor: {e}");
                }
            }
        }

        [DisallowConcurrentExecution]
        private class IdleWorks : IJob
        {
            public async Task Execute(IJobExecutionContext context)
            {
                var cs = context.Scheduler.Context.Get("cs") as ConsensusService;

                try
                {
                    // TODO: create a better method to detect 'Idle'
                    if(cs.CurrentState == Data.API.BlockChainState.Almighty)
                        await cs._sys.Storage.UpdateStatsAsync();
                }
                catch (Exception e)
                {
                    cs._log.LogError($"In IdleWorks: {e}");
                }
            }
        }

        [DisallowConcurrentExecution]
        private class FetchBalance : IJob
        {
            public async Task Execute(IJobExecutionContext context)
            {
                var cs = context.Scheduler.Context.Get("cs") as ConsensusService;

                try
                {
                    if(cs.IsThisNodePrimary && cs.CanDoConsense)
                    {
                        await cs._sys.PosWallet.SyncAsync(null);
                        // open when all upgraded.
                        var pfts = await cs._sys.Storage.FindAllProfitingAccountForOwnerAsync(cs._sys.PosWallet.AccountId);
                        var pft = pfts.Where(a => a.PType == Blocks.ProfitingType.Node)
                            .FirstOrDefault();
                        if (pft != null)// && pft.AccountID.StartsWith("LRJtGk"))      // debug only
                            await cs._sys.PosWallet.CreateDividendsAsync(pft.AccountID);
                    }
                }
                catch (Exception e)
                {
                    cs._log.LogError($"In FetchBalance: {e}");
                }
            }
        }

        private async Task CreateJobAsync(TimeSpan ts, Type job, string name, string group)
        {
            await _sched.ScheduleJob(
                JobBuilder
                .Create(job)
                .WithIdentity(name, group)
                .Build(),

                TriggerBuilder
                .Create()
                .WithIdentity($"{name} trigger", group)
                .StartNow()
                .WithSimpleSchedule(x => x
                    .WithInterval(ts)
                    .RepeatForever())
                .Build()
            );
        }

        private async Task CreateJobAsync(string cronStr, Type job, string name, string group)
        {
            await _sched.ScheduleJob(
                JobBuilder
                .Create(job)
                .WithIdentity(name, group)
                .Build(),

                TriggerBuilder.Create()
                .WithIdentity($"{name} trigger", group)
                .StartNow()
                .WithCronSchedule(cronStr)
                .Build()
            );
        }
    }

   
}
