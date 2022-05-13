using Lyra.Data;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Shared
{
    public class GetPublicIPAddress
    {
        static string[] url = new[]
        {
            //"http://checkip.dyndns.org/",
            "https://checkip.amazonaws.com/",
            "http://ipinfo.io/ip",
            "https://api.ipify.org",
        };

        public static async Task<bool> IsThisHostMeAsync(string host)
        {
            var he = await Dns.GetHostEntryAsync(host);
            var myip = await PublicIPAddressAsync();
            if (he.AddressList.Any() && he.AddressList.First().Equals(myip))
            {
                // self
                return true;
            }
            else
                return false;
        }

        public static async Task<IPAddress> PublicIPAddressAsync()
        {
            int retry = 10;
            IPAddress myIp = null;
            while (retry-- > 0)
            {
                try
                {
                    var netid = Environment.GetEnvironmentVariable("LYRA_NETWORK");
                    if (netid != "mainnet" && netid != "testnet")
                    {
                        myIp = Utilities.LocalIPAddress(false);
                    }
                    else
                    {
                        var wc = new HttpClient();
                        var json = await wc.GetStringAsync(url[0]);
                        myIp = IPAddress.Parse(json.Trim());
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"In getting IP: {ex.Message}");
                    myIp = Utilities.LocalIPAddress(false);
                    await Task.Delay(5000);
                }
            }

            return myIp;
        }
    }

}
