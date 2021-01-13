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

            if (block.Fee != _calculator.protocolFee)
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

            var OriginalSendBlockPrevBlock = await sys.Storage.FindBlockByHashAsync(originalSendBlock.PreviousHash) as TransactionBlock;
            var chgs = originalSendBlock.GetBalanceChanges(OriginalSendBlockPrevBlock);

            // calculate rito by prevBlock.prevBlock
            var prevprevBlock = await sys.Storage.FindBlockByHashAsync(previousBlock.PreviousHash) as TransactionBlock;
            var swapRito = Math.Round(prevprevBlock.Balances[poolGenesis.Token0].ToBalanceDecimal() / prevprevBlock.Balances[poolGenesis.Token1].ToBalanceDecimal(), LyraGlobal.RITOPRECISION);
            if (chgs.Changes.Count != 1)
                return APIResultCodes.InvalidPoolOperation;
            string tokenIn = chgs.Changes.First().Key;
            if (tokenIn != poolGenesis.Token0 && tokenIn != poolGenesis.Token1)
                return APIResultCodes.InvalidPoolOperation;

            var kvp = chgs.Changes.First();
            var cfg = new SwapCalculator(kvp.Key, kvp.Value, poolGenesis, swapRito);

            if (cfg.swapOutToken == null || cfg.swapOutAmount == 0m)
                return APIResultCodes.InvalidPoolOperation;

            var currentOutChgs = swapOutBlock.GetBalanceChanges(previousBlock as TransactionBlock);
            if(currentOutChgs.Changes.Count != 1)   //plus the fee: no, fee pay by standard and is not included here
                return APIResultCodes.InvalidPoolSwapOutToken;
            if(currentOutChgs.Changes.First().Key != cfg.swapOutToken
                || currentOutChgs.Changes.First().Value != cfg.swapOutAmount)
                return APIResultCodes.InvalidPoolSwapOutAmount;

            // balance & share
            var curBalance = poolLatestBlock.Balances.ToDecimalDict();
            var nextBalance = poolLatestBlock.Balances.ToDecimalDict();

            var tokenOut = cfg.swapOutToken;
            if(tokenIn == LyraGlobal.OFFICIALTICKERCODE)
            {
                // tokenIn == LYR
                nextBalance[tokenIn] = curBalance[tokenIn] - cfg.protocolFee;  // pool fee leave in the pool
                nextBalance[tokenOut] = curBalance[tokenOut] - cfg.swapOutAmount;
            }
            else
            {
                // tokenOut == LYR
                nextBalance[tokenIn] = curBalance[tokenIn];  // pool fee leave in the pool
                nextBalance[tokenOut] = curBalance[tokenOut] - cfg.swapOutAmount - cfg.protocolFee;
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

    public class SwapCalculator
    {
        // calculate the fee. 0.2%. half go to liquidate providers, half goto node operators (as fee)
        // reduct from swap in token
        public string swapInToken { get; private set; }
        public string swapOutToken { get; private set; }
        public decimal swapInAmount { get; private set; }
        public decimal swapOutAmount { get; private set; }
        public decimal poolFee { get; private set; }
        public decimal protocolFee { get; private set; }

        public SwapCalculator(string swapFromToken, decimal originalAmount, PoolGenesisBlock poolGenesis, decimal swapRito)
        {
            if (swapFromToken == poolGenesis.Token0 && poolGenesis.Token0 == LyraGlobal.OFFICIALTICKERCODE)
            {
                swapInToken = poolGenesis.Token0;
                swapOutToken = poolGenesis.Token1;

                // LYR -> other token
                swapInAmount = Math.Round(originalAmount * 0.998m, 8);
                swapOutAmount = Math.Round(swapInAmount / swapRito, 8);
                poolFee = Math.Round(originalAmount * 0.001m, 8);
                protocolFee = Math.Round(originalAmount * 0.001m, 8);
            }
            else if (swapFromToken == poolGenesis.Token0 && poolGenesis.Token0 != LyraGlobal.OFFICIALTICKERCODE)
            {
                swapInToken = poolGenesis.Token0;
                swapOutToken = poolGenesis.Token1;

                // other token -> LYR
                swapInAmount = originalAmount;
                var swapOutTotal = swapInAmount / swapRito;

                swapOutAmount = Math.Round(swapOutTotal * 0.998m, 8);
                poolFee = Math.Round(swapOutTotal * 0.001m, 8);
                protocolFee = Math.Round(swapOutTotal * 0.001m, 8);
            }
            else if (swapFromToken == poolGenesis.Token1 && poolGenesis.Token1 == LyraGlobal.OFFICIALTICKERCODE)
            {
                swapInToken = poolGenesis.Token1;
                swapOutToken = poolGenesis.Token0;

                // LYR -> other token
                swapInAmount = Math.Round(originalAmount * 0.998m, 8);
                poolFee = Math.Round(originalAmount * 0.001m, 8);
                protocolFee = Math.Round(originalAmount * 0.001m, 8);

                swapOutAmount = Math.Round(swapInAmount * swapRito, 8);
            }
            else if (swapFromToken == poolGenesis.Token1 && poolGenesis.Token1 != LyraGlobal.OFFICIALTICKERCODE)
            {
                swapInToken = poolGenesis.Token1;
                swapOutToken = poolGenesis.Token0;

                // other token -> LYR
                swapInAmount = originalAmount;
                var swapOutTotal = swapInAmount * swapRito;

                swapOutAmount = Math.Round(swapOutTotal * 0.998m, 8);
                poolFee = Math.Round(swapOutTotal * 0.001m, 8);
                protocolFee = Math.Round(swapOutTotal * 0.001m, 8);
            }
        }
    }
}
