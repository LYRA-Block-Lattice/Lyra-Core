using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Lyra.Shared
{
    public class GetPublicIPAddress
    {
        static string url = "https://api.ipify.org";
        static IPAddress _myIp;

        public static async System.Threading.Tasks.Task<IPAddress> PublicIPAddressAsync()
        {
            if(_myIp == null)  // no hammer on get ip service.
            try
            {
                var env = Environment.GetEnvironmentVariable("LYRA_NETWORK");
                if (string.IsNullOrWhiteSpace(env) || env == "devnet")
                {
                    _myIp = Utilities.LocalIPAddress();
                }
                var wc = new HttpClient();
                var json = await wc.GetStringAsync(url);
                _myIp = IPAddress.Parse(json);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"In getting IP: {ex.Message}");
                _myIp = Utilities.LocalIPAddress();
            }
            return _myIp;
        }
    }

}
