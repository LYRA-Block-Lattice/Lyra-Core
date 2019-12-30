using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Utils
{
    public class LyraNodeConfig
    {
        public LyraConfig Lyra { get; set; }

        public class LyraConfig
        {
            public string NetworkId { get; set; }
            public LyraDatabaseConfig Database { get; set; }
            public LyraWalletConfig Wallet { get; set; }
        }
    }

    public class LyraWalletConfig
    {
        public string Name { get; set; }
    }

    public class LyraDatabaseConfig
    {
        public string DatabaseName { get; set; }
        public string DBConnect { get; set; }
        public string DexDBConnect { get; set; }
    }
}
