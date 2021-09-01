using Lyra.Core.Utils;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            await CreateJobAsync(TimeSpan.FromSeconds(28), typeof(HeartBeater), "Heart Beat", jobGroup);
            await CreateJobAsync(TimeSpan.FromMilliseconds(100), typeof(BlockAuthorizationMonitor), "Block Monitor", jobGroup);
            await CreateJobAsync("0/2 * * * * ?", typeof(LeaderTaskMonitor), "Leader Monitor", jobGroup);
            await CreateJobAsync("0 0/10 * * * ?", typeof(NewPlayerMonitor), "Player Monitor", jobGroup);

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
                try
                {
                    if (Neo.Settings.Default.LyraNode.Lyra.Mode == Data.Utils.NodeMode.Normal)
                    {
                        var cs = context.Scheduler.Context.Get("cs") as ConsensusService;
                        await cs.HeartBeatAsync();
                    }
                }
                catch(Exception)
                {

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
                    if (cs._viewChangeHandler.IsViewChanging)
                    {
                        if (cs._viewChangeHandler.IsTimeout)
                        {
                            // view change timeout
                        }

                        //if (cs._viewChangeHandler?.CheckTimeout() == true)
                        //{
                        //    cs._log.LogInformation($"View Change with Id {cs._viewChangeHandler.ViewId} begin {cs._viewChangeHandler.TimeStarted} Ends: {DateTime.Now} used: {DateTime.Now - cs._viewChangeHandler.TimeStarted}");
                        //    cs._viewChangeHandler.Reset();
                        //}
                    }
                    else
                    {
                        // first check if there are timeout
                        var timeoutList = cs._activeConsensus
                            .Where(a => a.Value.IsTimeout);

                        foreach (var worker in cs._activeConsensus.Values.ToArray())
                        {
                            // check to see if anyone wait for view change
                            if(worker.Status == ConsensusWorker.ConsensusWorkerStatus.WaitForViewChanging)
                            {
                                cs._log.LogWarning("View changed. recovery failed block(s)...");
                                worker.Status = ConsensusWorker.ConsensusWorkerStatus.InAuthorizing;
                                worker.Reset();

                                worker.RedoBlockAuthorizing();
                            }

                            if (worker.IsTimeout)
                            {
                                if(worker.State.IsCommited)
                                {
                                    // no close. use dotnet's dispose.
                                    cs._activeConsensus.TryRemove(worker.Hash, out _);
                                }
                                else
                                {
                                    cs._log.LogWarning("Block consensus failed. do view change...");
                                    // consensus failed. change view and redo later
                                    worker.Status = ConsensusWorker.ConsensusWorkerStatus.WaitForViewChanging;

                                    await cs.BeginChangeViewAsync("block monitor", ViewChangeReason.ConsensusTimeout);
                                }
                            }
                        }

                        if(!cs._activeConsensus.Any(a => a.Value.Status == ConsensusWorker.ConsensusWorkerStatus.WaitForViewChanging))
                            await cs.ConsolidateBlocksAsync();
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
            public async Task Execute(IJobExecutionContext context)
            {
                var cs = context.Scheduler.Context.Get("cs") as ConsensusService;

                try
                {
                    // check svc tasks
                    // leader monitor. check if all items in _pendingLeaderTasks is finished. if not, change view to remove the leader.
                    cs._svcQueue.Clean();
                    var timeoutTasks = cs._svcQueue.TimeoutTxes;
                    if (timeoutTasks.Any())
                    {
                        await cs.BeginChangeViewAsync("Leader svc checker timer", ViewChangeReason.LeaderFailedProcessingDEX);
                        cs._svcQueue.Clean();
                        cs._svcQueue.ResetTimestamp();
                    }

                    // check consolidation block
                }
                catch (Exception e)
                {
                    cs._log.LogError($"In LeaderTaskMonitor: {e}");
                }
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
                    // announce self
                    cs._board.ActiveNodes.RemoveAll(a => a.LastActive < DateTime.Now.AddSeconds(-60));
                    await Task.Delay(5000);

                    await cs.DeclareConsensusNodeAsync();

                    // make sure peers update its status
                    await Task.Delay(5000);

                    await cs.CheckNewPlayerAsync();               
                }
                catch (Exception e)
                {
                    cs._log.LogError($"In NewPlayerMonitor: {e}");
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
