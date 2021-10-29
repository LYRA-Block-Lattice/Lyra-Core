using Lyra.Core.API;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Data.API
{
    public class Vote
    {
        public string AccountId { get; set; }
        public Decimal Amount { get; set; }    // all vote round to int to avoid float calculation 
    }

    public class Voter
    {
        public string AccountId { get; set; }
        public Dictionary<string, long> Balance2 { get; set; }
        public string VoteFor { get; set; }

        public Decimal LYR => Balance2.ContainsKey(LyraGlobal.OFFICIALTICKERCODE) ? Balance2[LyraGlobal.OFFICIALTICKERCODE] : 0;
    }

    public class Staker
    {
        public string StkAccount { get; set; }
        public string OwnerAccount { get; set; }
        public DateTime Time { get; set; }
        public int Days { get; set; }
        public decimal Amount { get; set; }
    }
}
