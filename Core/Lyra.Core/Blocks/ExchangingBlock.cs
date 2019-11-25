using Lyra.Core.Blocks.Transactions;
using Lyra.Core.Protos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Blocks
{
    public class ExchangingBlock : SendTransferBlock
    {
        public const decimal FEE = 0.01m;

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.ExchangingTransfer;
        }
    }
}
