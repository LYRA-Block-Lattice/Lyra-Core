using Microsoft.Extensions.Logging;

namespace GrpcServerHelper
{
    public abstract class MessageProcessorBase<TRequest, TResponse>
    {
        protected ILogger Logger { get; set; }

        public MessageProcessorBase(ILoggerFactory loggerFactory)
        {
            Logger = loggerFactory.CreateLogger<MessageProcessorBase<TRequest, TResponse>>();
        }

        public abstract string GetClientId(TRequest message);

        public abstract TResponse Process(TRequest message);
    }
}

