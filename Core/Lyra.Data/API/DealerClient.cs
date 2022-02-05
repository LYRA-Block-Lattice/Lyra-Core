using DexServer.Ext;
using Lyra.Core.API;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lyra.Data.API
{
    public class DealerClient
    {
        string _url;
        private CancellationTokenSource _cancel;
        public DealerClient(string networkid)
        {
            _cancel = new CancellationTokenSource();

            if (networkid == "devnet")
                _url = "https://192.168.3.91:7070/api/Dealer/";
            else if (networkid == "testnet")
                _url = "https://dealertestnet.lyra.live/api/Dealer/";
            else
                _url = "https://dealer.lyra.live/api/Dealer/";
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
                BaseAddress = new Uri(_url),
                //_client.DefaultRequestHeaders.Accept.Clear();
                //_client.DefaultRequestHeaders.Accept.Add(
                //    new MediaTypeWithQualityHeaderValue("application/json"));
                //#if DEBUG
                //            _client.Timeout = new TimeSpan(0, 0, 30);
                //#else
                Timeout = new TimeSpan(0, 0, 15)
            };
            //#endif
            return client;
        }

        public void Abort()
        {
            _cancel.Cancel();
            _cancel.Dispose();
            _cancel = new CancellationTokenSource();
        }

        public async Task<APIResult> GetUserByAccountIdAsync(string accountId)
        {
            var args = new Dictionary<string, string>
            {
                { "accountId", accountId },
            };
            return await GetAsync<APIResult>("GetUserByAccountId", args);
        }

        public async Task<APIResult> RegisterAsync(string accountId,
            string userName, string firstName, string middleName, string lastName,
            string email, string mibilePhone, string avatarId
            )
        {
            var args = new Dictionary<string, string>
            {
                { "accountId", accountId },
                { "userName", userName },
                { "firstName", firstName },
                { "middleName", middleName },
                { "lastName", lastName },
                { "email", email },
                { "mibilePhone", mibilePhone },
                { "avatarId", avatarId },
            };
            return await GetAsync<APIResult>("Register", args);
            //return await PostBlockAsync<bool>("Register", user);
        }

        private async Task<T> PostBlockAsync<T>(string action, object obj)
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

        private async Task<T> GetAsync<T>(string action, Dictionary<string, string> args)
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
                throw new Exception($"Web Api Failed for {url}");
        }

        public async Task<DexAddress> CreateWalletAsync(string owner, string symbol, string provider,
            string reqhash,
            string authid, string signature)
        {
            var args = new Dictionary<string, string>
            {
                { "owner", owner },
                { "symbol", symbol },
                { "provider", provider },
                { "reqhash", reqhash },
                { "authid", authid },
                { "signature", signature },
            };
            return await GetAsync<DexAddress>("CreateWallet", args);
        }

        public async Task<DexResult> RequestWithdrawAsync(string owner, string symbol, string provider,
            string dexid, string reqhash,
            string address, long amountlong, 
            string authid, string signature)
        {
            var args = new Dictionary<string, string>
            {
                { "owner", owner },
                { "symbol", symbol },
                { "provider", provider },
                { "dexid", dexid },
                { "reqhash", reqhash },
                { "address", address },
                { "amountlong", amountlong.ToString() },
                { "authid", authid },
                { "signature", signature },
            };
            return await GetAsync<DexResult>("RequestWithdraw", args);
        }

        public async Task<SupportedTokens> GetSupportedExtTokenAsync(string networkid)
        {
            var args = new Dictionary<string, string>
            {
                { "networkid", networkid },
            };
            return await GetAsync<SupportedTokens>("GetSupportedExtToken", args);
        }
    }
}
