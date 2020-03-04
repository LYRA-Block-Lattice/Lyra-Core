using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Utils
{
    public class LyraConfig
    {
        public LyraNodeConfig Lyra { get; }

        public LyraConfig(IConfigurationSection section)
        {
            Lyra = new LyraNodeConfig(section.GetSection("Lyra"));
        }
    }
    public class LyraNodeConfig
    {
        public string NetworkId { get; }
        public LyraDatabaseConfig Database { get; }
        public LyraWalletConfig Wallet { get; }
        public string FeeAccountId { get; }

        public LyraNodeConfig(IConfigurationSection section)
        {
            NetworkId = section.GetSection("NetworkId").Value;
            Database = new LyraDatabaseConfig(section.GetSection("Database"));
            Wallet = new LyraWalletConfig(section.GetSection("Wallet"));
            FeeAccountId = section.GetSection("FeeAccountId").Value;
        }
    }

    public class LyraWalletConfig
    {
        public string Name { get; set; }
        public LyraWalletConfig(IConfigurationSection section)
        {
            Name = section.GetSection("Name").Value;
        }
    }

    public class LyraDatabaseConfig
    {
        public string DatabaseName { get; }
        public string DBConnect { get; }
        public string DexDBConnect { get; }
        public LyraDatabaseConfig(IConfigurationSection section)
        {
            DatabaseName = section.GetSection("DatabaseName").Value;
            DBConnect = section.GetSection("DBConnect").Value;
            DexDBConnect = section.GetSection("DexDBConnect").Value;
        }
    }
}
