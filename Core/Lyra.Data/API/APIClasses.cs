using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Data.API
{
    public class UnSettledFees : IEquatable<UnSettledFees>
    {
        public string AccountId { get; set; }
        public long ServiceBlockStartHeight { get; set; }
        public long ServiceBlockEndHeight { get; set; }
        public decimal TotalFees { get; set; }

        public override bool Equals(object obj)
        {
            return Equals(obj as UnSettledFees);
        }

        public bool Equals(UnSettledFees other)
        {
            return other != null &&
                   AccountId == other.AccountId &&
                   ServiceBlockStartHeight == other.ServiceBlockStartHeight &&
                   ServiceBlockEndHeight == other.ServiceBlockEndHeight &&
                   TotalFees == other.TotalFees;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(AccountId, ServiceBlockStartHeight, ServiceBlockEndHeight, TotalFees);
        }

        public static bool operator ==(UnSettledFees left, UnSettledFees right)
        {
            return EqualityComparer<UnSettledFees>.Default.Equals(left, right);
        }

        public static bool operator !=(UnSettledFees left, UnSettledFees right)
        {
            return !(left == right);
        }
    }

    public class FeeStats
    {
        public decimal TotalFeeConfirmed { get; set; }
        public decimal TotalFeeUnConfirmed { get; set; }

        public List<RevnuItem> ConfirmedEarns { get; set; }
        public List<RevnuItem> UnConfirmedEarns { get; set; }
    }

    public class RevnuItem
    {
        public string AccId { get; set; }
        public decimal Revenue { get; set; }
    }

    public class TransStats
    {
        public long ms { get; set; }
        public BlockTypes trans { get; set; }
    }
    // when out of sync, we adjust useed, continue to save blocks, and told blockchain to do sync.
    public enum ConsensusWorkingMode { Normal, OutofSyncWaiting }

    public class NodeStatus : IEquatable<NodeStatus>
    {
        public string accountId { get; set; }
        public string version { get; set; }
        public BlockChainState state { get; set; }
        public long totalBlockCount { get; set; }
        public string lastConsolidationHash { get; set; }
        public string lastUnSolidationHash { get; set; }
        public int activePeers { get; set; }
        public int connectedPeers { get; set; }
        public DateTime now { get; set; }

        public override bool Equals(object obj)
        {
            return Equals(obj as NodeStatus);
        }

        public bool Equals(NodeStatus other)
        {
            return other != null &&
                   //accountId == other.accountId &&
                   version == other.version &&
                   state == other.state &&
                   totalBlockCount == other.totalBlockCount &&
                   lastConsolidationHash == other.lastConsolidationHash &&
                   lastUnSolidationHash == other.lastUnSolidationHash;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(version, state, totalBlockCount, lastConsolidationHash, lastUnSolidationHash);
        }

        public static bool operator ==(NodeStatus left, NodeStatus right)
        {
            return EqualityComparer<NodeStatus>.Default.Equals(left, right);
        }

        public static bool operator !=(NodeStatus left, NodeStatus right)
        {
            return !(left == right);
        }
    }

    public enum BlockChainState
    {
        NULL,
        Initializing,
        StaticSync,    // the default mode. app started. wait for p2p stack up.
        Engaging,   // storing new commit while syncing blocks
        Almighty,   // fullly synced and working
        Genesis
    }

    public class VoteQueryModel
    {
        public List<string> posAccountIds { get; set; }
        public DateTime endTime { get; set; }
    }
}
