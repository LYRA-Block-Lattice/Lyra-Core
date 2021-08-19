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
}
