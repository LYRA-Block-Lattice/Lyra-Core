using Microsoft.Extensions.Configuration;

namespace StressTest
{
    /// <summary> 
    /// A static class for retrieving API keys and other application settings. 
    /// </summary> 
    public static class Config
    {
        public static string SenderPrivateKey;
        //public static string RecipientAccountId;
        public static string NodeURL;
        public static string NetworkId;
        public static string SendCount;
        public static string LogPath;


        public static void LoadConfig(IConfiguration configuration)
        {
            SenderPrivateKey = configuration.GetSection("Settings").GetSection("SenderPrivateKey").Value;
            //RecipientAccountId = configuration.GetSection("Settings").GetSection("RecipientAccountId").Value;
            NodeURL = configuration.GetSection("Settings").GetSection("NodeURL").Value;
            NetworkId = configuration.GetSection("Settings").GetSection("NetworkId").Value;
            SendCount = configuration.GetSection("Settings").GetSection("SendCount").Value;
            LogPath = configuration.GetSection("Settings").GetSection("LogPath").Value;
        }
    }
}
