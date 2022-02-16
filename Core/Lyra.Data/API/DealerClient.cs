using DexServer.Ext;
using Lyra.Core.API;
using Lyra.Data.Crypto;
using Newtonsoft.Json;
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
    public class DealerClient : WebApiClientBase
    {
        public DealerClient(string networkid)
        {
            if (networkid == "devnet")
                UrlBase = "https://192.168.3.91:7070/api/Dealer/";
            else if (networkid == "testnet")
                UrlBase = "https://dealertestnet.lyra.live/api/Dealer/";
            else
                UrlBase = "https://dealer.lyra.live/api/Dealer/";
        }

        public async Task<Dictionary<string, decimal>> GetPricesAsync()
        {
            var result = await GetAsync<SimpleJsonAPIResult>("GetPrices");
            if (result.Successful())
                return JsonConvert.DeserializeObject<Dictionary<string, decimal>>(result.JsonString);
            else
                throw new Exception($"Error GetPricesAsync: {result.ResultCode}, {result.ResultMessage}");
        }

        public async Task<SimpleJsonAPIResult> GetUserByAccountIdAsync(string accountId)
        {
            var args = new Dictionary<string, string>
            {
                { "accountId", accountId },
            };
            return await GetAsync<SimpleJsonAPIResult>("GetUserByAccountId", args);
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
        }

        public async Task<ImageUploadResult> UploadImageAsync(string accountId, string signature, string tradeId, 
            string fileName, byte[] imageData, string contentType)
        {
            var form = new MultipartFormDataContent {
                {
                    new ByteArrayContent(imageData),
                    "file",
                    fileName
                },
                {
                    new StringContent(accountId), "accountId"
                },
                {
                    new StringContent(signature), "signature"
                },
                {
                    new StringContent(tradeId), "tradeId"
                },
            };
            var postResponse = await PostRawAsync<ImageUploadResult>("UploadFile", form);
            return postResponse;
        }
    }
}
