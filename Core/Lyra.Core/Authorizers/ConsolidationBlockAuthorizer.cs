using System;
using System.Collections.Generic;
using Lyra.Core.Blocks;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Lyra.Core.Utils;
using Lyra.Core.Accounts;
using Clifton.Blockchain;
using System.Linq;
using Lyra.Core.API;

namespace Lyra.Core.Authorizers
{
    public class ConsolidationBlockAuthorizer : BaseAuthorizer
    {
        public ConsolidationBlockAuthorizer()
        {
        }

        public async override Task<(APIResultCodes, AuthorizationSignature)> AuthorizeAsync<T>(DagSystem sys, T tblock)
        {
            var result = await AuthorizeImplAsync(sys, tblock);
            if (APIResultCodes.Success == result)
                return (APIResultCodes.Success, Sign(sys, tblock));
            else
                return (result, (AuthorizationSignature)null);
        }

        protected override Task<APIResultCodes> ValidateFeeAsync(DagSystem sys, TransactionBlock block)
        {
            return Task.FromResult(APIResultCodes.Success);
        }

        private async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is ConsolidationBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as ConsolidationBlock;

            //// 1. check if the block already exists
            //if (null != await DagSystem.Singleton.Storage.GetBlockByUIndexAsync(block.UIndex))
            //    return APIResultCodes.BlockWithThisUIndexAlreadyExists;

            var lastCons = await sys.Storage.GetLastConsolidationBlockAsync();
            if(block.Height > 1)
            {
                if (lastCons == null)
                    return APIResultCodes.CouldNotFindLatestBlock;

                // make sure the first hash is ALWAYS the previous consblock (except the first one)
                if(block.blockHashes.First() != lastCons.Hash)
                {
                    return APIResultCodes.InvalidConsolidationBlockContinuty;
                }

                var allHashes = (await sys.Storage.GetBlockHashesByTimeRangeAsync(lastCons.TimeStamp, block.TimeStamp)).ToList();
                if (block.blockHashes.Count != allHashes.Count)
                    return APIResultCodes.InvalidConsolidationBlockCount;

                var mineNotYours = allHashes.Except(block.blockHashes).ToList();
                var yoursNotMine = block.blockHashes.Except(allHashes).ToList();
                if (mineNotYours.Any() || yoursNotMine.Any())
                    return APIResultCodes.InvalidConsolidationBlockHashes;
            }

            var result = await VerifyBlockAsync(sys, block, lastCons);
            if (result != APIResultCodes.Success)
                return result;

            // recalculate merkeltree
            // use merkle tree to consolidate all previous blocks, from lastCons.UIndex to consBlock.UIndex -1
            var mt = new MerkleTree();
            decimal feeAggregated = 0;
            foreach (var hash in block.blockHashes)
            {
                mt.AppendLeaf(MerkleHash.Create(hash));

                // verify block exists
                if (null == await sys.Storage.FindBlockByHashAsync(hash))
                    return APIResultCodes.BlockNotFound;

                // aggregate fees
                var transBlock = (await sys.Storage.FindBlockByHashAsync(hash)) as TransactionBlock;
                if (transBlock != null)
                {
                    feeAggregated += transBlock.Fee;
                }
            }

            var mkhash = mt.BuildTree().ToString();
            if(block.MerkelTreeHash != mkhash)
                return APIResultCodes.InvalidConsolidationMerkleTreeHash;

            if (block.totalFees != feeAggregated.ToBalanceLong())
                return APIResultCodes.InvalidConsolidationTotalFees;

            // consolidation must come from leader node
            // done in base authorizer already!

            return APIResultCodes.Success;
        }

    }
}