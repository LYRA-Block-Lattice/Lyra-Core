using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Threading;
using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTests.JsonRPC
{
    [TestClass]
    public class UT_Handshake
    {

        [TestMethod]
        public async Task HelloToNodeAsync()
        {
            var cancellationTokenSrc = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSrc.Token;
            using (var socket = new ClientWebSocket())
            {
                socket.Options.RemoteCertificateValidationCallback = (a, b, c, d) => true;

                await socket.ConnectAsync(new Uri("wss://api.devnet:4504/api/socket"), cancellationToken);
                Console.WriteLine("Connected to web socket. Establishing JSON-RPC protocol...");
                using (var jsonRpc = new JsonRpc(new WebSocketMessageHandler(socket)))
                {
                    try
                    {
                        jsonRpc.AddLocalRpcMethod("Tick", new Action<int>(tick => Console.WriteLine($"Tick {tick}!")));
                        jsonRpc.StartListening();
                        Console.WriteLine("JSON-RPC protocol over web socket established.");
                        int result = await jsonRpc.InvokeWithCancellationAsync<int>("Add", new object[] { 1, 2 }, cancellationToken);
                        Console.WriteLine($"JSON-RPC server says 1 + 2 = {result}");

                        // Request notifications from the server.
                        await jsonRpc.NotifyAsync("SendTicksAsync");

                        _ = Task.Run(async () => {
                            await Task.Delay(5000);
                            cancellationTokenSrc.Cancel();
                        });
                        await jsonRpc.Completion.WithCancellation(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Closing is initiated by Ctrl+C on the client.
                        // Close the web socket gracefully -- before JsonRpc is disposed to avoid the socket going into an aborted state.
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None);
                        //throw;
                    }
                    catch
                    {
                        throw;
                    }
                }
            }
        }
    }
}
