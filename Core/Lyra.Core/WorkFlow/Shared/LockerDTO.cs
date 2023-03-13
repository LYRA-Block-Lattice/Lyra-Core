using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.WorkFlow.Shared
{
    /// <summary>
    /// a block/wf always has some chains to lockup.
    /// </summary>
    public class LockerDTO
    {
        /// <summary>
        /// the block hash which trigged the consensus
        /// </summary>
        public string reqhash { get; set; } = null!;

        /// <summary>
        /// need workflow or not
        /// </summary>
        public bool haswf { get; set; }

        /// <summary>
        /// if has workflow, the worflow's ID
        /// </summary>
        public string? workflowid { get; set; }

        /// <summary>
        /// TX chains to lockup, accound ID.
        /// cascading lockup: a workflow should predicate *ALL* possible lockups. 
        ///   if it can't, split the workflow into multiple ones.
        /// </summary>
        public List<string> lockedups { get; set; } = null!;

        /// <summary>
        /// if has workflow, all generated hashes
        /// </summary>
        public List<string> seqhashes { get; set; } = null!;
    }
}
