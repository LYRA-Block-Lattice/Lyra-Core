using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Lyra.Data.API
{
    public class UnSettledFees : IEquatable<UnSettledFees>
    {
        public string AccountId { get; set; } = null!;
        public long ServiceBlockStartHeight { get; set; }
        public long ServiceBlockEndHeight { get; set; }
        public decimal TotalFees { get; set; }

        public override bool Equals(object? obj)
        {
            if(obj == null)
                return false;

            if (obj.GetType() != typeof(UnSettledFees))
                return false;

            return Equals(obj as UnSettledFees);
        }

        public bool Equals(UnSettledFees? other)
        {
            if (other is null)
                return false;

            return 
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

        public List<RevnuItem> ConfirmedEarns { get; set; } = null!;
        public List<RevnuItem> UnConfirmedEarns { get; set; } = null!;

        public override bool Equals(object? obj)
        {
            return Equals(obj as FeeStats);
        }

        public bool Equals(FeeStats? other)
        {
            return other != null &&
                   TotalFeeConfirmed == other.TotalFeeConfirmed &&
                   TotalFeeUnConfirmed == other.TotalFeeUnConfirmed;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = base.GetHashCode() + 19;
                if (null != ConfirmedEarns)
                    foreach (var t in ConfirmedEarns)
                    {
                        hash = hash * 31 + (t == null ? 0 : t.GetHashCode());
                    }
                if (null != UnConfirmedEarns)
                    foreach (var t in UnConfirmedEarns)
                    {
                        hash = hash * 31 + (t == null ? 0 : t.GetHashCode());
                    }
                return HashCode.Combine(hash, TotalFeeConfirmed, TotalFeeUnConfirmed);
            }
        }
    }

    public class RevnuItem
    {
        public string AccId { get; set; } = null!;
        public decimal Revenue { get; set; }

        public override int GetHashCode()
        {
            return HashCode.Combine(AccId, Revenue);
        }
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
        public string accountId { get; set; } = null!;
        public string? version { get; set; }
        public BlockChainState state { get; set; }
        public long totalBlockCount { get; set; }
        public string? lastConsolidationHash { get; set; }
        public string? lastUnSolidationHash { get; set; }
        public int activePeers { get; set; }
        public int connectedPeers { get; set; }
        public DateTime now { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as NodeStatus);
        }

        public bool Equals(NodeStatus? other)
        {
            if (other is null)
                return false;

            return 
                   //accountId == other.accountId &&
                   //version == other.version &&
                   //state == other.state &&
                   totalBlockCount == other.totalBlockCount &&
                   lastConsolidationHash == other.lastConsolidationHash &&
                   lastUnSolidationHash == other.lastUnSolidationHash;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(totalBlockCount, lastConsolidationHash, lastUnSolidationHash);
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
        public List<string> posAccountIds { get; set; } = null!;
        public DateTime endTime { get; set; }
    }

    public class TradeStatsReq
    {
        public List<string> AccountIDs { get; set; } = null!;
    }

    public class TradeStats
    {
        public string AccountId { get; set; } = null!;
        public int TotalTrades { get; set; }
        public int FinishedCount { get; set; }
    }
}
