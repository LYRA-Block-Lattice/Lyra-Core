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
        INodeTransactionAPI _trans;
        public SocketController(INodeAPI node, INodeTransactionAPI trans)
        {
            _node = node;
            _trans = trans;
        }
        public async Task<IActionResult> Index()
        {
            if (this.HttpContext.WebSockets.IsWebSocketRequest)
            {
                var socket = await this.HttpContext.WebSockets.AcceptWebSocketAsync();
                using (var jsonRpc = new JsonRpc(new WebSocketMessageHandler(socket), new JsonRpcServer(_node, _trans)))
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
