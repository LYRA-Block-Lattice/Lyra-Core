using Lyra.Core.API;
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

        public BillBoard()
        {

        }

        public bool CanDoConsensus
        {
            get
            {
                var workingNodes = AllNodes.Values.Where(a => a.AbleToAuthorize).OrderByDescending(b => b.Balance).Take(ProtocolSettings.Default.ConsensusTotalNumber);
                if (workingNodes.Count() >= ProtocolSettings.Default.ConsensusWinNumber)
                    return true;
                else
                    return false;
            }
        }

        public bool HasNode(string accountId) { return AllNodes.ContainsKey(accountId); }
        public PosNode GetNode(string accountId) { return AllNodes[accountId]; }
 
        public async Task<PosNode> AddAsync(PosNode node)
        {
            if (!AllNodes.ContainsKey(node.AccountID))
            {
                AllNodes.Add(node.AccountID, node);
            }

            node.LastStaking = DateTime.Now;

            // lookup balance
            var block = await BlockChain.Singleton.FindLatestBlockAsync(node.AccountID);
            if (block != null && block.Balances != null && block.Balances.ContainsKey(LyraGlobal.LYRATICKERCODE))
            {
                node.Balance = block.Balances[LyraGlobal.LYRATICKERCODE];
            }

            return node;
        }
        public void RefreshAsync(string accountId)
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
        public string IP { get; set; }
        public decimal Balance { get; set; }
        public DateTime LastStaking { get; set; }

        private MeshNetworkConnecStatus _netStatus;
        public string NetStatus { get => _netStatus.ToString(); set => Enum.TryParse(value, out MeshNetworkConnecStatus _netStatus); }

        public PosNode(string accountId)
        {
            AccountID = accountId;
            LastStaking = DateTime.Now;
            Balance = 0;
        }

        public void UpdateNetStatus(MeshNetworkConnecStatus status) => _netStatus = status;
        // heartbeat/consolidation block: 10 min so if 30 min no message the node die
        public bool AbleToAuthorize => (ProtocolSettings.Default.StandbyValidators.Any(a => a == AccountID) || Balance >= 1000000) && (DateTime.Now - LastStaking < TimeSpan.FromMinutes(12) && _netStatus == MeshNetworkConnecStatus.FulllyConnected);
    }
}
