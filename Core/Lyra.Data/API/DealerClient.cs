using DexServer.Ext;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.API.Identity;
using Lyra.Data.API.ODR;
using Lyra.Data.Crypto;
using MessagePack.Formatters;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.Ocsp;
using Org.BouncyCastle.Asn1.X509.Qualified;
using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection.Emit;
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

    public enum ComplaintByRole { Buyer, Seller }
    public enum ComplaintRequest { CancelTrade, ContinueTrade, Arbitration }
    public enum ComplaintResponse { AgreeCancel, AgreeContinue, 
        RefuseCancel, RefuseContinue, 
        AgreeResolution, RefuseResolution }
    public enum ComplaintFiatStates { SelfUnpaid, SelfPaid, PeerUnpaid, PeerPaid }

    public abstract class ComplaintBase : SignableObject
    {
        public string ownerId { get; set; } = null!;
        public string tradeId { get; set; } = null!;
        public DateTime created { get; set; }
        public DisputeLevels level { get; set; }
        public ComplaintByRole role { get; set; }
        public ComplaintFiatStates fiatState { get; set; }
        public string statement { get; set; } = null!;
        public string[]? imageHashes { get; set; }

        public override string GetHashInput()
        {
            return $"{ownerId}|{tradeId}|{DateTimeToString(created)}|{level}|{role}|{fiatState}|" +
                    $"{Convert.ToBase64String(Encoding.UTF8.GetBytes(statement))}|" +
                    imageHashes?.Aggregate("", (a, b) => a + "," + b) ?? "";
        }

        protected override string GetExtraData()
        {
            return "";
        }
    }

    public class ComplaintClaim : ComplaintBase
    {
        public ComplaintRequest request { get; set; }

        protected override string GetExtraData()
        {
            return base.GetExtraData() + $"|{request}";
        }
    }

    public class ComplaintReply : ComplaintBase
    {
        public string complaintHash { get; set; } = null!;
        public ComplaintResponse response { get; set; }
        protected override string GetExtraData()
        {
            return base.GetExtraData() + $"|{complaintHash}|{response}";
        }
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

        public async Task<APIResult> ComplainAsync(ComplaintClaim complaint)
        {
            return await PostAsync("Complain", complaint);
        }

        public async Task<APIResult> ComplainReplyAsync(ComplaintReply reply)
        {
            return await PostAsync("ComplainReply", reply);
        }

        public async Task<APIResult> SubmitResolutionAsync(ODRResolution resolution, string voteid)
        {
            return await PostAsync($"SubmitResolution/?voteid={voteid}", resolution);
        }

        public async Task<APIResult> AnswerToResolutionAsync(string tradeId, int resolutionId, bool accepted, string accountId, string signature)
        {
            var args = new Dictionary<string, string>
            {
                { "tradeId", tradeId },
                { "resolutionId", resolutionId.ToString() },
                { "accountId", accountId },
                { "signature", signature },
                { "accepted", accepted.ToString() },
            };
            return await GetAsync<APIResult>("AnswerToResolution", args);
        }
    }
}
