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
    }
}
