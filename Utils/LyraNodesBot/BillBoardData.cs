using Lyra.Core.API;
using Lyra.Core.Decentralize;
using System;
using System.Collections.Generic;
using System.Text;

namespace LyraNodesBot
{
    class BillBoardData
    {
        public Dictionary<string, PosNodeData> AllNodes { get; set; }
        public bool canDoConsensus { get; set; }
    }

    public class PosNodeData
    {
        public string accountID { get; set; }
        public string ip { get; set; }
        public decimal balance { get; set; }
        public DateTime lastStaking { get; set; }
        public bool ableToAuthorize { get; set; }

        public IEnumerable<string> FailReasons
        {
            get
            {
                if (balance < LyraGlobal.MinimalAuthorizerBalance)
                    yield return "Low Balance";
                if (DateTime.Now - lastStaking > TimeSpan.FromMinutes(12))
                    yield return "Inactive";
            }
        }
    }





}
