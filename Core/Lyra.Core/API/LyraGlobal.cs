using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.API
{
    public class LyraGlobal
    {
        public static readonly int APIVERSION = 2;
        public static readonly string NodeVersion = "LyraLex 1.0";
#if DEBUG
        public static readonly IList<string> Networks = new[] { "mainnet", "testnet",
            "lexnet", "lexdev"
        };
#else
        public static readonly IList<string> Networks = new[] { "lexnet" };
#endif
        // get api for (rpcurl, resturl)
        public static (string rpcUrl, string restUrl) SelectNode(string networkID)
        {
            switch (networkID)
            {
#if DEBUG
                case "lexdev":
                    return ("https://34.80.72.244:5492/", "https://34.80.72.244:5492/api/");
#endif
                case "lexnet":
                    return ("https://34.80.72.244:5392/", "https://34.80.72.244:5392/api/");
                case "testnet":
                    return ("https://testnet.lyratokens.com:5392/", "");
                case "mainnet":
                    return ("https://mainnet.lyratokens.com:5392/", "");
                default:
                    throw new Exception("Unsupported network ID");
            }
        }
    }
}
