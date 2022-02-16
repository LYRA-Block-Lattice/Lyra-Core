using DexServer.Ext;
using Lyra.Core.API;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lyra.Data.API
{
    public abstract class WebApiClientBase
    {
        protected string UrlBase { get; set; }
        private CancellationTokenSource _cancel;

        public WebApiClientBase()
        {
            _cancel = new CancellationTokenSource();
        }
        private HttpClient CreateClient()
        {
            var handler = new HttpClientHandler();
            try
            {
                handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, error) =>
                {
                    var cert2 = new X509Certificate2(cert.GetRawCertData());
                    //ServerThumbPrint = cert2.Thumbprint;
                    return true;
                };
            }
            catch { }

            var client = new HttpClient(handler)
            {
                BaseAddress = new Uri(UrlBase),
#if DEBUG
                Timeout = new TimeSpan(0, 5, 0)
#else
                Timeout = new TimeSpan(0, 0, 15)
#endif
        };
            
            return client;
        }

        public void Abort()
        {
            _cancel.Cancel();
            _cancel.Dispose();
            _cancel = new CancellationTokenSource();
        }

        private async Task<T> PostJsonAsync<T>(string action, object obj)
        {
            using var client = CreateClient();
            HttpResponseMessage response = await client.PostAsJsonAsync(
                action, obj, _cancel.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<T>();
                return result;
            }
            else
                throw new Exception("Web Api Failed.");
        }

        protected async Task<T> PostRawAsync<T>(string action, HttpContent obj)
        {
            using var client = CreateClient();
            HttpResponseMessage response = await client.PostAsync(
                action, obj, _cancel.Token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<T>();
                return result;
            }
            else
            {
                var resp = await response.Content.ReadAsStringAsync();
                throw new Exception($"Web Api Failed: {resp}");
            }                
        }

        protected async Task<T> GetAsync<T>(string action, Dictionary<string, string>? args = null)
        {
            var url = $"{action}/?" + args?.Aggregate(new StringBuilder(),
                          (sb, kvp) => sb.AppendFormat("{0}{1}={2}",
                                       sb.Length > 0 ? "&" : "", kvp.Key, kvp.Value),
                          sb => sb.ToString());

            using var client = CreateClient();
            HttpResponseMessage response = await client.GetAsync(url, _cancel.Token);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsAsync<T>();
                return result;
            }
            else
            {
                var resp = await response.Content.ReadAsStringAsync();
                throw new Exception($"Web Api Failed for {url}, {resp}");
            }                
        }

    }
}
