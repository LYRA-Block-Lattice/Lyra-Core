using Lyra.Core.API;
using MongoDB.Bson.Serialization.Attributes;
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

    public class ProfitingStats
    {
        public string ProfitingID { get; set; }
        public DateTime Begin { get; set; }
        public DateTime End { get; set; }
        public decimal Total { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class AccountChange
    {
        public DateTime Time { get; set; }
        public string AccountID { get; set; }
        public string TxHash { get; set; }
        
        /// <summary>
        /// change of LYR. + means income, - means spend, 0 means no change
        /// </summary>
        public decimal LyrChg { get; set; }
        public long ConsHeight { get; set; }
    }
}
