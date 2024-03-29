﻿using DexServer.Ext;
using Lyra.Core.API;
using Lyra.Data.API.WorkFlow.UniMarket;
using Lyra.Data.Crypto;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lyra.Data.API
{
    public class AcademyClient : WebApiClientBase
    {
        public AcademyClient(string networkid)
        {
            if (networkid == "xtest")
                UrlBase = "https://startdevnet.lyra.live/svc/"; //"https://localhost:7288/svc/";
            else if (networkid == "testnet")
                UrlBase = "https://starttestnet.lyra.live/svc/";
            else if(networkid == "devnet")
                UrlBase = "https://startdevnet.lyra.live/svc/";
            else
                UrlBase = "https://start.lyra.live/svc/";
        }

        public async Task<string> CreateTotMetaAsync(string accountId, string signature,
            HoldTypes type,
                string name, string description)
        {
            dynamic args = new ExpandoObject();
            args.accountId = accountId;
            args.signature = signature;
            args.name = name;
            args.description = description;
            args.type = type.ToString();
            
            return await PostJsonAsync("CreateTotMeta", args);
        }

        public async Task<string> CreateNFTMetaIpfsAsync(string accountId, string signature,
            string name, string description, string ipfscid)
        {
            var args = new Dictionary<string, string>
            {
                { "accountId", accountId },
                { "signature", signature },
                { "name", name },
                { "description", description },
                { "ipfscid", ipfscid}
            };
            return await GetAsync<string>("CreateMetaIpfs", args);
        }

        public async Task<string> CreateNFTMetaHostedAsync(string accountId, string signature,
            string name, string description, string imgUrl)
        {
            var args = new Dictionary<string, string>
            {
                { "accountId", accountId },
                { "signature", signature },
                { "name", name },
                { "description", description },
                { "imgUrl", imgUrl}
            };
            return await GetAsync<string>("CreateMetaHosted", args);
        }

        public async Task<string> VerifyEmailAsync(string accountId, string email, string signature)
        {
            var args = new Dictionary<string, string>
            {
                { "accountId", accountId },
                { "email", email },
                { "signature", signature },
            };
            return await GetAsync<string>("VerifyEmail", args);
        }

        public async Task<string> GetCodeForEmailAsync(string accountId, string email, string signature)
        {
            var args = new Dictionary<string, string>
            {
                { "accountId", accountId },
                { "email", email },
                { "signature", signature },
            };
            return await GetAsync<string>("GetCodeForEmail", args);
        }
    }
}
