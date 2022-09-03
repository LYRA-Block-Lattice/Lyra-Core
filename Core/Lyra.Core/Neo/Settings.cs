using Lyra.Core.Utils;
using Lyra.Data.Utils;
using Microsoft.Extensions.Configuration;
using Neo.Network.P2P;
using System.Threading;

namespace Neo
{
    public class Settings
    {
        public LyraConfig LyraNode { get; }
        public P2PSettings P2P { get; }
        public string PluginURL { get; }

        static Settings _default;

        static bool UpdateDefault(IConfiguration configuration)
        {
            var settings = new Settings(configuration.GetSection("ApplicationConfiguration"));
            return null == Interlocked.CompareExchange(ref _default, settings, null);
        }

        public static bool Initialize(IConfiguration configuration)
        {
            return UpdateDefault(configuration);
        }

        public static Settings Default
        {
            get
            {
                if (_default == null)
                {
                    UpdateDefault(NeoHelper.LoadConfig("config"));
                }

                return _default;
            }
        }

        public Settings(IConfigurationSection section)
        {
            this.LyraNode = new LyraConfig(section.GetSection("LyraNode"));
            this.P2P = new P2PSettings(section.GetSection("P2P"));
            this.PluginURL = section.GetSection("PluginURL").Value;
        }
    }

    public class P2PSettings
    {
        public ushort Port { get; }
        //public ushort WsPort { get; }
        public string Endpoint { get; }
        public int MinDesiredConnections { get; }
        public int MaxConnections { get; }
        public int MaxConnectionsPerAddress { get; }

        public P2PSettings(IConfigurationSection section)
        {
            this.Port = ushort.Parse(section.GetSection("Port").Value);
            //this.WsPort = ushort.Parse(section.GetSection("WsPort").Value);
            this.Endpoint = section.GetValue("Endpoint", "").Trim().Trim(new char[] { ':'});   // docker may give ":4504" when no host name

            this.MinDesiredConnections = section.GetValue("MinDesiredConnections", Peer.DefaultMinDesiredConnections);
            this.MaxConnections = section.GetValue("MaxConnections", Peer.DefaultMaxConnections);
            this.MaxConnectionsPerAddress = section.GetValue("MaxConnectionsPerAddress", 6);
        }
    }
}
