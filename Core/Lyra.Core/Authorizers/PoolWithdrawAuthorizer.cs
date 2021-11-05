using Lyra.Core.API;
using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class PoolWithdrawAuthorizer : SendTransferAuthorizer
    {
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is PoolWithdrawBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as PoolWithdrawBlock;

            var send = await sys.Storage.FindBlockByHashAsync(block.RelatedTx) as SendTransferBlock;
            if (send == null || send.DestinationAccountId != PoolFactoryBlock.FactoryAccount)
                return APIResultCodes.InvalidMessengerAccount;

            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(block.RelatedTx);
            if(blocks.Count != 0)
                return APIResultCodes.InvalidRelatedTx;

            return await base.AuthorizeImplAsync(sys, tblock);
        }

        protected override AuthorizationFeeTypes GetFeeType()
        {
            return AuthorizationFeeTypes.NoFee;
        }

        protected override async Task<APIResultCodes> VerifyBlockAsync(DagSystem sys, Block block, Block previousBlock)
        {
            var withdrawBlock = block as PoolWithdrawBlock;
            if (withdrawBlock == null)
                return APIResultCodes.InvalidBlockType;

            var relatedTransactions = await sys.Storage.FindBlocksByRelatedTxAsync(withdrawBlock.RelatedTx);
            if (relatedTransactions.Count >= 2)
                return APIResultCodes.PoolOperationAlreadyCompleted;

            var poolId = (block as PoolWithdrawBlock).AccountID;
            var poolGenesisBlock = await sys.Storage.FindFirstBlockAsync(poolId) as PoolGenesisBlock;
            var poolLatestBlock = await sys.Storage.FindLatestBlockAsync(poolId) as TransactionBlock;

            var curBalance = poolLatestBlock.Balances.ToDecimalDict();
            var curShares = (poolLatestBlock as IPool).Shares.ToRitoDecimalDict();

            var nextBalance = poolLatestBlock.Balances.ToDecimalDict();
            var nextShares = (poolLatestBlock as IPool).Shares.ToRitoDecimalDict();

            // get target accountId
            var originalSendBlock = await sys.Storage.FindBlockByHashAsync(withdrawBlock.RelatedTx) as SendTransferBlock;
            var targetAccountId = originalSendBlock.AccountID;

            if (targetAccountId != withdrawBlock.DestinationAccountId)
                return APIResultCodes.InvalidPoolWithdrawAccountId;

            var usersShare = curShares[targetAccountId];
            var amountsToSend = new Dictionary<string, decimal>
            {
                { poolGenesisBlock.Token0, curBalance[poolGenesisBlock.Token0] * usersShare },
                { poolGenesisBlock.Token1, curBalance[poolGenesisBlock.Token1] * usersShare }
            };

            nextBalance[poolGenesisBlock.Token0] -= amountsToSend[poolGenesisBlock.Token0];
            nextBalance[poolGenesisBlock.Token1] -= amountsToSend[poolGenesisBlock.Token1];
            nextShares.Remove(targetAccountId);

            foreach (var share in curShares)
            {
                if (share.Key == targetAccountId)
                    continue;

                nextShares[share.Key] = (share.Value * curBalance[poolGenesisBlock.Token0]) / nextBalance[poolGenesisBlock.Token0];
            }

            if (!withdrawBlock.Balances.OrderBy(a => a.Key)
                .SequenceEqual(nextBalance.ToLongDict().OrderBy(a => a.Key)))
                return APIResultCodes.InvalidPoolWithdrawAmount;

            if (!withdrawBlock.Shares.OrderBy(a => a.Key)
                .SequenceEqual(nextShares.ToRitoLongDict().OrderBy(a => a.Key)))
                return APIResultCodes.InvalidPoolWithdrawRito;

            return await base.VerifyBlockAsync(sys, block, previousBlock);
        }
    }
}
