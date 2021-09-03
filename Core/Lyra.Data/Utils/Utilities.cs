using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Lyra.Data
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

            var ip1 = host
                .AddressList
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .FirstOrDefault(b => getPublicIP ? !IsPrivate(b.ToString()) : true);

            string localIP = ip1.ToString();
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                localIP = endPoint.Address.ToString();
            }

            return IPAddress.Parse(localIP);
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

        public static string GetLyraDataDir(string networkId, string productShortName)
        {
            string homePath = (Environment.OSVersion.Platform == PlatformID.Unix ||
               Environment.OSVersion.Platform == PlatformID.MacOSX)
                ? Environment.GetEnvironmentVariable("HOME")
                : Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");

            var path = $"{homePath}{PathSeperator}.{productShortName}";
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            var net = string.IsNullOrWhiteSpace(networkId) ? "devnet" : networkId;

            path = $"{path}{PathSeperator}{net}";
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return path;
        }

        public static T[] InitializeArray<T>(int length) where T : new()
        {
            T[] array = new T[length];
            for (int i = 0; i < length; ++i)
            {
                array[i] = new T();
            }

            return array;
        }

        public static string Sha256(string randomString)
        {
            var crypt = new System.Security.Cryptography.SHA256Managed();
            var hash = new System.Text.StringBuilder();
            byte[] crypto = crypt.ComputeHash(Encoding.UTF8.GetBytes(randomString));
            foreach (byte theByte in crypto)
            {
                hash.Append(theByte.ToString("x2"));
            }
            return hash.ToString();
        }

        public static int Sha256Int(string randomString)
        {
            var crypt = new System.Security.Cryptography.SHA256Managed();
            var hash = new System.Text.StringBuilder();
            byte[] crypto = crypt.ComputeHash(Encoding.UTF8.GetBytes(randomString));
            return BitConverter.ToInt32(crypto, 0);
        }
    }
}
