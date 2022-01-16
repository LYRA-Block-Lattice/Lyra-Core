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
    public class SimpleResult
    {
        public bool success { get; set; }
        public string message { get; set; }
    }

    public class PendingInfo
    {
        public string accountid { get; set; }
        public decimal funds { get; set; }
        public decimal fees { get; set; }
    }

    public class BrokerAccountsInfo
    {
        public string owner { get; set; }
        public List<ProfitInfo> profits { get; set; }
        public List<StakingInfo> stakings { get; set; }
    }

    public class StakingInfo
    {
        public string name { get; set; }
        public string voting { get; set; }
        public string owner { get; set; }
        public string stkid { get; set; }
        public int days { get; set; }
        public DateTime start { get; set; }
        public decimal amount { get; set; }
        public bool compound { get; set; }
    }
    public class ProfitInfo
    {
        public string name { get; set; }
        public string type { get; set; }
        public decimal shareratio { get; set; }
        public int seats { get; set; }
        public string pftid { get; set; }
        public string owner { get; set; }
    }
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
        public long height { get; set; }
        public bool unreceived { get; set; }
    }

    public class SendResult
    {
        public Dictionary<string, decimal> balance { get; set; }
        public long height { get; set; }
        public bool unreceived { get; set; }
        public string txHash { get; set; }
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

    public class TxInfoBase
    {
        public string to { get; set; }
        public string sendHash { get; set; }
        public Dictionary<string, decimal> funds { get; set; }
    }
    public class Receiving : TxInfoBase
    {
        public string from { get; set; }
    }

    public class Settlement : TxInfoBase
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
            Changes = tx.Changes?.ToDictionary(k => k.Key, k => k.Value.ToBalanceDecimal().ToString());
            Balances = tx.Balances.ToDictionary(k => k.Key, k => k.Value.ToBalanceDecimal().ToString());
        }
    }

    public delegate void ReceivingEventHandler(Receiving recvMsg);
    public abstract class JsonRpcClientBase
    {
        protected string NetworkId;
        public event ReceivingEventHandler OnReceiving;
        protected virtual void RecvNotify(JObject notifyObj)
        {
            OnReceiving?.Invoke(notifyObj.ToObject<Receiving>());
        }
        protected abstract Task<string> SignMessageAsync(string message);

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
                        jsonRpc.AddLocalRpcMethod("Sign", new Func<string, string, string, Task<string[]>>(
                            async (type, msg, accountId) =>
                            {
                                var sign = await SignMessageAsync(msg);
                                return new string[] { "p1393",  sign};
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
