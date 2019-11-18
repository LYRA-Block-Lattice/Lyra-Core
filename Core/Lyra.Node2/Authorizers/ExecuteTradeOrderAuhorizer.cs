using System;
using System.Collections.Generic;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;

using Lyra.Core.Cryptography;
using Lyra.Core.API;
using Lyra.Core.Accounts.Node;
using Lyra.Node2.Services;
using Lyra.Core.Protos;

namespace Lyra.Node2.Authorizers
{
    public class ExecuteTradeOrderAuthorizer: SendTransferAuthorizer
    {
        TradeMatchEngine _TradeMatchEngine;

        public ExecuteTradeOrderAuthorizer(ServiceAccount serviceAccount, IAccountCollection accountCollection, TradeMatchEngine tradeMatchEngine) : base(serviceAccount, accountCollection)
        {
            _TradeMatchEngine = tradeMatchEngine;
        }

        public override APIResultCodes Authorize<T>(ref T tblock)
        {
            if (!(tblock is ExecuteTradeOrderBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as ExecuteTradeOrderBlock;

            // 1. check if the account already exists
            if (!_accountCollection.AccountExists(block.AccountID))
                return APIResultCodes.AccountDoesNotExist;

            TransactionBlock lastBlock = _accountCollection.FindLatestBlock(block.AccountID);
            if (lastBlock == null)
                return APIResultCodes.CouldNotFindLatestBlock;

            var result = VerifyBlock(block, lastBlock);
            if (result != APIResultCodes.Success)
                return result;

            // Validate the destination account id
            if (!Signatures.ValidateAccountId(block.DestinationAccountId))
                return APIResultCodes.InvalidDestinationAccountId;

            result = VerifyTransactionBlock(block);
            if (result != APIResultCodes.Success)
                return result;

            //if (!block.ValidateTransaction(lastBlock))
            //    return APIResultCodes.SendTransactionValidationFailed;

            // To DO validate the transaction amount (that it matches the trade amd the order)

            result = ValidateNonFungible(block, lastBlock);
            if (result != APIResultCodes.Success)
                return result;

            // Validate the linked trade
            var trade = _accountCollection.FindBlockByHash(block.DestinationAccountId, block.TradeId) as TradeBlock;
            if (trade == null)
                return APIResultCodes.NoTradesFound;

            if (block.SellTokenCode != trade.BuyTokenCode)
                return APIResultCodes.TransactionTokenDoesNotMatch;

            // TO DO validate amounts match

            // Validate the linked trade
            var trade_order = _accountCollection.FindBlockByHash(block.AccountID, block.TradeOrderId) as TradeOrderBlock;
            if (trade_order == null)
                return APIResultCodes.TradeOrderNotFound;

            if (block.SellTokenCode != trade_order.SellTokenCode)
                return APIResultCodes.TransactionTokenDoesNotMatch;

            // TO DO validate amounts match

            _accountCollection.AddBlock(block);

            //// Deactivate the order if it allows only one matching trade
            //if (trade_order.MaxQuantity == 1)
            //    _TradeMatchEngine.RemoveOrder(trade_order);

            return APIResultCodes.Success;

        }

        protected override APIResultCodes ValidateFee(TransactionBlock block)
        {
            var order = _accountCollection.FindBlockByHash((block as ExecuteTradeOrderBlock).TradeOrderId) as TradeOrderBlock;
            if (order == null)
                return APIResultCodes.NoTradesFound;

            if (order.CoverAnotherTradersFee)
            {
                if (block.FeeType != AuthorizationFeeTypes.BothParties)
                    return APIResultCodes.InvalidFeeAmount;
                if (block.Fee != _serviceAccount.GetLastServiceBlock().TradeFee * 2)
                    return APIResultCodes.InvalidFeeAmount;
            }
            else
            if (order.AnotherTraderWillCoverFee)
            {
                if (block.FeeType == AuthorizationFeeTypes.NoFee)
                    return APIResultCodes.InvalidFeeAmount;
                if (block.Fee != 0)
                    return APIResultCodes.InvalidFeeAmount;
            }
            else
            {

                if (block.FeeType == AuthorizationFeeTypes.Regular)
                    return APIResultCodes.InvalidFeeAmount;
                if (block.Fee != _serviceAccount.GetLastServiceBlock().TradeFee)
                    return APIResultCodes.InvalidFeeAmount;
            }

            return APIResultCodes.Success;
        }

    }
}
