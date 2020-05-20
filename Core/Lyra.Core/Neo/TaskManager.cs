using Akka.Actor;
using Akka.Configuration;
using Lyra;
using Neo.IO.Actors;
using Neo.IO.Caching;
using Neo.Network.P2P.Payloads;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Neo.Network.P2P
{
    internal class TaskManager : UntypedActor
    {
        public class Register { public VersionPayload Version; }
        public class Update { public uint LastBlockIndex; }
        public class NewTasks { public InvPayload Payload; }
        public class TaskCompleted { public UInt256 Hash; }
        public class HeaderTaskCompleted { }
        public class RestartTasks { public InvPayload Payload; }
        private class Timer { }

        private static readonly TimeSpan TimerInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan TaskTimeout = TimeSpan.FromMinutes(1);

        private readonly DagSystem system;
        private const int MaxConncurrentTasks = 3;

        private const int PingCoolingOffPeriod = 60; // in secconds.
        /// <summary>
        /// A set of known hashes, of inventories or payloads, already received.
        /// </summary>        
        private readonly HashSetCache<UInt256> knownHashes;
        private readonly Dictionary<UInt256, int> globalTasks = new Dictionary<UInt256, int>();
        private readonly Dictionary<IActorRef, TaskSession> sessions = new Dictionary<IActorRef, TaskSession>();
        private readonly ICancelable timer = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(TimerInterval, TimerInterval, Context.Self, new Timer(), ActorRefs.NoSender);

        private readonly UInt256 HeaderTaskHash = UInt256.Zero;
        private bool HasHeaderTask => globalTasks.ContainsKey(HeaderTaskHash);

        public TaskManager(DagSystem system)
        {
            this.system = system;
            this.knownHashes = new HashSetCache<UInt256>(10);// BlockChain.Singleton.MemPool.Capacity * 2 / 5);
        }

        private void OnHeaderTaskCompleted()
        {
            if (!sessions.TryGetValue(Sender, out TaskSession session))
                return;
            session.Tasks.Remove(HeaderTaskHash);
            DecrementGlobalTask(HeaderTaskHash);
            RequestTasks(session);
        }

        private void OnNewTasks(InvPayload payload)
        {/*
            if (!sessions.TryGetValue(Sender, out TaskSession session))
                return;
            // Do not accept payload of type InventoryType.TX if not synced on best known HeaderHeight
            if (payload.Type == InventoryType.TX && BlockChain.Singleton.Height < BlockChain.Singleton.HeaderHeight)
            {
                RequestTasks(session);
                return;
            }
            HashSet<UInt256> hashes = new HashSet<UInt256>(payload.Hashes);
            // Remove all previously processed knownHashes from the list that is being requested
            hashes.Remove(knownHashes);
            // Add to AvailableTasks the ones, of type InventoryType.Block, that are global (already under process by other sessions)
            if (payload.Type == InventoryType.Block)
                session.AvailableTasks.UnionWith(hashes.Where(p => globalTasks.ContainsKey(p)));

            // Remove those that are already in process by other sessions
            hashes.Remove(globalTasks);
            if (hashes.Count == 0)
            {
                RequestTasks(session);
                return;
            }

            // Update globalTasks with the ones that will be requested within this current session
            foreach (UInt256 hash in hashes)
            {
                IncrementGlobalTask(hash);
                session.Tasks[hash] = DateTime.UtcNow;
            }

            foreach (InvPayload group in InvPayload.CreateGroup(payload.Type, hashes.ToArray()))
                Sender.Tell(Message.Create(MessageCommand.GetData, group));*/
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case Register register:
                    OnRegister(register.Version);
                    break;
                case Update update:
                    OnUpdate(update.LastBlockIndex);
                    break;
                case NewTasks tasks:
                    OnNewTasks(tasks.Payload);
                    break;
                case TaskCompleted completed:
                    OnTaskCompleted(completed.Hash);
                    break;
                case HeaderTaskCompleted _:
                    OnHeaderTaskCompleted();
                    break;
                case RestartTasks restart:
                    OnRestartTasks(restart.Payload);
                    break;
                case Timer _:
                    OnTimer();
                    break;
                case Terminated terminated:
                    OnTerminated(terminated.ActorRef);
                    break;
            }
        }

        private void OnRegister(VersionPayload version)
        {
            Context.Watch(Sender);
            TaskSession session = new TaskSession(Sender, version);
            sessions.Add(Sender, session);
            RequestTasks(session);
        }

        private void OnUpdate(uint lastBlockIndex)
        {
            if (!sessions.TryGetValue(Sender, out TaskSession session))
                return;
            session.LastBlockIndex = lastBlockIndex;
        }

        private void OnRestartTasks(InvPayload payload)
        {
            knownHashes.ExceptWith(payload.Hashes);
            foreach (UInt256 hash in payload.Hashes)
                globalTasks.Remove(hash);
            foreach (InvPayload group in InvPayload.CreateGroup(payload.Type, payload.Hashes))
                system.LocalNode.Tell(Message.Create(MessageCommand.GetData, group));
        }

        private void OnTaskCompleted(UInt256 hash)
        {
            knownHashes.Add(hash);
            globalTasks.Remove(hash);
            foreach (TaskSession ms in sessions.Values)
                ms.AvailableTasks.Remove(hash);
            if (sessions.TryGetValue(Sender, out TaskSession session))
            {
                session.Tasks.Remove(hash);
                RequestTasks(session);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecrementGlobalTask(UInt256 hash)
        {
            if (globalTasks.TryGetValue(hash, out var value))
            {
                if (value == 1)
                    globalTasks.Remove(hash);
                else
                    globalTasks[hash] = value - 1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IncrementGlobalTask(UInt256 hash)
        {
            if (!globalTasks.TryGetValue(hash, out var value))
            {
                globalTasks[hash] = 1;
                return true;
            }
            if (value >= MaxConncurrentTasks)
                return false;

            globalTasks[hash] = value + 1;

            return true;
        }

        private void OnTerminated(IActorRef actor)
        {
            if (!sessions.TryGetValue(actor, out TaskSession session))
                return;
            sessions.Remove(actor);
            foreach (UInt256 hash in session.Tasks.Keys)
                DecrementGlobalTask(hash);
        }

        private void OnTimer()
        {
            foreach (TaskSession session in sessions.Values)
                foreach (var task in session.Tasks.ToArray())
                    if (DateTime.UtcNow - task.Value > TaskTimeout)
                    {
                        if (session.Tasks.Remove(task.Key))
                            DecrementGlobalTask(task.Key);
                    }
            foreach (TaskSession session in sessions.Values)
                RequestTasks(session);
        }

        protected override void PostStop()
        {
            timer.CancelIfNotNull();
            base.PostStop();
        }

        public static Props Props(DagSystem system)
        {
            return Akka.Actor.Props.Create(() => new TaskManager(system)).WithMailbox("task-manager-mailbox");
        }

        private void RequestTasks(TaskSession session)
        {
 /*           if (session.HasTask) return;
            // If there are pending tasks of InventoryType.Block we should process them
            if (session.AvailableTasks.Count > 0)
            {
                session.AvailableTasks.Remove(knownHashes);
                // Search any similar hash that is on Singleton's knowledge, which means, on the way or already processed
                session.AvailableTasks.RemoveWhere(p => BlockChain.Singleton.ContainsBlock(p));
                HashSet<UInt256> hashes = new HashSet<UInt256>(session.AvailableTasks);
                if (hashes.Count > 0)
                {
                    foreach (UInt256 hash in hashes.ToArray())
                    {
                        if (!IncrementGlobalTask(hash))
                            hashes.Remove(hash);
                    }
                    session.AvailableTasks.Remove(hashes);
                    foreach (UInt256 hash in hashes)
                        session.Tasks[hash] = DateTime.UtcNow;
                    foreach (InvPayload group in InvPayload.CreateGroup(InventoryType.Block, hashes.ToArray()))
                        session.RemoteNode.Tell(Message.Create(MessageCommand.GetData, group));
                    return;
                }
            }

            // When the number of AvailableTasks is no more than 0, no pending tasks of InventoryType.Block, it should process pending the tasks of headers
            // If not HeaderTask pending to be processed it should ask for more Blocks
            if ((!HasHeaderTask || globalTasks[HeaderTaskHash] < MaxConncurrentTasks) && BlockChain.Singleton.HeaderHeight < session.LastBlockIndex)
            {
                session.Tasks[HeaderTaskHash] = DateTime.UtcNow;
                IncrementGlobalTask(HeaderTaskHash);
                session.RemoteNode.Tell(Message.Create(MessageCommand.GetHeaders, GetBlocksPayload.Create(BlockChain.Singleton.CurrentHeaderHash)));
            }
            else if (BlockChain.Singleton.Height < session.LastBlockIndex)
            {
                UInt256 hash = BlockChain.Singleton.CurrentBlockHash;
                for (uint i = BlockChain.Singleton.Height + 1; i <= BlockChain.Singleton.HeaderHeight; i++)
                {
                    hash = BlockChain.Singleton.GetBlockHash(i);
                    if (!globalTasks.ContainsKey(hash))
                    {
                        hash = BlockChain.Singleton.GetBlockHash(i - 1);
                        break;
                    }
                }
                session.RemoteNode.Tell(Message.Create(MessageCommand.GetBlocks, GetBlocksPayload.Create(hash)));
            }
            else if (BlockChain.Singleton.HeaderHeight >= session.LastBlockIndex
                    && TimeProvider.Current.UtcNow.ToTimestamp() - PingCoolingOffPeriod >= BlockChain.Singleton.GetBlock(BlockChain.Singleton.CurrentHeaderHash)?.Timestamp)
            {
                session.RemoteNode.Tell(Message.Create(MessageCommand.Ping, PingPayload.Create(BlockChain.Singleton.Height)));
            }*/
        }
    }

    internal class TaskManagerMailbox : PriorityMailbox
    {
        public TaskManagerMailbox(Akka.Actor.Settings settings, Config config)
            : base(settings, config)
        {
        }

        internal protected override bool IsHighPriority(object message)
        {
            switch (message)
            {
                case TaskManager.Register _:
                case TaskManager.Update _:
                case TaskManager.RestartTasks _:
                    return true;
                case TaskManager.NewTasks tasks:
                    if (tasks.Payload.Type == InventoryType.Block || tasks.Payload.Type == InventoryType.Consensus)
                        return true;
                    return false;
                default:
                    return false;
            }
        }

        internal protected override bool ShallDrop(object message, IEnumerable queue)
        {
            if (!(message is TaskManager.NewTasks tasks)) return false;
            // Remove duplicate tasks
            if (queue.OfType<TaskManager.NewTasks>().Any(x => x.Payload.Type == tasks.Payload.Type && x.Payload.Hashes.SequenceEqual(tasks.Payload.Hashes))) return true;
            return false;
        }
    }
}
