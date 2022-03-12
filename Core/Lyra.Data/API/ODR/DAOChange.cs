using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API.ODR
{
    public class DAOChange
    {
        public string creator { get; set; }
        public Dictionary<string, string> settings { get; set; }

        public string GetExtraData()
        {
            var actstr = string.Join("|", settings.Select(x => $"{x.Key}={x.Value}"));
            return $"{creator}|{actstr}";
        }

        public override string ToString()
        {
            var result = $"Creator: {creator}\n";

            foreach (var chg in settings)
            {
                result += $"{chg.Key} => {chg.Value}\n";
            }
            return result;
        }
    }
}
