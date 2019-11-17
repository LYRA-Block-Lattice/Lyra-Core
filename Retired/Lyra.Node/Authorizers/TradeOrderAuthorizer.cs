using System;
using System.Collections.Generic;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;

using Lyra.Core.Cryptography;
using Lyra.Core.API;
using Lyra.Core.Accounts.Node;

namespace Lyra.Node.Authorizers
{
    public class TradeOrderAuthorizer: BaseAuthorizer
    {
        TradeMatchEngine _TradeMatchEngine;

        public TradeOrderAuthorizer(ServiceAccount serviceAccount, IAccountCollection accountCollection, TradeMatchEngine tradeMatchEngine) : base(serviceAccount, accountCollection)
        {
            _TradeMatchEngine = tradeMatchEngine;
        }

        public TradeBlock MatchTradeBlock { get; set; }

        public override APIResultCodes Authorize<T>(ref T tblock)
        {
            if (!(tblock is TradeOrderBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as TradeOrderBlock;

            if (block.MaxQuantity != 1)
                return APIResultCodes.FeatureIsNotSupported;

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

            // Validate the destination account id (should be empty)
            if (!string.IsNullOrWhiteSpace(block.DestinationAccountId))
                return APIResultCodes.InvalidDestinationAccountId;

            result = VerifyTransactionBlock(block);
            if (result != APIResultCodes.Success)
                return result;

            if (!block.ValidateTransaction(lastBlock))
                return APIResultCodes.TradeOrderValidationFailed;

            var transaction = block.GetTransaction(lastBlock);

            if (block.MinTradeAmount < 0 || block.MinTradeAmount > block.TradeAmount)
                return APIResultCodes.TradeOrderValidationFailed;

            if (block.SellTokenCode != transaction.TokenCode)
                return APIResultCodes.TradeOrderValidationFailed;

            var token = _accountCollection.FindTokenGenesisBlock(null, block.BuyTokenCode);
            if (token == null)
                return APIResultCodes.TradeOrderValidationFailed;

            bool res = (block.OrderType == TradeOrderTypes.Sell) ? ValidateSellOrder(block, transaction) : ValidateBuyOrder(block, transaction);

            if (!res)
                return APIResultCodes.TradeOrderValidationFailed;

            MatchTradeBlock = _TradeMatchEngine.Match(block);
            if (MatchTradeBlock != null)
                return APIResultCodes.TradeOrderMatchFound;

            Sign(ref block);

            _accountCollection.AddBlock(block);

            _TradeMatchEngine.AddOrder(block);

            return APIResultCodes.Success;

        }

        protected override APIResultCodes ValidateFee(TransactionBlock block)
        {
            if ((block as TradeOrderBlock).CoverAnotherTradersFee && (block as TradeOrderBlock).AnotherTraderWillCoverFee)
                return APIResultCodes.InvalidFeeType;

            if (block.FeeType != AuthorizationFeeTypes.NoFee)
               return APIResultCodes.InvalidFeeType;

            // no fee for the order (it will be paid in executeorder block)
            if (block.Fee != 0)
                return APIResultCodes.InvalidFeeAmount;

            return APIResultCodes.Success;
        }

        private bool ValidateSellOrder(TradeOrderBlock block, TransactionInfo transaction)
        {
            var serviceblock = _serviceAccount.GetLastServiceBlock();
            decimal balance_change = block.TradeAmount;
            decimal reference_fee = serviceblock.TradeFee;
            if (block.CoverAnotherTradersFee)
                reference_fee = serviceblock.TradeFee * 2;
            else
                if (block.AnotherTraderWillCoverFee)
                    reference_fee = 0;

            if (block.SellTokenCode == TokenGenesisBlock.LYRA_TICKER_CODE)
                balance_change += reference_fee;

            if (transaction.Amount != balance_change)
                return false;

            return true;
        }

        private bool ValidateBuyOrder(TradeOrderBlock block, TransactionInfoEx transaction)
        {
            int sell_token_precision = FindTokenPrecision(block.SellTokenCode);
            if (sell_token_precision < 0)
                return false;

            int buy_token_precision = FindTokenPrecision(block.BuyTokenCode);
            if (buy_token_precision < 0)
                return false;

            if (transaction.Amount != block.TradeAmount * block.Price)
                return false;

            var serviceblock = _serviceAccount.GetLastServiceBlock();
            //decimal real_price = Math.Round(block.Price / (decimal)Math.Pow(10, sell_token_precision), sell_token_precision);
            //decimal real_trade_amount = Math.Round(block.TradeAmount / (decimal)Math.Pow(10, buy_token_precision), buy_token_precision);
            //long sell_amount = (long) (real_price * real_trade_amount * (decimal)Math.Pow(10, sell_token_precision));
            decimal balance_change = block.TradeAmount * block.Price;

            decimal reference_fee = serviceblock.TradeFee;

            if (block.CoverAnotherTradersFee)
                reference_fee = serviceblock.TradeFee * 2;

            if (block.AnotherTraderWillCoverFee)
                reference_fee = 0;

            if (block.SellTokenCode == TokenGenesisBlock.LYRA_TICKER_CODE)
                balance_change += reference_fee;

            if (transaction.TotalBalanceChange != balance_change)
                return false;

            return true;
        }

        private int FindTokenPrecision(string token)
        {
            int precision = -1;

            // see if we have this already in local storage
            var genesisBlock = _accountCollection.FindTokenGenesisBlock(null, token);

            if (genesisBlock != null)
                precision = (int)genesisBlock.Precision;

            return precision;
        }
    }
}
