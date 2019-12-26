using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Utils
{
    public class LyraNodeConfig
    {
        public LyraConfig Lyra { get; set; }
        public OrleansConfig Orleans { get; set; }

        public class LyraConfig
        {
            public string NetworkId { get; set; }
            public LyraDatabaseConfig Database { get; set; }
        }
    }

    public class LyraDatabaseConfig
    {
        public string DatabaseName { get; set; }
        public string DBConnect { get; set; }
        public string DexDBConnect { get; set; }
    }

    public class OrleansConfig
    {
        public GlobalSettings ZooKeeperClusteringSilo { get; set; }
        public ClusterSettings Cluster { get; set; }
        public EndPointSettings EndPoint { get; set; }
        public class GlobalSettings
        {
            public string ConnectionString { get; set; }
        }
        public class ClusterSettings
        {
            public string ClusterId { get; set; }
            public string ServiceId { get; set; }
        }
        public class EndPointSettings
        {
            public string AdvertisedIPAddress { get; set; }
        }
    }
}
