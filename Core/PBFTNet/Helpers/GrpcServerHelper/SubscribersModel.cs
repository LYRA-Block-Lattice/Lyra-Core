using Grpc.Core;

namespace GrpcServerHelper
{
    public class SubscribersModel<TResponse>
    {
        public IServerStreamWriter<TResponse> Subscriber { get; set; }

        public string Id { get; set; }
    }
}





