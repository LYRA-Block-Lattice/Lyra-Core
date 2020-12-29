using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Blocks
{
    public class PoolAddLiquidateBlock : ReceiveTransferBlock
    {
        // because token always has a uniq name, we order it as alphbet order. token0 < token1
        public string Token0 { get; set; }
        public string Token1 { get; set; }

        public Decimal Token0Amount { get; set; }
        public Decimal Token1Amount { get; set; }
    }
}
