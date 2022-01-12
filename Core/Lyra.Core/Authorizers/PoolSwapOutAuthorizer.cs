using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class PoolSwapOutAuthorizer : SendTransferAuthorizer
    {
        private SwapCalculator _calculator;

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.PoolSwapOut;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is PoolSwapOutBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as PoolSwapOutBlock;

            // also prevent race condition
            var blk = await sys.Storage.FindBlockByHashAsync(block.PreviousHash);
            if (blk is ReceiveTransferBlock recv)
            {
                if (recv.SourceHash != block.RelatedTx)
                    return APIResultCodes.InvalidRelatedTx;
            }
            else
                return APIResultCodes.InvalidBlockSequence;


            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "PoolSwapOutAuthorizer->SendTransferAuthorizer");
        }

        protected override bool IsManagedBlockAllowed(DagSystem sys, TransactionBlock block)
        {
            return true;
        }

        protected override AuthorizationFeeTypes GetFeeType()
        {
            return AuthorizationFeeTypes.Dynamic;
        }

        protected override async Task<APIResultCodes> VerifyWithPrevAsync(DagSystem sys, Block block, Block previousBlock)
        {
            var swapOutBlock = block as PoolSwapOutBlock;
            if (swapOutBlock == null)
                return APIResultCodes.InvalidBlockType;

            var relatedTransactions = await sys.Storage.FindBlocksByRelatedTxAsync(swapOutBlock.RelatedTx);
            if (relatedTransactions.Count >= 2)
                return APIResultCodes.PoolOperationAlreadyCompleted;

            var poolId = (block as PoolSwapOutBlock).AccountID;
            var poolGenesis = await sys.Storage.FindFirstBlockAsync(poolId) as PoolGenesisBlock;
            var poolLatestBlock = await sys.Storage.FindLatestBlockAsync(poolId) as TransactionBlock;

            // get target accountId
            var originalSendBlock = await sys.Storage.FindBlockByHashAsync(swapOutBlock.RelatedTx) as SendTransferBlock;
            var targetAccountId = originalSendBlock.AccountID;

            if (targetAccountId != swapOutBlock.DestinationAccountId)
                return APIResultCodes.InvalidPoolSwapOutAccountId;

            if (originalSendBlock.Tags == null || originalSendBlock.Tags[Block.REQSERVICETAG] != BrokerActions.BRK_POOL_SWAP)
                return APIResultCodes.InvalidPoolOperation;

            var prevPrevBlock = await sys.Storage.FindBlockByHashAsync(previousBlock.PreviousHash) as TransactionBlock;
            var chgs = (previousBlock as TransactionBlock).GetBalanceChanges(prevPrevBlock);

            // calculate rito by prevBlock.prevBlock
            var prevprevBlock = await sys.Storage.FindBlockByHashAsync(previousBlock.PreviousHash) as TransactionBlock;
            var swapRito = Math.Round(prevprevBlock.Balances[poolGenesis.Token0].ToBalanceDecimal() / prevprevBlock.Balances[poolGenesis.Token1].ToBalanceDecimal(), LyraGlobal.RITOPRECISION);
            if (chgs.Changes.Count != 1)
                return APIResultCodes.InvalidPoolOperation;
            string tokenIn = chgs.Changes.First().Key;
            if (tokenIn != poolGenesis.Token0 && tokenIn != poolGenesis.Token1)
                return APIResultCodes.InvalidPoolOperation;

            var kvp = chgs.Changes.First();
            var cfg = new SwapCalculator(poolGenesis.Token0, poolGenesis.Token1, prevprevBlock,
                tokenIn, chgs.Changes.First().Value, 0);

            if (cfg.SwapOutToken == null || cfg.SwapOutAmount == 0m)
                return APIResultCodes.InvalidPoolOperation;

            var currentOutChgs = swapOutBlock.GetBalanceChanges(previousBlock as TransactionBlock);
            if(currentOutChgs.Changes.Count != 1)   //plus the fee: no, fee pay by standard and is not included here
                return APIResultCodes.InvalidPoolSwapOutToken;
            if(currentOutChgs.Changes.First().Key != cfg.SwapOutToken
                || currentOutChgs.Changes.First().Value != cfg.SwapOutAmount)
            {
                Console.WriteLine($"Swap out: {currentOutChgs.Changes.First().Value} should be: {cfg.SwapOutAmount}");
                return APIResultCodes.InvalidPoolSwapOutAmount;
            }                

            // balance & share
            var curBalance = poolLatestBlock.Balances.ToDecimalDict();
            var nextBalance = poolLatestBlock.Balances.ToDecimalDict();

            var tokenOut = cfg.SwapOutToken;
            if(tokenIn == LyraGlobal.OFFICIALTICKERCODE)
            {
                // tokenIn == LYR
                nextBalance[tokenIn] = curBalance[tokenIn] - cfg.PayToAuthorizer;  // pool fee leave in the pool
                nextBalance[tokenOut] = curBalance[tokenOut] - cfg.SwapOutAmount;
            }
            else
            {
                // tokenOut == LYR
                nextBalance[tokenIn] = curBalance[tokenIn];  // pool fee leave in the pool
                nextBalance[tokenOut] = curBalance[tokenOut] - cfg.SwapOutAmount - cfg.PayToAuthorizer;
            }

            var outBalances = nextBalance.ToLongDict();
            var outShares = (poolLatestBlock as IPool).Shares;

            if (!swapOutBlock.Balances.OrderBy(a => a.Key)
                .SequenceEqual(outBalances.OrderBy(a => a.Key)))
                return APIResultCodes.InvalidPoolSwapOutAmount;

            if (!swapOutBlock.Shares.OrderBy(a => a.Key)
                .SequenceEqual(outShares.OrderBy(a => a.Key)))
                return APIResultCodes.InvalidPoolSwapOutShare;

            _calculator = cfg;
            return await base.VerifyWithPrevAsync(sys, block, previousBlock);
        }
    }
}
