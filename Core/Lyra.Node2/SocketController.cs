﻿using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using StreamJsonRpc;

#pragma warning disable VSTHRD200

namespace Lyra.Node
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class SocketController : Controller
    {
        public async Task<IActionResult> Index()
        {
            if (this.HttpContext.WebSockets.IsWebSocketRequest)
            {
                var socket = await this.HttpContext.WebSockets.AcceptWebSocketAsync();
                using (var jsonRpc = new JsonRpc(new WebSocketMessageHandler(socket), new JsonRpcServer()))
                {
                    jsonRpc.CancelLocallyInvokedMethodsWhenConnectionIsClosed = true;
                    jsonRpc.StartListening();
                    await jsonRpc.Completion;
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
