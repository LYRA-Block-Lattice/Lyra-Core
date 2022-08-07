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
            (var myipv4, var myipv6) = await PublicIPAddressAsync();
            if (he.AddressList.Any() && he.AddressList.Any(a => a == myipv4 || a == myipv6))
            {
                // self
                return true;
            }
            else
                return false;
        }

        public static async Task<(IPAddress? ipv4addr, IPAddress? ipv6addr)> PublicIPAddressAsync()
        {
            (var myIpv4, var myIpv6) = Utilities.LocalIPAddress(false);

            int retry = 10;            
            while (retry-- > 0)
            {
                try
                {
                    var netid = Environment.GetEnvironmentVariable("LYRA_NETWORK");
                    if (netid != "mainnet" && netid != "testnet")
                    {
                        return (myIpv4, myIpv6);
                    }
                    else
                    {
                        var wc = new HttpClient();
                        var json = await wc.GetStringAsync(url[0]);
                        myIpv4 = IPAddress.Parse(json.Trim());
                        
                        return (myIpv4, myIpv6);
                    }
                }
                catch
                {
                    await Task.Delay(5000);
                }
            }

            return (myIpv4, myIpv6);
        }
    }

}
