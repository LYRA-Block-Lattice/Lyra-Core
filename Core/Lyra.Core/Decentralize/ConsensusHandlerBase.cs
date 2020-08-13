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

        protected ConsensusState _state;

        DateTime dtStart = DateTime.Now;

        protected ConcurrentQueue<ConsensusMessage> _outOfOrderedMessages;

        public ConsensusHandlerBase(ConsensusService context)
        {
            _log = new SimpleLogger("ConsensusHandlerBase").Logger;
            _context = context;

            _outOfOrderedMessages = new ConcurrentQueue<ConsensusMessage>();
        }

        public bool CheckTimeout()
        {
            if (DateTime.Now - dtStart > TimeSpan.FromSeconds(ProtocolSettings.Default.ConsensusTimeout))
            {
                return true;
            }
            else
                return false;
        }

        public async virtual Task ProcessMessage(ConsensusMessage msg)
        {
            if (_state == null && !(msg is AuthorizingMsg) && !(msg is ViewChangeRequestMessage))
            {
                _outOfOrderedMessages.Enqueue(msg);
            }
            else
            {
                await InternalProcessMessage(msg);

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

        protected virtual Task InternalProcessMessage(ConsensusMessage msg)
        {
            throw new InvalidOperationException("Must override.");
        }
    }
}
