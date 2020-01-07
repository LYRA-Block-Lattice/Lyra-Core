using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Decentralize
{
    public class BillBoard
    {
        public Dictionary<string, PosNode> AllNodes { get; private set; }

        public BillBoard()
        {
            AllNodes = new Dictionary<string, PosNode>();
        }
    }

    public class PosNode
    {
        public string AccountID { get; set; }
        public decimal Balance { get; set; }
        public DateTime LastStaking { get; set; }

        public PosNode(string accountId)
        {
            AccountID = accountId;
            LastStaking = DateTime.Now;
            Balance = 0;
        }
    }
}
