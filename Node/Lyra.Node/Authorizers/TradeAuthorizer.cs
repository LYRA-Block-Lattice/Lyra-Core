using System;
using System.Collections.Generic;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;

using Lyra.Core.Cryptography;
using Lyra.Core.API;
using Lyra.Core.Accounts.Node;

namespace Lyra.Node.Authorizers
{
    public class TradeAuthorizer: BaseAuthorizer
    {
        TradeMatchEngine _TradeMatchEngine;

        public TradeAuthorizer(ServiceAccount serviceAccount, IAccountCollection accountCollection, TradeMatchEngine tradeMatchEngine) : base(serviceAccount, accountCollection)
        {
            _TradeMatchEngine = tradeMatchEngine;
        }

        public override APIResultCodes Authorize<T>(ref T tblock)
        {
            if (!(tblock is TradeBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as TradeBlock;

            // 1. check if the account already exists
            if (!_accountCollection.AccountExists(block.AccountID))
                return APIResultCodes.AccountDoesNotExist;

            TransactionBlock lastBlock = _accountCollection.FindLatestBlock(block.AccountID);
            if (lastBlock == null)
                return APIResultCodes.CouldNotFindLatestBlock;

            var result = VerifyBlock(block, lastBlock);
            if (result != APIResultCodes.Success)
                return result;

            //if (lastBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE] <= block.Balances[TokenGenesisBlock.LYRA_TICKER_CODE] + block.Fee)
            //    return AuthorizationResultCodes.NegativeTransactionAmount;

            // Validate the destination account id
            if (!Signatures.ValidateAccountId(block.DestinationAccountId))
                return APIResultCodes.InvalidDestinationAccountId;

            result = VerifyTransactionBlock(block);
            if (result != APIResultCodes.Success)
                return result;

            if (!block.ValidateTransaction(lastBlock))
                return APIResultCodes.SendTransactionValidationFailed;

            // TP DO validate the original order
            var original_order = _accountCollection.FindBlockByHash(block.DestinationAccountId, block.TradeOrderId) as TradeOrderBlock;
            if (original_order == null)
                return APIResultCodes.TradeOrderValidationFailed;

            if (original_order.SellTokenCode != block.BuyTokenCode)
                return APIResultCodes.TradeOrderValidationFailed;

            if (original_order.BuyTokenCode != block.SellTokenCode)
                return APIResultCodes.TradeOrderValidationFailed;

            // TO DO validate amounts

            Sign(ref block);

            _accountCollection.AddBlock(block);

            // Deactivate the order if it allows only one matching trade
            if (original_order.MaxQuantity == 1)
                _TradeMatchEngine.RemoveOrder(original_order);

            return APIResultCodes.Success;

        }

        protected override APIResultCodes ValidateFee(TransactionBlock block)
        {
            var order = _accountCollection.FindBlockByHash((block as TradeBlock).TradeOrderId) as TradeOrderBlock;
            if (order == null)
                return APIResultCodes.NoTradesFound;

            if (order.CoverAnotherTradersFee)
            {
                // we don't pay fee as it paid by another party
                if (block.FeeType != AuthorizationFeeTypes.NoFee)
                    return APIResultCodes.InvalidFeeType;
                if (block.Fee != 0)
                    return APIResultCodes.InvalidFeeAmount;
                return APIResultCodes.Success;
            }
            else
            if (order.AnotherTraderWillCoverFee)
            {
                if (block.FeeType != AuthorizationFeeTypes.BothParties)
                    return APIResultCodes.InvalidFeeType;
                if (block.Fee != _serviceAccount.GetLastServiceBlock().TradeFee * 2)
                    return APIResultCodes.InvalidFeeAmount;
                return APIResultCodes.Success;
            }
            else
            {
                // regular fee
                if (block.FeeType != AuthorizationFeeTypes.Regular)
                    return APIResultCodes.InvalidFeeType;

                if (block.Fee != _serviceAccount.GetLastServiceBlock().TradeFee)
                    return APIResultCodes.InvalidFeeAmount;

                return APIResultCodes.Success;
            }
        }
    }
}
