using Lyra.Data.API.ODR;
using Lyra.Data.API.WorkFlow;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API.Identity
{
    public class TradeBrief
    {
        public string TradeId { get; set; } = null!;

        public TradeDirection Direction { get; set; }

        /// <summary>
        /// account ID list
        /// first is seller, second is buyer
        /// </summary>
        public List<string> Members { get; set; } = null!;

        /// <summary>
        /// seller name and buyer name
        /// </summary>
        public List<string> Names { get; set; }

        public List<DateTime> RegTimes { get; set; }

        public bool IsCancellable { get; set; }

        public DisputeLevels DisputeLevel { get; set; }

        public List<DisputeCase>? DisputeHistory { get; set; }
        public List<ODRResolution>? ResolutionHistory { get; set; }
    }
}
