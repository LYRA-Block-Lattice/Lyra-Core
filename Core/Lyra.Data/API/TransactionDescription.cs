using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Data.API
{
    // a simple description of the transaction.
    // this description need to be calculated
    public class TransactionDescription
    {
        public long Height { get; set; }
        public bool IsReceive { get; set; }
        public DateTime TimeStamp { get; set; }
        public string SendAccountId { get; set; }
        public string SendHash { get; set; }
        public string RecvAccountId { get; set; }
        public string RecvHash { get; set; }
        public Dictionary<string, long> Changes { get; set; }
        public Dictionary<string, long> Balances { get; set; }
    }
}
