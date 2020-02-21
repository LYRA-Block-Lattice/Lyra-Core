using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.API
{
    public sealed class LyraGlobal
    {
        public const string LYRATICKERCODE = "Lyra.Coin";
        public const int LYRAPRECISION = 8;

        public const int ProtocolVersion = 1;
        public const int DatabaseVersion = 1;

        public static string NodeAppName = "Lyra Permisionless " + typeof(LyraGlobal).Assembly.GetName().Version.ToString();

        public const int MinimalAuthorizerBalance = 1000000;
        public const decimal LYRAGENESISAMOUNT = 2000000000;

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
                    return "https://192.168.3.93:4505/api/";
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
