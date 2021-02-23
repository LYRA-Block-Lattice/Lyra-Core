using System.Threading.Tasks;
using Lyra.Core.API;
using Microsoft.AspNetCore.Mvc;
using StreamJsonRpc;

#pragma warning disable VSTHRD200

namespace Lyra.Node
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class SocketController : Controller
    {
        INodeAPI _node;
        public SocketController(INodeAPI node)
        {
            _node = node;
        }
        public async Task<IActionResult> Index()
        {
            if (this.HttpContext.WebSockets.IsWebSocketRequest)
            {
                var socket = await this.HttpContext.WebSockets.AcceptWebSocketAsync();
                using (var jsonRpc = new JsonRpc(new WebSocketMessageHandler(socket), new JsonRpcServer(_node)))
                {
                    jsonRpc.CancelLocallyInvokedMethodsWhenConnectionIsClosed = true;
                    jsonRpc.StartListening();
                    try
                    {
                        await jsonRpc.Completion;
                    }
                    catch { }
                }

                return new EmptyResult();
            }
            else
            {
                return new BadRequestResult();
            }
        }
    }
}
