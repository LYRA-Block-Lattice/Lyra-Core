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
        public List<PosNode> AllNodes { get; }
        public string[] PrimaryAuthorizers { get; set; }
        public string[] BackupAuthorizers { get; set; }

        public string CurrentLeader { get; set; }

        public BillBoard()
        {
            AllNodes = new List<PosNode>();
        }

        public void SnapShot()
        {
            var nonSeeds = AllNodes.Where(a => a.GetAbleToAuthorize() && !ProtocolSettings.Default.StandbyValidators.Any(b => b == a.AccountID))
                    .OrderByDescending(b => b.Votes)
                    .ThenByDescending(c => c.LastStaking)
                    .Take(ProtocolSettings.Default.ConsensusTotalNumber - ProtocolSettings.Default.StandbyValidators.Length)
                    .Select(n => n.AccountID)
                    .ToArray();
            PrimaryAuthorizers = new string[ProtocolSettings.Default.StandbyValidators.Length + nonSeeds.Length];
            Array.Copy(ProtocolSettings.Default.StandbyValidators, 0, PrimaryAuthorizers, 0, ProtocolSettings.Default.StandbyValidators.Length);
            if(nonSeeds.Length > 0)
                Array.Copy(nonSeeds, 0, PrimaryAuthorizers, ProtocolSettings.Default.StandbyValidators.Length, nonSeeds.Length);

            var nonPrimaryNodes = AllNodes.Where(a => a.GetAbleToAuthorize() && !Array.Exists(PrimaryAuthorizers, x => x == a.AccountID));
            if(nonPrimaryNodes.Any())
            {
                BackupAuthorizers = nonPrimaryNodes
                    .OrderByDescending(b => b.Votes)
                    .ThenByDescending(c => c.LastStaking)
                    .Take(ProtocolSettings.Default.ConsensusTotalNumber)
                    .Select(a => a.AccountID).ToArray();
            }
            else
            {
                BackupAuthorizers = new string[0];
            }
        }

        public bool HasNode(string accountId) { return AllNodes.Any(a => a.AccountID == accountId); }
        public PosNode GetNode(string accountId) { return AllNodes.First(a => a.AccountID == accountId); }

        public PosNode Add(PosNode node)
        {
            if (!HasNode(node.AccountID))
            {
                AllNodes.Add(node);
            }
            else
            {
                var oldNode = AllNodes.RemoveAll(a => a.AccountID == node.AccountID);
                AllNodes.Add(node);
            }

            node.LastStaking = DateTime.Now;

            return node;
        }

        public void Refresh(string accountId)
        {
            PosNode node;
            if (HasNode(accountId))
            {
                node = GetNode(accountId);
                node.LastStaking = DateTime.Now;
            }
        }
    }

    public class PosNode
    {
        public string AccountID { get; set; }
        public string IPAddress { get; set; }
        public decimal Votes { get; set; }
        public DateTime LastStaking { get; set; }
        public string Signature { get; set; }

        public PosNode(string accountId)
        {
            AccountID = accountId;
            LastStaking = DateTime.Now;
            Votes = 0;
        }

        public override bool Equals(object obj)
        {
            if(obj is PosNode pn)
            {
                return AccountID == pn.AccountID
                    && IPAddress == pn.IPAddress
                    && Votes == pn.Votes
                    && LastStaking == pn.LastStaking
                    && Signature == pn.Signature;
            }

            return base.Equals(obj);
        }

        // heartbeat/consolidation block: 10 min so if 30 min no message the node die
        public bool GetAbleToAuthorize() => (ProtocolSettings.Default.StandbyValidators.Any(a => a == AccountID) || Votes >= LyraGlobal.MinimalAuthorizerBalance) && (DateTime.Now - LastStaking < TimeSpan.FromSeconds(180));

        internal string ToHashInputString()
        {
            return $"{AccountID}|{IPAddress}|{Votes.ToBalanceLong()}|{SignableObject.DateTimeToString(LastStaking)}|{Signature}";
        }
    }
}
