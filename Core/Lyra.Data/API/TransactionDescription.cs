using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Data.API
{
    // a simple description of the transaction.
    // this description need to be calculated
    public class TransactionDescription
    {
        public string AccountId { get; set; }
        public DateTime TimeStamp { get; set; }
        public bool IsReceive { get; set; }
        public string PeerAccountId { get; set; }
        public Dictionary<string, long> Changes { get; set; }
        public Dictionary<string, long> Balance { get; set; }
    }
}
