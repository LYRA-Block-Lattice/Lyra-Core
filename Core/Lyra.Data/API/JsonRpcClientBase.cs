﻿using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lyra.Core.API
{
    public class PoolInfo
    {
        public string poolId { get; set; }
        public string token0 { get; set; }
        public string token1 { get; set; }

        public Dictionary<string, decimal> balance { get; set; }
    }
    public class BalanceResult
    {
        public Dictionary<string, decimal> balance { get; set; }
        public bool unreceived { get; set; }
    }
    public class ApiStatus
    {
        public string version { get; set; }
        public string networkid { get; set; }
        public bool synced { get; set; }
    }
    public class Receiving
    {
        public string from { get; set; }
        public string sendHash { get; set; }
        public Dictionary<string, decimal> funds { get; set; }
    }

    public delegate void ReceivingEventHandler(Receiving recvMsg);
    public class JsonRpcClientBase
    {
        public event ReceivingEventHandler OnReceiving;
        protected virtual void RecvNotify(JObject notifyObj)
        {
            OnReceiving?.Invoke(notifyObj.ToObject<Receiving>());
        }
        protected virtual string SignMessage(string message)
        {
            throw new Exception("SignMessage Must be override.");
        }
        public async Task TestProcAsync(Func<JsonRpc, CancellationToken, Task> mainFunc)
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

                        jsonRpc.AddLocalRpcMethod("Notify", new Action<JObject>(
                            (recving) =>
                            {
                                RecvNotify(recving);
                            }
                        ));

                        jsonRpc.StartListening();

                        await mainFunc(jsonRpc, cancellationToken);

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