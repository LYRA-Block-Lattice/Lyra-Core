using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.API
{
    public class LyraGlobal
    {
#if DEBUG
        public static readonly IList<string> Networks = new[] { "mainnet", "testnet",
            "lexnet", "lexdev"
        };
#else
        public static readonly IList<string> Networks = new[] { "lexnet" };
#endif
    }
}
