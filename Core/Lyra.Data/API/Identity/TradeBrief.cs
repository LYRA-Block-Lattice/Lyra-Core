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

        // role -> account id
        public List<string> Members { get; set; } = null!;

        public bool IsCancellable { get; set; }

        // dispute
        [JsonIgnore]
        public DisputeLevels DisputeLevel => DisputeHistory == null ? DisputeLevels.None : (DisputeLevels)DisputeHistory.Count;

        public List<DisputeCase>? DisputeHistory { get; set; }
    }
}
