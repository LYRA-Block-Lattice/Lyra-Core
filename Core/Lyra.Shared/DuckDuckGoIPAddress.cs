using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Lyra.Shared
{
    public class DuckDuckGoIPAddress
    {
        static string url = "https://api.ipify.org";

        public static async System.Threading.Tasks.Task<IPAddress> PublicIPAddressAsync()
        {
            try
            {
                var env = Environment.GetEnvironmentVariable("LYRA_NETWORK");
                if (string.IsNullOrWhiteSpace(env) || env == "devnet")
                {
                    return Utilities.LocalIPAddress();
                }
                var wc = new HttpClient();
                var json = await wc.GetStringAsync(url);
                return IPAddress.Parse(json);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"In getting IP: {ex.Message}");
                return Utilities.LocalIPAddress();
            }
        }
    }

}
