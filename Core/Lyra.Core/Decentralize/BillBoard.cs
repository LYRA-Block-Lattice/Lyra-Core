using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Cryptography;
using Lyra.Core.Utils;
using Lyra.Shared;
using Neo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize
{
    // billboard is declaresive. 
    // if it has been broadcasted, it is treated as a pbft node.
    // if the pbft node can do authorize, it will be added into pbftnet.
    // if the node is dead for a long time (> 45 seconds), it will be put into freezing pool
    public class BillBoard
    {
        public Dictionary<string, IPAddress> NodeAddresses { get; }

        // with heartbeat, we know who is alive.
        public List<ActiveNode> ActiveNodes { get; }
        public List<string> AllVoters { get; set; }

        public string[] PrimaryAuthorizers { get; set; }

        public string CurrentLeader { get; set; }
        public int CurrentLeadersVotes { get; set; }

        public BillBoard()
        {
            NodeAddresses = new Dictionary<string, IPAddress>();
            ActiveNodes = new List<ActiveNode>();
        }
    }

    public class ActiveNode
    {
        public string AccountID { get; set; }
        public DateTime LastActive { get; set; }
        public string AuthorizerSignature { get; set; }
        public decimal Votes { get; internal set; }
    }

    public class PosNode
    {
        public string AccountID { get; set; }
        public string IPAddress { get; set; }
        public decimal Votes { get; set; }
        public DateTime LastStaking { get; set; }
        public string AuthorizerSignature { get; set; }

        public PosNode(string accountId)
        {
            AccountID = accountId;
            LastStaking = DateTime.Now;
            Votes = 0;
        }

        //public override bool Equals(object obj)
        //{
        //    if(obj is PosNode pn)
        //    {
        //        return AccountID == pn.AccountID
        //            && IPAddress == pn.IPAddress
        //            && Votes == pn.Votes
        //            && LastStaking == pn.LastStaking
        //            && Signature == pn.Signature;
        //    }

        //    return base.Equals(obj);
        //}

        // heartbeat/consolidation block: 10 min so if 30 min no message the node die
        //public bool GetAbleToAuthorize() => (ProtocolSettings.Default.StandbyValidators.Any(a => a == AccountID) || Votes >= LyraGlobal.MinimalAuthorizerBalance) && (DateTime.Now - LastStaking < TimeSpan.FromSeconds(180));

        //internal string ToHashInputString()
        //{
        //    return $"{AccountID}|{IPAddress}|{Votes.ToBalanceLong()}|{Signature}";
        //}
    }
}
