using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Blocks
{
    public interface IBlockConsensus
    {
        long GenerateUniversalBlockId();
    }
}
