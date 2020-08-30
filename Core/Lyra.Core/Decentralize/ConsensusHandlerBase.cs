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

        protected DateTime _dtStart = DateTime.Now;

        protected ConcurrentQueue<ConsensusMessage> _outOfOrderedMessages;

        public ConsensusHandlerBase(ConsensusService context)
        {
            _log = new SimpleLogger("ConsensusHandlerBase").Logger;
            _context = context;

            _outOfOrderedMessages = new ConcurrentQueue<ConsensusMessage>();
        }

        public virtual bool CheckTimeout()
        {
            if (DateTime.Now - _dtStart > TimeSpan.FromSeconds(20))
            {
                return true;
            }
            else
                return false;
        }

        public async virtual Task ProcessMessage(ConsensusMessage msg)
        {
            _log.LogInformation($"ProcessMessage {msg.MsgType} _state is null? {!IsStateCreated()}");
            if(msg is AuthorizingMsg || msg is ViewChangeRequestMessage)
            {
                await InternalProcessMessage(msg);
                await ProcessQueueAsync();
            }
            else if(!IsStateCreated())
            {
                _outOfOrderedMessages.Enqueue(msg);
            }
            else
            {
                await InternalProcessMessage(msg);
                await ProcessQueueAsync();
            }
        }

        protected async Task ProcessQueueAsync()
        {
            while (_outOfOrderedMessages.Count > 0)
            {
                ConsensusMessage msg1;
                if (_outOfOrderedMessages.TryDequeue(out msg1))
                    await InternalProcessMessage(msg1);
                else
                    await Task.Delay(10);
            }
        }

        protected virtual bool IsStateCreated()
        {
            return false;
        }

        protected virtual Task InternalProcessMessage(ConsensusMessage msg)
        {
            throw new InvalidOperationException("Must override.");
        }
    }
}
