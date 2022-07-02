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
        /// <summary>
        /// account ID of the resolution owner
        /// </summary>
        public string creator { get; set; }

        public ResolutionType RType { get; set; }
        public string tradeid { get; set; }

        public TransMove[] actions { get; set; }

        /// <summary>
        /// say something about the resolution, optional the dispute itself
        /// </summary>
        public string description { get; set; }

        public string GetExtraData()
        {
            var actstr = string.Join("|", actions.Select(x => x.GetExtraData()));
            return $"{creator}|{RType}|{tradeid}|{actstr}|{description}";
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
            result += $"Description: {description}";
            return result;
        }
    }

    public class ODRNegotiationRound
    {
        public DateTime Timestamp { get; set; }
        public string tradeid { get; set; }

        // complain
        public string complainBy { get; set; }
        public string resoluteBy { get; set; }
        public ODRResolution resolution { get; set; }

        // accepance
        public string acceptanceBy { get; set; }
        public bool acceptanceResult { get; set; }

        // execution
        public bool executed { get; set; }
        public DateTime? executedTime { get; set; }
    }
}
