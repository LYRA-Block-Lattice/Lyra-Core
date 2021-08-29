using Lyra.Core.Utils;
using Quartz;
using Quartz.Impl;
using System;
using System.Collections.Generic;
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
                        var x = context.Scheduler.Context.Get("cs");
                        await (x as ConsensusService).DeclareConsensusNodeAsync();
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

        [DisallowConcurrentExecution]
        private class LeaderTaskMonitor : IJob
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
