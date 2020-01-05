using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.API
{
    public class LyraGlobal
    {
        public const string LYRA_TICKER_CODE = "Lyra.Coin";
        public const int LYRA_PRECISION = 8;

        public static readonly int APIVERSION = 3;
        public static readonly string NodeVersion = "LyraLex 1.0";
#if DEBUG
        public static readonly IList<string> Networks = new[] { "mainnet", "testnet",
            "devnet"
        };
#else
        public static readonly IList<string> Networks = new[] { "mainnet", "testnet"
        };
#endif

        // get api for (rpcurl, resturl)
        public static string SelectNode(string networkID)
        {
            switch (networkID)
            {
#if DEBUG
                case "devnet":
                    return "https://seed.devnet.lyrashops.com:4505/api/";
#endif
                case "testnet":
                    return "https://seed.testnet.lyrashops.com:4505/api/";
                case "mainnet":
                    return "https://seed.mainnet.lyrashops.com:4505/api/";
                default:
                    throw new Exception("Unsupported network ID");
            }
        }
    }
}
