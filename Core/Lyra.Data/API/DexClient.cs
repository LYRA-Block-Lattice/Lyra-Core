using DexServer.Ext;
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
    public class DexClient : WebApiClientBase
    {
        public DexClient(string networkid)
        {
            if (networkid == "devnet")
                UrlBase = "https://dex.devnet.lyra.live:7010/api/Dex/";
            else if (networkid == "testnet")
                UrlBase = "https://dextestnet.lyra.live/api/Dex/";
            else
                UrlBase = "https://dex.lyra.live/api/Dex/";
        }

        public async Task<DexStatus> GetDexStatusAsync(string accountId, string signature)
        {
            var args = new Dictionary<string, string>
            {
                { "accountId", accountId },
                { "signature", signature },
            };
            return await GetAsync<DexStatus>("GetDexStatus", args);
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

    public class UserStats
    {
        public string AccountId { get; set; } = null!;
        public string UserName { get; set; } = null!;
        public int Total { get; set; }
        public decimal Ratio { get; set; }
        public bool TelegramVerified { get; set; } = false;
        public bool EmailVerified { get; set; } = false;
    }
}
