using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace TcpHelperLib
{
    public static class JObjectEx
    {
        public static T V<T>(this JObject jo, string name)
        {
            return jo.GetValue(name).Value<T>();
        }
    }

    public static class StringBytesEx
    {
        public static byte[] ToBytes(this string s)
        {
            if (string.IsNullOrEmpty(s))
                return null;

            return Encoding.ASCII.GetBytes(s);
        }

        public static string ToStr(this byte[] bts)
        {
            if (bts == null || bts.Length == 0)
                return null;

            return Encoding.ASCII.GetString(bts);
        }

        public static string ToStr(this List<byte> lstBts)
        {
            if (lstBts == null || lstBts.Count == 0)
                return null;

            return lstBts.ToArray().ToStr();
        }
    }

    public static class Proxy
    {
        public static string ToJson(string methodName, string callID, params object[] args)
        {
            return new RemoteProcInfo { Name = methodName, CallId = callID,  Params = args }.ToJson();
        }
    }
}
