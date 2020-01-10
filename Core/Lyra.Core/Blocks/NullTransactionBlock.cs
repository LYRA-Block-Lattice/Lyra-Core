using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Blocks
{
    public class NullTransactionBlock : TransactionBlock
    {
        public override TransactionInfoEx GetTransaction(TransactionBlock previousBlock)
        {
            throw new NotImplementedException();
        }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.NullTransaction;
        }
    }
}
