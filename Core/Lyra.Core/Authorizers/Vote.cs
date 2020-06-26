using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Authorizers
{
    public class Vote
    {
        public string AccountId { get; set; }
        public long Amount { get; set; }    // all vote round to int to avoid float calculation 
        public long ConsHeight { get; set; }
    }
}
