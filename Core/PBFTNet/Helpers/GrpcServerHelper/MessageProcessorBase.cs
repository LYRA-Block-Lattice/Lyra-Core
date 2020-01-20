using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

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

        public abstract Task<TResponse> ProcessAsync(TRequest message);
    }
}

