using DexServer.Ext;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.API.Identity;
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
    public class CommentConfig : SignableObject
    {
        public string ByAccountId { get; set; } = null!;
        public string TradeId { get; set; } = null!;
        public DateTime Created { get; set; }
        public int Rating { get; set; }
        public string Content { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string EncContent { get; set; } = null!;
        public string EncTitle { get; set; } = null!;
        public bool Confirm { get; set; }

        public override string GetHashInput()
        {
            if (EncContent == null || EncTitle == null)
                throw new ArgumentNullException();

            return $"{TradeId}|{DateTimeToString(Created)}{Rating}|{ByAccountId}|{EncTitle}|{EncContent}";
        }

        protected override string GetExtraData()
        {
            return "";
        }
    }

    public class FiatInfo
    {
        public string symbol { get; set; } = null!;
        public string name { get; set; } = null!;
        public string unit { get; set; } = null!;
    }

    public class DealerBrief
    {
        public string AccountId { get; set; } = null!;
        public string TelegramBotUsername { get; set; } = null!;
    }

    /// <summary>
    /// App bound to a single dealer server at one time.
    /// App can swith dealer server, all things will changes, prices feed, realtime notification on various changes, etc.
    /// Lyra team provides a generic permissionless dealer by default. 
    /// </summary>
    public class DealerClient : WebApiClientBase
    {
        public DealerClient(Uri endpointUri)
        {
            UrlBase = endpointUri.ToString();
        }

        public async Task<DealerBrief?> GetBriefAsync()
        {
            var result = await GetAsync<SimpleJsonAPIResult>("GetBrief");
            if (result.Successful())
                return JsonConvert.DeserializeObject<DealerBrief>(result.JsonString);
            else
                throw new Exception($"Error GetBriefAsync: {result.ResultCode}, {result.ResultMessage}");
        }

        public async Task<Dictionary<string, decimal>> GetPricesAsync()
        {
            var result = await GetAsync<SimpleJsonAPIResult>("GetPrices");
            if (result.Successful())
                return JsonConvert.DeserializeObject<Dictionary<string, decimal>>(result.JsonString)!;
            else
                throw new Exception($"Error GetPricesAsync: {result.ResultCode}, {result.ResultMessage}");
        }

        public async Task<FiatInfo?> GetFiatAsync(string symbol)
        {
            var args = new Dictionary<string, string>
            {
                { "symbol", symbol },
            };
            var fiat = await GetAsync<SimpleJsonAPIResult>("GetFiat", args);
            if (fiat.Successful() && fiat.JsonString != "null")
                return JsonConvert.DeserializeObject<FiatInfo>(fiat.JsonString)!;
            
            return null;
        }

        public async Task<SimpleJsonAPIResult> GetUserByAccountIdAsync(string accountId)
        {
            var args = new Dictionary<string, string>
            {
                { "accountId", accountId },
            };
            return await GetAsync<SimpleJsonAPIResult>("GetUserByAccountId", args);
        }

        /// <summary>
        /// get full user details. only owner can do this.
        /// </summary>
        /// <param name="accountId"></param>
        /// <param name="signature"></param>
        /// <returns></returns>
        public async Task<LyraUser?> GetUserDetailsByAccountIdAsync(string accountId, string signature)
        {
            var args = new Dictionary<string, string>
            {
                { "accountId", accountId },
                { "signature", signature },
            };
            var ret = await GetAsync<SimpleJsonAPIResult>("GetUserDetailsByAccountId", args);
            if (ret.Successful())
                return ret.Deserialize<LyraUser>();
            else
                return null;
        }

        public async Task<APIResult> RegisterAsync(string accountId,
            string userName, string firstName, string middleName, string lastName,
            string email, string mibilePhone, string avatarId, string telegramID, string signature
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
                { "telegramId", telegramID },
                { "signature", signature },
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

        public async Task<SimpleJsonAPIResult> GetTradeBriefAsync(string tradeId, string accountId, string signature)
        {
            var args = new Dictionary<string, string>
            {
                { "tradeId", tradeId },
                { "accountId", accountId },
                { "signature", signature },
            };
            return await GetAsync<SimpleJsonAPIResult>("GetTradeBrief", args);
        }

        public async Task<APIResult> CommentTradeAsync(CommentConfig cfg)
        {
            return await PostAsync("CommentTrade", cfg);
        }

        public async Task<List<CommentConfig>?> GetCommentsForTradeAsync(string tradeId)
        {
            var args = new Dictionary<string, string>
            {
                { "tradeId", tradeId },
            };
            var ret = await GetAsync<SimpleJsonAPIResult>("GetCommentsForTrade", args);
            if(ret.Successful())
            {
                var cmnts = ret.Deserialize<List<CommentConfig>>();
                return cmnts;
            }

            return new List<CommentConfig>();
        }

        public async Task<APIResult> ComplainAsync(string tradeId, decimal claimedLost, string accountId, string signature)
        {
            var args = new Dictionary<string, string>
            {
                { "tradeId", tradeId },
                { "accountId", accountId },
                { "signature", signature },
                { "claimedLost", claimedLost.ToString() },
            };
            return await GetAsync<APIResult>("Complain", args);
        }
    }
}
