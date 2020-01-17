using Microsoft.Extensions.Logging;
using GrpcServerHelper;
using Communication;

namespace Lyra.Node2
{
    public class ServerGrpcSubscribers : ServerGrpcSubscribersBase<ResponseMessage>
    {
        public ServerGrpcSubscribers(ILoggerFactory loggerFactory) 
            : base(loggerFactory)
        {
        }

        public override bool Filter(SubscribersModel<ResponseMessage> subscriber, ResponseMessage message) => true;
    }
}
