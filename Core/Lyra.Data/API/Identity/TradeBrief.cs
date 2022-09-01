using Lyra.Core.Blocks;
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

        public string[] CasesData { get; set; } = null!;
        public string[] CaseTypes { get; set; } = null!;

        public List<DisputeCase?> GetDisputeHistory()
        {
            var list = new List<DisputeCase?>();
            if (CaseTypes != null)
            {
                for (var i = 0; i < CaseTypes.Length; i++)
                {
                    DisputeCase? dispute = CaseTypes[i] switch
                    {
                        "Peer" => JsonConvert.DeserializeObject<PeerDisputeCase>(CasesData[i]),
                        "DAO" => JsonConvert.DeserializeObject<DaoDisputeCase>(CasesData[i]),
                        "LyraCouncil" => JsonConvert.DeserializeObject<CouncilDisputeCase>(CasesData[i]),
                        _ => throw new Exception($"Unknown dispute case type: {CaseTypes[i]}"),
                    };
                    list.Add(dispute);
                }
            }
            return list;
        }
        public void SetDisputeHistory(List<DisputeCase?> hist)
        {
            if (hist == null)
            {
                CaseTypes = new string[0];
                CasesData = new string[0];
            }
            else
            {
                CasesData = hist.Select(a => JsonConvert.SerializeObject(a)).ToArray();
                CaseTypes = hist.Select(a => a.Complaint.level.ToString()).ToArray();
            }
        }
    }
}

