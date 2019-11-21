using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lyra.Core.API
{
    public class LyraRestNotify : INotifyAPI
    {
        private string _url;
        private HttpClient _client;
        public LyraRestNotify(string url)
        {
            _url = url;

            var httpClientHandler = new HttpClientHandler();
            // Return `true` to allow certificates that are untrusted/invalid
            httpClientHandler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            _client = new HttpClient(httpClientHandler);
            _client.Timeout = new TimeSpan(0, 5, 0);        // the api will hung. long-poll
            _client.BaseAddress = new Uri(url);
            _client.DefaultRequestHeaders.Accept.Clear();
            _client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<GetNotificationAPIResult> GetNotification(string AccountID, string Signature)
        {
            HttpResponseMessage response = await _client.GetAsync($"?AccountID={AccountID}&Signature={Signature}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<GetNotificationAPIResult>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        public async Task BeginReceiveNotifyAsync(string AccountID, string Signature, Action<NotifySource, string, string, string> action, CancellationToken cancel)
        {
            await Task.Factory.StartNew(async () => {
                while(true)
                {
                    if (cancel.IsCancellationRequested)
                        return;

                    try
                    {
                        var result = await GetNotification(AccountID, Signature);
                        if (result.ResultCode == Protos.APIResultCodes.Success && result.HasEvent)
                        {
                            Task.Run(() => action(result.Source, result.Action, result.Catalog, result.ExtraInfo));
                        }
                    }
                    catch(Exception ex)
                    {
                        await Task.Delay(5000);
                    }
                }
            }, cancel);
        }
    }
}
