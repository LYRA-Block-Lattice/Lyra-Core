using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Web;

namespace LyraWallet.Models
{
    // lyra://localhost/cart/checkout/?AccountID=[xxxxxx]&&Shop=[shop name]&&Token=Lyra.LeX&&Total=123.3

    public class LyraUri : Uri
    {
        NameValueCollection args;

        public string AccountID => args["AccountID"];
        public string Token => args["Token"];
        public string Total => args["Total"];
        public string Shop => args["Shop"];

        public string Method => AbsolutePath;

        public LyraUri(string url) : base(url)
        {
            args = HttpUtility.ParseQueryString(Query);
        }

        public LyraUri(string method, Dictionary<string, string> args)
            : base ($"lyra://localhost{method}?" + args?.Aggregate(new StringBuilder(),
                          (sb, kvp) => sb.AppendFormat("{0}{1} = {2}",
                                       sb.Length > 0 ? "&&" : "", kvp.Key, kvp.Value),
                          sb => sb.ToString()))
        {
            // todo
        }
    }
}
