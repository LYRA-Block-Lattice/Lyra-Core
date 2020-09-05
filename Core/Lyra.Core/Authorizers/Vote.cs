using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Authorizers
{
    public class Vote
    {
        public string AccountId { get; set; }
        public Decimal Amount { get; set; }    // all vote round to int to avoid float calculation 
    }

    public class Voter
    {
        public string AccountId { get; set; }
        public Decimal Balance { get; set; }    // all vote round to int to avoid float calculation 
        public string VoteFor { get; set; }
    }
}
