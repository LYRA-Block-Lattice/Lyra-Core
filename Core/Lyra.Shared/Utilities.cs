using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Lyra.Shared
{
    public class Utilities
    {
        public static IPAddress LocalIPAddress(bool getPublicIP)
        {
            if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                return null;
            }

            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());

            return host
                .AddressList
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .FirstOrDefault(b => getPublicIP ? !IsPrivate(b.ToString()) : true);
        }

        public static bool IsPrivate(string ipAddress)
        {
            try
            {
                int[] ipParts = ipAddress.Split(new String[] { "." }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(s => int.Parse(s)).ToArray();
                // in private ip range
                if (ipParts[0] == 127 ||
                    ipParts[0] == 10 ||
                    (ipParts[0] == 192 && ipParts[1] == 168) ||
                    (ipParts[0] == 172 && (ipParts[1] >= 16 && ipParts[1] <= 31)))
                {
                    return true;
                }

                // IP Address is probably public.
                // This doesn't catch some VPN ranges like OpenVPN and Hamachi.
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }

        public static string PathSeperator => (Environment.OSVersion.Platform == PlatformID.Unix ||
                   Environment.OSVersion.Platform == PlatformID.MacOSX) ? "/" : "\\";

        public static string LyraDataDir
        {
            get
            {
                string homePath = (Environment.OSVersion.Platform == PlatformID.Unix ||
                   Environment.OSVersion.Platform == PlatformID.MacOSX)
                    ? Environment.GetEnvironmentVariable("HOME")
                    : Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");

                var path = $"{homePath}{PathSeperator}.wizdag";
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                var netEnv = Environment.GetEnvironmentVariable("WIZDAG_NETWORK");
                var net = string.IsNullOrWhiteSpace(netEnv) ? "devnet" : netEnv;

                path = $"{path}{PathSeperator}{net}";
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                return path;
            }
        }
    }
}
