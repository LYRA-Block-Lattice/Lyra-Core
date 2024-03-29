﻿using System;
using System.Collections.Generic;
using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using Lyra.Core.API;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Lyra.Core.Utils;
using Lyra.Core.Accounts;


namespace Lyra.Core.Authorizers
{
    /*
    public class TradeAuthorizer : TransactionAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.Trade;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is TradeBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as TradeBlock;

            // 1. check if the account already exists
            if (!await sys.Storage.AccountExistsAsync(block.AccountID))
                return APIResultCodes.AccountDoesNotExist;

            if (await sys.Storage.WasAccountImportedAsync(block.AccountID))
                return APIResultCodes.CannotModifyImportedAccount;

            var lastBlock = await sys.Storage.FindLatestBlockAsync(block.AccountID) as TransactionBlock;
            if (lastBlock == null)
                return APIResultCodes.CouldNotFindLatestBlock;

            //if (lastBlock.Balances[TokenGenesisBlock.POINTGEAR_TICKER_CODE] <= block.Balances[TokenGenesisBlock.POINTGEAR_TICKER_CODE] + block.Fee)
            //    return AuthorizationResultCodes.NegativeTransactionAmount;

            // Validate the destination account id
            if (!Signatures.ValidateAccountId(block.DestinationAccountId))
                return APIResultCodes.InvalidDestinationAccountId;

            var result = await VerifyTransactionBlockAsync(sys, block);
            if (result != APIResultCodes.Success)
                return result;

            if (!block.ValidateTransaction(lastBlock))
                return APIResultCodes.SendTransactionValidationFailed;

            // TP DO validate the original order
            var original_order = await sys.Storage.FindBlockByHashAsync(block.DestinationAccountId, block.TradeOrderId) as TradeOrderBlock;
            if (original_order == null)
                return APIResultCodes.TradeOrderValidationFailed;

            if (original_order.SellTokenCode != block.BuyTokenCode)
                return APIResultCodes.TradeOrderValidationFailed;

            if (original_order.BuyTokenCode != block.SellTokenCode)
                return APIResultCodes.TradeOrderValidationFailed;

            // TO DO validate amounts

            //// Deactivate the order if it allows only one matching trade
            //if (original_order.MaxQuantity == 1)
            //    sys.TradeEngine.RemoveOrder(original_order);

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "TradeAuthorizer->TransactionAuthorizer");
        }

        protected override async Task<APIResultCodes> ValidateFeeAsync(DagSystem sys, TransactionBlock block)
        {
            var order = await sys.Storage.FindBlockByHashAsync((block as TradeBlock).TradeOrderId) as TradeOrderBlock;
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
                if (block.Fee != (await sys.Storage.GetLastServiceBlockAsync()).TradeFee * 2)
                    return APIResultCodes.InvalidFeeAmount;
                return APIResultCodes.Success;
            }
            else
            {
                // regular fee
                if (block.FeeType != AuthorizationFeeTypes.Regular)
                    return APIResultCodes.InvalidFeeType;

                if (block.Fee != (await sys.Storage.GetLastServiceBlockAsync()).TradeFee)
                    return APIResultCodes.InvalidFeeAmount;

                return APIResultCodes.Success;
            }
        }
    }*/
}
