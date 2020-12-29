using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Blocks
{
    /// <summary>
    /// 
    /// </summary>
    public class PoolBlock : ReceiveTransferBlock
    {
        public string Token0 { get; set; }
        public string Token1 { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.Pool;
        }

        // the balance is always 3: 
        // PoolToken -> 1M
        // Token0 -> dynamic
        // Token1 -> dynamic

        // AccountId -> Share
        // Initial pool token is 1M
        // make sure sum(share) is always 1M
        public Dictionary<string, long> Shares { get; set; }
    }
}
