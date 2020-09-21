using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.API
{
    public sealed class LyraGlobal
    {
        public const string PRODUCTNAME = "LYRA Block Lattice";
        public const string PRODUCTWEBLINK = "https://lyra.live";
        public const string OFFICIALDOMAIN = "lyra";
        public const string OFFICIALTICKERCODE = "LYR";
        public const int OFFICIALTICKERPRECISION = 8;

        public const char ADDRESSPREFIX = 'L';
        public const string WALLETFILEEXT = ".lyrawallet";

        public const int CONSENSUS_TIMEOUT = 10;  // seconds
        public const int VIEWCHANGE_TIMEOUT = 15;    // seconds
        public const int MAXIMUM_VOTER_NODES = MAXIMUM_AUTHORIZERS + 2;
        public const int MAXIMUM_AUTHORIZERS = 19;
        public const int MINIMUM_AUTHORIZERS = 4; // initial number required to generate first service block and genesis

        public const int ProtocolVersion = 4;
        public const int DatabaseVersion = 2;

        public readonly static Version MINIMAL_COMPATIBLE_VERSION = new Version("1.7.7.0");
        public readonly static Version NODE_VERSION = typeof(LyraGlobal).Assembly.GetName().Version;
        public readonly static string NodeAppName = PRODUCTNAME + " " + typeof(LyraGlobal).Assembly.GetName().Version.ToString();

        public const int MinimalAuthorizerBalance = 1000000;
        public const decimal OFFICIALGENESISAMOUNT = 10000000000;

#if DEBUG
        public static readonly IList<string> Networks = new[] { "mainnet", "testnet",
            "devnet"
        };
#else
        public static readonly IList<string> Networks = new[] { "mainnet", "testnet"
        };
#endif

        public const int TOKENSTORAGERITO = 100000000;

        public static int GetMajority(int totalCount)
        {
            return totalCount - (int)Math.Floor((double)(((double)totalCount - 1) / 3));
        }

        // get api for (rpcurl, resturl)
        public static string SelectNode(string networkID)
        {
            switch (networkID)
            {
#if DEBUG
                case "devnet":
                    //return "http://192.168.3.73:4505/api/";
                    //return "http://10.211.55.5:4505/api/";
                    return "http://api.devnet:4505/api/";       // better set static hosts entry

#endif
                case "testnet":
                    return "http://api.testnet.lyra.live:4505/api/";
                case "mainnet":
                    return "http://api.mainnet.lyra.live:4505/api/";
                default:
                    throw new Exception("Unsupported network ID: " + networkID);
            }
        }
    }

    public static class LyraExtensions
    {
        public static long ToBalanceLong(this decimal currency)
        {
            return (long)Math.Round(currency * LyraGlobal.TOKENSTORAGERITO);
        }

        public static decimal ToBalanceDecimal(this long currency)
        {
            return ((decimal)currency) / LyraGlobal.TOKENSTORAGERITO;
        }
    }
}
