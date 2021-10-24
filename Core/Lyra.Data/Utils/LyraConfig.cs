using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Data.Utils
{
    public enum NodeMode { Normal, App }
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
        private static string _networkId;
        public static void Init(string NetworkId)
        {
            if (_networkId == null)
                _networkId = NetworkId;
        }
        public static string GetNetworkId() => _networkId;

        public string NetworkId => _networkId;
        public LyraDatabaseConfig Database { get; }
        public LyraWalletConfig Wallet { get; }
        public NodeMode Mode { get; }
        public string FeeAccountId { get; }

        public LyraNodeConfig(IConfigurationSection section)
        {
            Database = new LyraDatabaseConfig(section.GetSection("Database"));
            Wallet = new LyraWalletConfig(section.GetSection("Wallet"));
            FeeAccountId = section.GetSection("FeeAccountId").Value;
            try
            {
                Mode = (NodeMode)Enum.Parse(typeof(NodeMode), section.GetSection("Mode").Value, true);
            }
            catch
            {
                Mode = NodeMode.Normal;
            }
        }
    }

    public class LyraWalletConfig
    {
        public string Name { get; set; }
        public string Password { get; set; }
        public LyraWalletConfig(IConfigurationSection section)
        {
            Name = section.GetSection("Name").Value;
            Password = section.GetSection("Password").Value;
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
