using System;
using System.Threading.Tasks;
using Lyra.Core.API;
using Microsoft.AspNetCore.Mvc;
using StreamJsonRpc;

#pragma warning disable VSTHRD200

namespace Lyra.Node
{
    /* versioning: https://exceptionnotfound.net/overview-of-api-versioning-in-asp-net-core-3-0/
     */
    [ApiVersion("1")]
    [ApiVersion("2")]
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class SocketController : Controller
    {
        INodeAPI _node;
        INodeTransactionAPI _trans;
        public SocketController(INodeAPI node, INodeTransactionAPI trans)
        {
            _node = node;
            _trans = trans;
        }
        public async Task<IActionResult> Index(ApiVersion apiVersion)
        {
            if (this.HttpContext.WebSockets.IsWebSocketRequest)
            {
                try
                {
                    var socket = await this.HttpContext.WebSockets.AcceptWebSocketAsync();

                    using (JsonRpcServerBase svr = apiVersion.MajorVersion == 1 ?
                        new JsonRpcServer(_node, _trans) as JsonRpcServerBase :
                        new JsonRpcServerV2(_node, _trans) as JsonRpcServerBase)
                    using (var jsonRpc = new JsonRpc(new WebSocketMessageHandler(socket), svr))
                    {
                        svr.RPC = jsonRpc;
                        jsonRpc.CancelLocallyInvokedMethodsWhenConnectionIsClosed = true;
                        jsonRpc.StartListening();
                        try
                        {
                            await jsonRpc.Completion;
                        }
                        catch { }
                    }
                }
                catch(Exception ex)
                {

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
