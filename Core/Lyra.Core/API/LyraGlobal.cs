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

        public const int MAXMIMUMAUTHORIZERS = 21;

        public const int ProtocolVersion = 1;
        public const int DatabaseVersion = 1;

        public static string NodeAppName = PRODUCTNAME + " " + typeof(LyraGlobal).Assembly.GetName().Version.ToString();

        public const int MinimalAuthorizerBalance = 1000000;
        public const decimal OFFICIALGENESISAMOUNT = 12000000000;

#if DEBUG
        public static readonly IList<string> Networks = new[] { "mainnet", "testnet",
            "devnet"
        };
#else
        public static readonly IList<string> Networks = new[] { "mainnet", "testnet"
        };
#endif

        public const int TOKENSTORAGERITO = 100000000;

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
                    return "https://seed.testnet.wizdag.com:4505/api/";
                case "mainnet":
                    return "https://seed.mainnet.wizdag.com:4505/api/";
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
