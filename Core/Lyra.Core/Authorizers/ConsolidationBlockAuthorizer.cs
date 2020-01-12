using System;
using System.Collections.Generic;
using Lyra.Core.Blocks;
using Lyra.Core.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Lyra.Core.Utils;
using Lyra.Core.Accounts;
using Clifton.Blockchain;

namespace Lyra.Core.Authorizers
{
    public class ConsolidationBlockAuthorizer : BaseAuthorizer
    {
        public ConsolidationBlockAuthorizer()
        {
        }

        public override (APIResultCodes, AuthorizationSignature) Authorize<T>(T tblock, bool WithSign = true)
        {
            var result = AuthorizeImpl(tblock);
            if (APIResultCodes.Success == result)
                return (APIResultCodes.Success, Sign(tblock));
            else
                return (result, (AuthorizationSignature)null);
        }

        protected override APIResultCodes ValidateFee(TransactionBlock block)
        {
            return APIResultCodes.Success;
        }

        private APIResultCodes AuthorizeImpl<T>(T tblock)
        {
            if (!(tblock is ConsolidationBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as ConsolidationBlock;

            // 1. check if the block already exists
            if (null != BlockChain.Singleton.GetBlockByUIndex(block.UIndex))
                return APIResultCodes.BlockWithThisUIndexAlreadyExists;

            var lastCons = BlockChain.Singleton.GetSyncBlock();
            if (lastCons == null)
                return APIResultCodes.CouldNotFindLatestBlock;

            var result = VerifyBlock(block, lastCons);
            if (result != APIResultCodes.Success)
                return result;

            // recalculate merkeltree
            // use merkle tree to consolidate all previous blocks, from lastCons.UIndex to consBlock.UIndex -1
            var mt = new MerkleTree();
            for (var ndx = lastCons.UIndex; ndx < block.UIndex; ndx++)
            {
                var bndx = BlockChain.Singleton.GetBlockByUIndex(ndx);
                var mhash = MerkleHash.Create(bndx.UHash);
                mt.AppendLeaf(mhash);
            }
            var mkhash = mt.BuildTree().ToString();
            if (block.MerkelTreeHash != mkhash)
                return APIResultCodes.InvalidConsolidationMerkleTreeHash;

            return APIResultCodes.Success;
        }

    }
}