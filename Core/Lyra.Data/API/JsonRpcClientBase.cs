using Lyra.Data.API;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lyra.Core.API
{
    public class PoolInfo
    {
        public string poolId { get; set; }
        public long height { get; set; }
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

    public class News
    {
        public string catalog { get; set; }
        public object content { get; set; }
    }
    public class Receiving
    {
        public string from { get; set; }
        public string to { get; set; }
        public string sendHash { get; set; }
        public Dictionary<string, decimal> funds { get; set; }
    }

    public class Settlement : Receiving
    {
        public string recvHash { get; set; }
    }

    public class TxDesc
    {
        public long Height { get; set; }
        public bool IsReceive { get; set; }
        public long TimeStamp { get; set; }
        public string SendAccountId { get; set; }
        public string SendHash { get; set; }
        public string RecvAccountId { get; set; }
        public string RecvHash { get; set; }
        public Dictionary<string, string> Changes { get; set; }
        public Dictionary<string, string> Balances { get; set; }

        public TxDesc(TransactionDescription tx)
        {
            Height = tx.Height;
            IsReceive = tx.IsReceive;
            TimeStamp = (long)Math.Round(tx.TimeStamp
               .Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc))
               .TotalMilliseconds);
            SendAccountId = tx.SendAccountId;
            SendHash = tx.SendHash;
            RecvAccountId = tx.RecvAccountId;
            RecvHash = tx.RecvHash;
            Changes = tx.Changes.ToDictionary(k => k.Key, k => k.Value.ToBalanceDecimal().ToString());
            Balances = tx.Balances.ToDictionary(k => k.Key, k => k.Value.ToBalanceDecimal().ToString());
        }
    }

    public delegate void ReceivingEventHandler(Receiving recvMsg);
    public class JsonRpcClientBase
    {
        protected string NetworkId;
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

                var uri = new Uri(LyraGlobal.SelectNode(NetworkId));
                var wssUrl = $"wss://{uri.Host}:{uri.Port}/api/v1/socket";

                await socket.ConnectAsync(new Uri(wssUrl), cancellationToken);

                using (var jsonRpc = new JsonRpc(new WebSocketMessageHandler(socket)))
                {
                    try
                    {
                        jsonRpc.AddLocalRpcMethod("Sign", new Func<string, string, string[]>(
                            (type, msg) =>
                            {
                                return new string[] { "p1393", SignMessage(msg) };
                            }
                        ));

                        jsonRpc.AddLocalRpcMethod("Notify", new Action<JObject>(
                            (newsObj) =>
                            {
                                var news = newsObj.ToObject<News>();
                                if (news.catalog == "Receiving")
                                    RecvNotify(news.content as JObject);
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
