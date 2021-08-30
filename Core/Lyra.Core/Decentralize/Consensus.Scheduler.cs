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
            await CreateJob(typeof(HeartBeater), "Heart Beat", jobGroup, TimeSpan.FromSeconds(28));
            await CreateJob(typeof(BlockAuthorizationMonitor), "Block Monitor", jobGroup, TimeSpan.FromMilliseconds(100));
            await CreateJob(typeof(LeaderTaskMonitor), "Leader Monitor", jobGroup, "0/2 * * * * ?");
            await CreateJob(typeof(NewPlayerMonitor), "Player Monitor", jobGroup, "* 0/10 * * * ?");

            // Start up the scheduler (nothing can actually run until the
            // scheduler has been started)
            await _sched.Start();
        }

        private async Task CloseJobScheduler()
        {
            // shut down the scheduler
            await _sched.Shutdown(true);
        }

        private async Task CreateJob(Type job, string name, string group, TimeSpan ts)
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

        private async Task CreateJob(Type job, string name, string group, string cronStr)
        {
            await _sched.ScheduleJob(
                JobBuilder
                .Create<LeaderTaskMonitor>()
                .WithIdentity(name, group)
                .Build(),

                TriggerBuilder.Create()
                .WithIdentity($"{name} trigger", group)
                .StartNow()
                .WithCronSchedule(cronStr)
                .Build()
            );
        }

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
                        await cs.DeclareConsensusNodeAsync();
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
                try
                {
                    var cs = context.Scheduler.Context.Get("cs") as ConsensusService;

                    if (cs._viewChangeHandler.IsViewChanging)
                    {
                        if (cs._viewChangeHandler.CheckTimeout())
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
                        foreach (var worker in cs._activeConsensus.Values.ToArray())
                        {
                            if (worker.CheckTimeout())
                            {
                                // no close. use dotnet's dispose.
                                cs._activeConsensus.TryRemove(worker.Hash, out _);
                            }
                        }

                        await cs.ConsolidateBlocksAsync();
                    }


                }
                catch (Exception)
                {

                }
            }
        }

        [DisallowConcurrentExecution]
        private class LeaderTaskMonitor : IJob
        {
            public async Task Execute(IJobExecutionContext context)
            {
                try
                {
                    // check svc tasks
                    var cs = context.Scheduler.Context.Get("cs") as ConsensusService;

                    // leader monitor. check if all items in _pendingLeaderTasks is finished. if not, change view to remove the leader.
                    cs._svcQueue.Clean();
                    var timeoutTasks = cs._svcQueue.TimeoutTxes;
                    if (timeoutTasks.Any())
                    {

                        await cs.BeginChangeViewAsync("Leader svc checker timer", ViewChangeReason.FaultyLeaderNode);
                        cs._svcQueue.Clean();
                        cs._svcQueue.ResetTimestamp();
                    }

                    // check consolidation block
                }
                catch (Exception)
                {

                }
            }
        }

        [DisallowConcurrentExecution]
        private class NewPlayerMonitor : IJob
        {
            public Task Execute(IJobExecutionContext context)
            {
                try
                {
                    //throw new NotImplementedException();
                }
                catch (Exception)
                {

                }

                return Task.CompletedTask;
            }
        }
    }

   
}
