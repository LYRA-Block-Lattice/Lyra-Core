using Lyra.Core.API;
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
        public Dictionary<string, PosNode> AllNodes { get; } = new Dictionary<string, PosNode>();
        public string[] PrimaryAuthorizers { get; set; }
        public string[] BackupAuthorizers { get; set; }

        public BillBoard()
        {
        }

        public void SnapShot()
        {
            var nonSeeds = AllNodes.Values.Where(a => a.AbleToAuthorize && !ProtocolSettings.Default.StandbyValidators.Any(b => b == a.AccountID))
                    .OrderByDescending(b => b.Votes)
                    .ThenByDescending(c => c.LastStaking)
                    .Take(ProtocolSettings.Default.ConsensusTotalNumber - ProtocolSettings.Default.StandbyValidators.Length)
                    .Select(n => n.AccountID)
                    .ToArray();
            PrimaryAuthorizers = new string[ProtocolSettings.Default.StandbyValidators.Length + nonSeeds.Length];
            Array.Copy(ProtocolSettings.Default.StandbyValidators, 0, PrimaryAuthorizers, 0, ProtocolSettings.Default.StandbyValidators.Length);
            if(nonSeeds.Length > 0)
                Array.Copy(nonSeeds, 0, PrimaryAuthorizers, ProtocolSettings.Default.StandbyValidators.Length, nonSeeds.Length);

            var nonPrimaryNodes = AllNodes.Values.Where(a => a.AbleToAuthorize && !Array.Exists(PrimaryAuthorizers, x => x == a.AccountID));
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

        //public bool CanDoConsensus
        //{
        //    get
        //    {
        //        if (PrimaryAuthorizers == null)
        //            return false;

        //        if (BlockChain.Singleton.CurrentState == BlockChainState.Almighty)
        //            return PrimaryAuthorizers.Length >= ProtocolSettings.Default.ConsensusTotalNumber;
        //        else
        //            return PrimaryAuthorizers.Length >= ProtocolSettings.Default.StandbyValidators.Length;
        //    }            
        //}

        public bool HasNode(string accountId) { return AllNodes.ContainsKey(accountId); }
        public PosNode GetNode(string accountId) { return AllNodes[accountId]; }

        public PosNode Add(PosNode node)
        {
            if (!AllNodes.ContainsKey(node.AccountID))
            {
                AllNodes.Add(node.AccountID, node);
            }
            else
            {
                AllNodes[node.AccountID].IPAddress = node.IPAddress;      // support for dynamic IP address
            }

            node.LastStaking = DateTime.Now;

            return node;
        }

        public void Refresh(string accountId)
        {
            PosNode node;
            if (AllNodes.ContainsKey(accountId))
            {
                node = AllNodes[accountId];
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

        public void Sign()
        {
            Signature = Signatures.GetSignature(NodeService.Instance.PosWallet.PrivateKey,
                    IPAddress, NodeService.Instance.PosWallet.AccountId);
        }

        // heartbeat/consolidation block: 10 min so if 30 min no message the node die
        public bool AbleToAuthorize => (ProtocolSettings.Default.StandbyValidators.Any(a => a == AccountID) || Votes >= LyraGlobal.MinimalAuthorizerBalance) && (DateTime.Now - LastStaking < TimeSpan.FromSeconds(90));
    }
}
