using Lyra.Core.API;
using Lyra.Core.Blocks;
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
        protected override async Task<APIResultCodes> ValidateFeeAsync(DagSystem sys, TransactionBlock block)
        {
            APIResultCodes result = APIResultCodes.Success;
            if (block.FeeType != AuthorizationFeeTypes.Regular)
                result = APIResultCodes.InvalidFeeAmount;

            if (block.Fee != _calculator.PayToAuthorizer)
                result = APIResultCodes.InvalidFeeAmount;

            return result;
        }

        protected override async Task<APIResultCodes> VerifyBlockAsync(DagSystem sys, Block block, Block previousBlock)
        {
            var swapOutBlock = block as PoolSwapOutBlock;
            if (swapOutBlock == null)
                return APIResultCodes.InvalidBlockType;

            var relatedTransactions = await sys.Storage.FindBlockByRelatedTxAsync(swapOutBlock.RelatedTx);
            if (relatedTransactions != null)
                return APIResultCodes.PoolOperationAlreadyCompleted;

            var poolId = (block as PoolSwapOutBlock).AccountID;
            var poolGenesis = await sys.Storage.FindFirstBlockAsync(poolId) as PoolGenesisBlock;
            var poolLatestBlock = await sys.Storage.FindLatestBlockAsync(poolId) as TransactionBlock;

            // get target accountId
            var relatedTxBlock = await sys.Storage.FindBlockByHashAsync(swapOutBlock.RelatedTx) as ReceiveTransferBlock;
            var originalSendBlock = await sys.Storage.FindBlockByHashAsync(relatedTxBlock.SourceHash) as SendTransferBlock;
            var targetAccountId = originalSendBlock.AccountID;

            if (targetAccountId != swapOutBlock.DestinationAccountId)
                return APIResultCodes.InvalidPoolSwapOutAccountId;

            if (originalSendBlock.Tags == null || originalSendBlock.Tags[Block.REQSERVICETAG] != "swaptoken")
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
                return APIResultCodes.InvalidPoolSwapOutAmount;

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
            return await base.VerifyBlockAsync(sys, block, previousBlock);
        }
    }
}
