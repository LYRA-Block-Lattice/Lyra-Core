using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using AustinHarris.JsonRpc;
using Newtonsoft.Json;

namespace Client.CLI
{
    internal class RPCServer : IDisposable
    {
        private static readonly HttpListener Server = new HttpListener();
        private static string Url = "http://localhost:3373/"; //$"http://{Settings.Default.Host}:{Settings.Default.Port}/";

        public static Dictionary<Guid, Tuple<HttpListenerWebSocketContext, Guid?>> Clients = new Dictionary<Guid, Tuple<HttpListenerWebSocketContext, Guid?>>();

        public RPCServer(string bindingUrl)
        {
            var timeoutMinutes = 1;// Settings.Default.SyncTimeoutMinutes;

            if(bindingUrl != null)
                Url = bindingUrl;

            Server.Prefixes.Add(Url);

            try
            {
                Server.Start();

                for (int i = 0; i < 10; i++)
                {
                    ReceiveConnection().ContinueWith(LogExceptions, TaskContinuationOptions.OnlyOnFaulted);
                }
            }
            catch (HttpListenerException ex)
            {
            }
        }

        private static async Task ReceiveConnection()
        {
            Guid connectionId = Guid.NewGuid();
            CancellationTokenSource cancelTokenSource = new CancellationTokenSource();
            CancellationToken cancelToken = cancelTokenSource.Token;

            try
            {

                HttpListenerContext context = await
                    Server.GetContextAsync();

                if (context.Request.IsWebSocketRequest)
                {

                    HttpListenerWebSocketContext webSocketContext =
                        await context.AcceptWebSocketAsync(null, new TimeSpan(0, 0, 15));

                    Clients.Add(connectionId, new Tuple<HttpListenerWebSocketContext, Guid?>(webSocketContext, null));

                    WebSocket webSocket = webSocketContext.WebSocket;

                    const int maxMessageSize = 4096;
                    byte[] receiveBuffer = new byte[maxMessageSize];

                    while (webSocket.State == WebSocketState.Open)
                    {

                        WebSocketReceiveResult receiveResult =
                            await webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);
                        if (receiveResult.MessageType == WebSocketMessageType.Close)
                        {
                            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty,
                                CancellationToken.None);
                        }
                        else if (receiveResult.MessageType == WebSocketMessageType.Binary)
                        {

                        }
                        else
                        {
                            int count = receiveResult.Count;

                            while (receiveResult.EndOfMessage == false)
                            {
                                if (count >= maxMessageSize)
                                {
                                    string closeMessage = $"Maximum message size: {maxMessageSize} bytes.";
                                    await webSocket.CloseAsync(WebSocketCloseStatus.MessageTooBig, closeMessage,
                                        CancellationToken.None);
                                    return;
                                }

                                receiveResult = await webSocket.ReceiveAsync(
                                    new ArraySegment<byte>(receiveBuffer, count, maxMessageSize - count),
                                    CancellationToken.None);
                                count += receiveResult.Count;
                            }

                            var message = Encoding.UTF8.GetString(receiveBuffer, 0, count);

                            if (message.Contains("method"))
                            {
                                string returnString = await JsonRpcProcessor.Process(message, connectionId);
                                if (returnString.Length != 0)
                                {
                                    ArraySegment<byte> outputBuffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(returnString));
                                    if (webSocket.State == WebSocketState.Open)
                                    {
                                        await webSocket.SendAsync(outputBuffer, WebSocketMessageType.Text, true, cancelToken);

                                    }
                                    else
                                    {
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {

                    HttpListenerResponse response = context.Response;
                    StringBuilder message = new StringBuilder();
                    message.Append("<HTML><BODY>");
                    message.Append("<p>HTTP NOT ALLOWED</p>");
                    message.Append("</BODY></HTML>");
                    string message403 = message.ToString();

                    // Turn the error message into a byte array using the
                    // encoding from the response when present.
                    Encoding encoding = response.ContentEncoding;
                    if (encoding == null)
                    {
                        encoding = Encoding.UTF8;
                        response.ContentEncoding = encoding;
                    }

                    byte[] buffer = encoding.GetBytes(message403);
                    response.ContentLength64 = buffer.Length;
                    response.StatusCode = 403;
                    // Write the error message.
                    Stream stream = response.OutputStream;
                    stream.Write(buffer, 0, buffer.Length);
                    // Send the response.
                    response.Close();
                }
            }
            catch (HttpListenerException ex)
            {
            }
            catch (Exception ex)
            {
            }
            finally
            {
                cancelTokenSource.Cancel();
                Clients.Remove(connectionId);
                await ReceiveConnection().ContinueWith(LogExceptions, TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        public static Guid? CheckAuthorization(Guid connectionId)
        {
            if (!Clients.TryGetValue(connectionId, out var client))
                return null;


            return client.Item2;
        }


        public static async Task Notify(string rpcMethod, object rpcParams)
        {
            JsonNotification request = new JsonNotification
            {
                Method = rpcMethod,
                Params = rpcParams
            };

            string notification = JsonConvert.SerializeObject(request);

            foreach (var client in Clients)
            {
                ArraySegment<byte> outputBuffer =
                    new ArraySegment<byte>(Encoding.UTF8.GetBytes(notification));

                var context = client.Value.Item1;
                if (context.WebSocket.State == WebSocketState.Open)
                {
                    try
                    {
                        await client.Value.Item1.WebSocket.SendAsync(outputBuffer, WebSocketMessageType.Text, true,
                            CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                    }
                }
            }
        }

        public static async Task NotifyClient(Guid clientId, string rpcMethod, object rpcParams)
        {
            JsonNotification request = new JsonNotification
            {
                Method = rpcMethod,
                Params = rpcParams
            };

            string notification = JsonConvert.SerializeObject(request);

            foreach (var client in Clients.Where(p => p.Key == clientId))
            {
                ArraySegment<byte> outputBuffer =
                                    new ArraySegment<byte>(Encoding.UTF8.GetBytes(notification));
                var context = client.Value.Item1;
                if (context.WebSocket.State == WebSocketState.Open)
                {
                    try
                    {
                        await client.Value.Item1.WebSocket.SendAsync(outputBuffer, WebSocketMessageType.Text, true,
                            CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                    }
                }
            }
        }

        public static void LogExceptions(Task task)
        {
            if (task.Exception != null)
            {
                var aggException = task.Exception.Flatten();

            }
        }

        public void Dispose()
        {

        }
    }

    internal class JsonNotification
    {
        public JsonNotification() { }

        [JsonProperty("jsonrpc")]
        public string JsonRpc => "2.0";

        [JsonProperty("method")]
        public string Method { get; set; }

        [JsonProperty("params", NullValueHandling = NullValueHandling.Ignore)]
        public object Params { get; set; }
    }
}