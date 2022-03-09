using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API.ODR
{
    public enum ResolutionType { OTCTrade };
    public class ODRResolution
    {
        public string creator { get; set; }

        public ResolutionType RType { get; set; }
        public string tradeid { get; set; }

        public TransMove[] actions { get; set; }

        public string GetExtraData()
        {
            var actstr = string.Join("|", actions.Select(x => x.GetExtraData()));
            return $"{creator}|{RType}|{tradeid}|{actstr}";
        }

        public override string ToString()
        {
            var result = $"Creator: {creator}\n";

            result += $"Resolution Type: {RType}\n";            
            result += $"On Trade: {tradeid}\n";
            foreach(var act in actions)
            {
                result += $"Action: {act}\n";
            }
            return result;
        }
    }
}
