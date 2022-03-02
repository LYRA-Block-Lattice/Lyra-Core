using System;
using System.Collections.Generic;
using System.Linq;
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
        public const int RITOPRECISION = 14;

        public const char ADDRESSPREFIX = 'L';
        public const string WALLETFILEEXT = ".lyrawallet";

        public const int CONSENSUS_TIMEOUT = 8;  // seconds
        public const int VIEWCHANGE_TIMEOUT = 18;    // seconds
        public const int MAXIMUM_VOTER_NODES = MAXIMUM_AUTHORIZERS;
        public const int MAXIMUM_AUTHORIZERS = 19;
        public const int MINIMUM_AUTHORIZERS = 4; // initial number required to generate first service block and genesis

        public const int ProtocolVersion = 4;
        public const int DatabaseVersion = 7;

        public readonly static Version MINIMAL_COMPATIBLE_VERSION = new Version("2.0.2.0");
        public readonly static Version NODE_VERSION = typeof(LyraGlobal).Assembly.GetName().Version;
        public readonly static string NodeAppName = PRODUCTNAME + " " + typeof(LyraGlobal).Assembly.GetName().Version.ToString();

        public const int MinimalAuthorizerBalance = 1000000;
        public const decimal OFFICIALGENESISAMOUNT = 10000000000;

        public const string BURNINGACCOUNTID = "L11111111111111111111111111111111111111111111111111111111111111116oUsJe";

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
            //int port = 4505;
            //if ("mainnet".Equals(networkID, StringComparison.InvariantCultureIgnoreCase))
            //    port = 5505;

            switch (networkID)
            {
                case "devnet":
                    //return "http://192.168.3.73:4505/api/";
                    //return "http://10.211.55.5:4505/api/";
                    return $"https://devnet.lyra.live:4504/api/";       // better set static hosts entry
                case "testnet":
                    //return $"http://api.testnet.lyra.live:{port}/api/";
                    return $"https://testnet.lyra.live/api/";
                case "mainnet":
                    //return $"http://api.mainnet.lyra.live:{port}/api/";
                    return $"https://mainnet.lyra.live/api/";
                default:
                    throw new Exception("Unsupported network ID: " + networkID);
            }
        }

        public static string GetDexServerAccountID(string networkId)
        {
            if (networkId == "mainnet")
                return "LDEXL5Qya4SC6vMwdtT24cvtbFEFtP6riD8NXSm75gcnu8odN4x7mHqyYzhkqvxE39gWLYxi8ry4vdVjK1R4zqZFRVG9HB";
            else
                return "LDEXLtDmnMq4Yuc9wW6DoUDrNmZznD43PLhF8TF5MpmkxWLR7zJzF7uipfVaugo19EcZBS46UFxc967V98AhHVQbWajpZk";
        }
    }

    public static class LyraExtensions
    {
        public static long ToBalanceLong(this decimal currency)
        {
            if (currency > long.MaxValue / LyraGlobal.TOKENSTORAGERITO)
                throw new OverflowException();

            return (long)Math.Round(currency * LyraGlobal.TOKENSTORAGERITO);
        }

        public static decimal ToBalanceDecimal(this long currency)
        {
            return ((decimal)currency) / LyraGlobal.TOKENSTORAGERITO;
        }

        public static Dictionary<string, decimal> ToDecimalDict(this Dictionary<string, long> dict)
        {
            return dict.ToDictionary(k => k.Key, k => k.Value.ToBalanceDecimal());
        }

        public static Dictionary<string, long> ToLongDict(this Dictionary<string, decimal> dict)
        {
            return dict.ToDictionary(k => k.Key, k => k.Value.ToBalanceLong());
        }

        public static long ToRitoLong(this decimal currency)
        {
            return (long)Math.Round(currency * (decimal) Math.Pow(10, LyraGlobal.RITOPRECISION));
        }

        public static decimal ToRitoDecimal(this long currency)
        {
            return ((decimal)currency) / (decimal)Math.Pow(10, LyraGlobal.RITOPRECISION);
        }

        public static Dictionary<string, decimal> ToRitoDecimalDict(this Dictionary<string, long> dict)
        {
            return dict.ToDictionary(k => k.Key, k => k.Value.ToRitoDecimal());
        }

        public static Dictionary<string, long> ToRitoLongDict(this Dictionary<string, decimal> dict)
        {
            return dict.ToDictionary(k => k.Key, k => k.Value.ToRitoLong());
        }
    }
}
