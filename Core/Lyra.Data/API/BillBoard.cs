using Lyra.Core.API;
using Lyra.Core.Blocks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API
{
    // billboard is declaresive. 
    // if it has been broadcasted, it is treated as a pbft node.
    // if the pbft node can do authorize, it will be added into pbftnet.
    // if the node is dead for a long time (> 45 seconds), it will be put into freezing pool
    public class BillBoard
    {
        public ConcurrentDictionary<string, string> NodeAddresses { get; set; }

        // with heartbeat, we know who is alive.
        public List<ActiveNode> ActiveNodes { get; set; }

        // for service block, validate from all voters
        private List<string> _avs;
        public List<string> AllVoters 
        { 
            get
            {
                return _avs;
            } 
            set
            {
                _avs = value;
                if (_avs == null || (_avs.Count < 4 && _avs.Count > 0))
                    Debugger.Break();
            } 
        }

        // for other block, validate from primary authorizers.
        public List<string> PrimaryAuthorizers { get; set; }

        public string CurrentLeader { get; set; }

        public string LeaderCandidate { get; set; }
        public int LeaderCandidateVotes { get; set; }


        public BillBoard()
        {
            NodeAddresses = new ConcurrentDictionary<string, string>();
            ActiveNodes = new List<ActiveNode>();
            AllVoters = new List<string>();
        }

        public void UpdatePrimary(List<string> list)
        {
            PrimaryAuthorizers = list;
        }
    }

    public class ActiveNode
    {
        public string AccountID { get; set; }
        public DateTime LastActive { get; set; }
        public string AuthorizerSignature { get; set; }
        public decimal Votes { get; set; }
        public BlockChainState State { get; set; }
        public string ThumbPrint { get; set; }
    }

    public class PosNode
    {
        public string AccountID { get; set; }
        public string IPAddress { get; set; }
        public decimal Votes { get; set; }
        public DateTime LastStaking { get; set; }
        public string AuthorizerSignature { get; set; }
        public string NodeVersion { get; set; }
        public string ThumbPrint { get; set; }

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
