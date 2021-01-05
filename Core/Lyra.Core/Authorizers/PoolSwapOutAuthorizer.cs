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
        protected override async Task<APIResultCodes> ValidateFeeAsync(DagSystem sys, TransactionBlock block)
        {
            APIResultCodes result = APIResultCodes.Success;
            if (block.FeeType != AuthorizationFeeTypes.NoFee)
                result = APIResultCodes.InvalidFeeAmount;

            if (block.Fee != 0)
                result = APIResultCodes.InvalidFeeAmount;

            return result;
        }

        protected override async Task<APIResultCodes> VerifyBlockAsync(DagSystem sys, Block block, Block previousBlock)
        {
            var swapOutBlock = block as PoolSwapOutBlock;
            if (swapOutBlock == null)
                return APIResultCodes.InvalidBlockType;

            var relatedTransactions = await sys.Storage.FindBlockInRelation(swapOutBlock.RelatedTx);
            if (relatedTransactions.Any())
                return APIResultCodes.PoolOperationAlreadyCompleted;

            var poolId = (block as PoolSwapOutBlock).AccountID;
            var poolGenesisBlock = await sys.Storage.FindFirstBlockAsync(poolId) as PoolGenesisBlock;
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
            var swapRito = prevprevBlock.Balances[poolGenesisBlock.Token0].ToBalanceDecimal() / prevprevBlock.Balances[poolGenesisBlock.Token1].ToBalanceDecimal();
            if (chgs.Changes.Count != 1)
                return APIResultCodes.InvalidPoolOperation;
            string tokenIn = chgs.Changes.First().Key;
            if (tokenIn != poolGenesisBlock.Token0 && tokenIn != poolGenesisBlock.Token1)
                return APIResultCodes.InvalidPoolOperation;
            string tokenOut = null;
            decimal tokenOutAmount = 0m;
            if(tokenIn == poolGenesisBlock.Token0)
            {
                tokenOut = poolGenesisBlock.Token1;
                tokenOutAmount = Math.Round(chgs.Changes.First().Value / swapRito, 8);
            }
            else if(tokenIn == poolGenesisBlock.Token1)
            {
                tokenOut = poolGenesisBlock.Token0;
                tokenOutAmount = Math.Round(chgs.Changes.First().Value * swapRito, 8);
            }
            if (tokenOut == null || tokenOutAmount == 0m)
                return APIResultCodes.InvalidPoolOperation;

            var currentOutChgs = swapOutBlock.GetBalanceChanges(previousBlock as TransactionBlock);
            if(currentOutChgs.Changes.Count != 1)
                return APIResultCodes.InvalidPoolOperation;
            if(currentOutChgs.Changes.First().Key != tokenOut 
                || currentOutChgs.Changes.First().Value != tokenOutAmount)
                return APIResultCodes.InvalidPoolOperation;

            // balance & share
            var curBalance = poolLatestBlock.Balances.ToDecimalDict();
            var nextBalance = poolLatestBlock.Balances.ToDecimalDict();

            nextBalance[tokenOut] = curBalance[tokenOut] - tokenOutAmount;

            var outBalances = nextBalance.ToLongDict();
            var outShares = (poolLatestBlock as IPool).Shares;

            if (!swapOutBlock.Balances.OrderBy(a => a.Key)
                .SequenceEqual(outBalances.OrderBy(a => a.Key)))
                return APIResultCodes.InvalidPoolSwapOutAmount;

            if (!swapOutBlock.Shares.OrderBy(a => a.Key)
                .SequenceEqual(outShares.OrderBy(a => a.Key)))
                return APIResultCodes.InvalidPoolSwapOutShare;

            return await base.VerifyBlockAsync(sys, block, previousBlock);
        }
    }
}
