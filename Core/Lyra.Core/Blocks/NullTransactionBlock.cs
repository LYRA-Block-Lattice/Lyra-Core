using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Blocks
{
    public class NullTransactionBlock : TransactionBlock
    {
        public string FailedBlockHash { get; set; }
        public string PreviousConsolidateHash { get; set; }

        public override TransactionInfoEx GetTransaction(TransactionBlock previousBlock)
        {
            throw new NotImplementedException();
        }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.NullTransaction;
        }

        protected override string GetExtraData()
        {
            return $"{base.GetExtraData()}|{PreviousConsolidateHash}|{FailedBlockHash}";
        }
    }
}
