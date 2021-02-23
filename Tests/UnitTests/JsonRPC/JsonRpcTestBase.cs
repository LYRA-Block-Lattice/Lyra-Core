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
    public class JsonRpcTestBase
    {
        protected virtual string SignMessage(string message)
        {
            throw new Exception("SignMessage Must be override.");
        }
        public async Task TestProcAsync(Func<JsonRpc, CancellationToken, Task> testFunc)
        {
            var cancellationTokenSrc = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSrc.Token;
            using (var socket = new ClientWebSocket())
            {
                socket.Options.RemoteCertificateValidationCallback = (a, b, c, d) => true;
                await socket.ConnectAsync(new Uri("wss://api.devnet:4504/api/v1/socket"), cancellationToken);

                using (var jsonRpc = new JsonRpc(new WebSocketMessageHandler(socket)))
                {
                    try
                    {
                        jsonRpc.AddLocalRpcMethod("Sign", new Func<string, string>(
                            (msg) =>
                            {
                                return SignMessage(msg);
                            }
                        ));

                        jsonRpc.StartListening();

                        await testFunc(jsonRpc, cancellationToken);

                        cancellationTokenSrc.Cancel();
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
