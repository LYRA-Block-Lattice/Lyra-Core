using Lyra.Core.API;
using Lyra.Core.Utils;
using Microsoft.Extensions.Logging;
using Neo;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize
{
    public class ConsensusHandlerBase
    {
        protected ConsensusService _context;
        protected ILogger _log;

        public DateTime TimeStarted { get; set; }

        protected ConcurrentQueue<ConsensusMessage> _outOfOrderedMessages;

        public ConsensusHandlerBase(ConsensusService context)
        {
            _log = new SimpleLogger("ConsensusHandlerBase").Logger;
            _context = context;

            _outOfOrderedMessages = new ConcurrentQueue<ConsensusMessage>();
        }

        public virtual bool CheckTimeout()
        {
            if (DateTime.Now - TimeStarted > TimeSpan.FromSeconds(LyraGlobal.CONSENSUS_TIMEOUT))
            {
                _log.LogInformation($"Consensus begin {TimeStarted} Ends: {DateTime.Now} used: {DateTime.Now - TimeStarted}");
                return true;
            }
            else
                return false;
        }

        public async virtual Task ProcessMessageAsync(ConsensusMessage msg)
        {
            //_log.LogInformation($"ProcessMessage {msg.MsgType} _state is null? {!IsStateCreated()}");
            if(msg is AuthorizingMsg || msg is ViewChangeRequestMessage)
            {
                await InternalProcessMessageAsync(msg);

                if(IsStateCreated())
                {
                    await ProcessQueueAsync();
                }
            }
            else if(!IsStateCreated())
            {
                _outOfOrderedMessages.Enqueue(msg);
            }
            else
            {
                await InternalProcessMessageAsync(msg);
                await ProcessQueueAsync();
            }
        }

        protected async Task ProcessQueueAsync()
        {
            while (_outOfOrderedMessages.Count > 0)
            {
                if (_outOfOrderedMessages.TryDequeue(out ConsensusMessage msg1))
                    await InternalProcessMessageAsync(msg1);
                else
                    await Task.Delay(10);
            }
        }

        protected virtual bool IsStateCreated()
        {
            return false;
        }

        protected virtual Task InternalProcessMessageAsync(ConsensusMessage msg)
        {
            throw new InvalidOperationException("Must override.");
        }
    }
}
