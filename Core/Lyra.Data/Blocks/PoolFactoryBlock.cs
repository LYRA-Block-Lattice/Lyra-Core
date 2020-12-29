using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Blocks
{
    // user send the specified amount fee to pool factory
    // pool factory will generate a new pool account
    // user send funds to the pool to create it
    public class PoolFactoryBlock : ReceiveTransferBlock
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.PoolFactory;
        }
    }
}
