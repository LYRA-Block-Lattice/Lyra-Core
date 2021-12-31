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
using Lyra.Data.API;
using Microsoft.Extensions.Logging;
using static Lyra.Core.Decentralize.ConsensusService;
using Akka.Actor;
using Lyra.Data.Shared;

namespace Lyra.Core.Authorizers
{
    public class ConsolidationBlockAuthorizer : BaseAuthorizer
    {
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
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
                {
                    Console.WriteLine($"real count: {allHashes.Count} but block has: {block.blockHashes.Count}");
                    return APIResultCodes.InvalidConsolidationBlockCount;
                }                    

                var mineNotYours = allHashes.Except(block.blockHashes).ToList();
                var yoursNotMine = block.blockHashes.Except(allHashes).ToList();
                if (mineNotYours.Any() || yoursNotMine.Any())
                    return APIResultCodes.InvalidConsolidationBlockHashes;
            }

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

            return await MeasureAuthAsync(base.GetType().Name, base.AuthorizeImplAsync(sys, tblock));
        }

        protected override async Task<APIResultCodes> VerifyWithPrevAsync(DagSystem sys, Block block, Block previousBlock)
        {
            var cons = block as ConsolidationBlock;
            var uniNow = DateTime.UtcNow;
            if (sys.ConsensusState != BlockChainState.StaticSync)
            {
                // time shift 10 seconds.
                if (block.TimeStamp < uniNow.AddSeconds(-60) || block.TimeStamp > uniNow.AddSeconds(3))
                {
                    _log.LogInformation($"TimeStamp 3: {block.TimeStamp} Universal Time Now: {uniNow}");
                    return APIResultCodes.InvalidBlockTimeStamp;
                }
            }

            var board = await sys.Consensus.Ask<BillBoard>(new AskForBillboard());
            if (board.CurrentLeader != cons.createdBy)
            {
                _log.LogWarning($"Invalid leader. was {cons.createdBy.Shorten()} should be {board.CurrentLeader.Shorten()}");
                return APIResultCodes.InvalidLeaderInConsolidationBlock;
            }

            var result = block.VerifySignature(board.CurrentLeader);
            if (!result)
            {
                _log.LogWarning($"VerifySignature failed for ConsolidationBlock Index: {block.Height} with Leader {board.CurrentLeader}");
                return APIResultCodes.BlockSignatureValidationFailed;
            }

            return await base.VerifyWithPrevAsync(sys, block, previousBlock);
        }
    }
}