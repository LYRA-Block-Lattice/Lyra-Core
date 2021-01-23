using Lyra.Data;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Shared
{
    public class GetPublicIPAddress
    {
        static string url = "https://api.ipify.org";
        static IPAddress _myIp;

        public static async Task<IPAddress> PublicIPAddressAsync(string networkId)
        {
            if (_myIp == null)  // no hammer on get ip service.
            {
                int retry = 10;
                while (retry-- > 0)
                {
                    try
                    {
                        var env = networkId;
                        if (string.IsNullOrWhiteSpace(env) || env == "devnet")
                        {
                            _myIp = Utilities.LocalIPAddress(false);
                        }
                        else
                        {
                            var wc = new HttpClient();
                            var json = await wc.GetStringAsync(url);
                            _myIp = IPAddress.Parse(json);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"In getting IP: {ex.Message}");
                        _myIp = Utilities.LocalIPAddress(false);
                        await Task.Delay(5000);
                    }
                }
            }

            return _myIp;
        }
    }

}
