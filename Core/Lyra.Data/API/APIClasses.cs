using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Data.API
{
    public class UnSettledFees
    {
        public string AccountId { get; set; }
        public long ServiceBlockStartHeight { get; set; }
        public long ServiceBlockEndHeight { get; set; }
        public decimal TotalFees { get; set; }
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

    public class NodeStatus
    {
        public string accountId { get; set; }
        public string version { get; set; }
        public BlockChainState state { get; set; }
        public long totalBlockCount { get; set; }
        public string lastConsolidationHash { get; set; }
        public string lastUnSolidationHash { get; set; }
        public int activePeers { get; set; }
        public int connectedPeers { get; set; }

        //public override bool Equals(object obj)
        //{
        //	if(obj is NodeStatus)
        //	{
        //		var ns = obj as NodeStatus;
        //		return version == ns.version
        //			&& totalBlockCount == ns.totalBlockCount
        //			&& lastConsolidationHash == ns.lastConsolidationHash
        //			&& lastUnSolidationHash == ns.lastUnSolidationHash;				
        //	}
        //	return base.Equals(obj);
        //}
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
