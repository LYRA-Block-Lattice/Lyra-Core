using System;
using System.Collections.Generic;
using Lyra.Core.Blocks;
using Lyra.Core.API;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using Lyra.Core.Utils;
using Lyra.Core.Accounts;
using Lyra.Data.Blocks;

namespace Lyra.Core.Authorizers
{
    public class ReceiveNodeProfitAuthorizer : ProfitingAuthorizer
    {
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is ReceiveNodeProfitBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as ReceiveNodeProfitBlock;

            var unSetFees = await sys.Storage.FindUnsettledFeesAsync(block.OwnerAccountId, block.AccountID);
            if (unSetFees == null)
                return APIResultCodes.InvalidSyncFeeBlock;

            var lastSb = await sys.Storage.GetLastServiceBlockAsync();
            var feesEndSb = await sys.Storage.FindServiceBlockByIndexAsync(unSetFees.ServiceBlockEndHeight);

            TransactionBlock lastBlock = await sys.Storage.FindLatestBlockAsync(block.AccountID) as TransactionBlock;

            var oldBalance = lastBlock.Balances.ContainsKey(LyraGlobal.OFFICIALTICKERCODE) ?
                lastBlock.Balances[LyraGlobal.OFFICIALTICKERCODE] : 0;
            if (
                block.ServiceHash == lastSb.Hash &&
                //block.SourceHash == feesEndSb.Hash &&
                block.ServiceBlockStartHeight == unSetFees.ServiceBlockStartHeight &&
                block.ServiceBlockEndHeight == unSetFees.ServiceBlockEndHeight &&
                block.Fee == 0 &&
                block.FeeType == AuthorizationFeeTypes.NoFee &&
                block.Balances[LyraGlobal.OFFICIALTICKERCODE] == oldBalance + unSetFees.TotalFees.ToBalanceLong()
                )
            {
                return await MeasureAuthAsync(base.GetType().Name, base.AuthorizeImplAsync(sys, tblock));
            }
            else
            {
                return APIResultCodes.InvalidSyncFeeBlock;
            }
        }

        //protected async Task<APIResultCodes> ValidateReceiveTransAmountAsync(DagSystem sys, ReceiveTransferBlock block, TransactionInfo receiveTransaction)
        //{
        //    //find the corresponding send block and validate the added transaction amount
        //    var sourceBlock = await sys.Storage.FindBlockByHashAsync(block.SourceHash) as TransactionBlock;
        //    if (sourceBlock == null)
        //        return APIResultCodes.SourceSendBlockNotFound;


        //    // find the actual amount of transaction 
        //    TransactionInfo sendTransaction;
        //    if (block.BlockType == BlockTypes.ReceiveTransfer || block.BlockType == BlockTypes.OpenAccountWithReceiveTransfer)
        //    {
        //        if ((sourceBlock as SendTransferBlock).DestinationAccountId != block.AccountID)
        //            return APIResultCodes.InvalidDestinationAccountId;

        //        TransactionBlock prevToSendBlock = await sys.Storage.FindBlockByHashAsync(sourceBlock.PreviousHash) as TransactionBlock;
        //        if (prevToSendBlock == null)
        //            return APIResultCodes.CouldNotTraceSendBlockChain;

        //        sendTransaction = sourceBlock.GetTransaction(prevToSendBlock);

        //        if (!sourceBlock.ValidateTransaction(prevToSendBlock))
        //            return APIResultCodes.SendTransactionValidationFailed;
        //        //originallySentAmount = sendTransaction.Amount;
        //        //originallySentAmount = 
        //        //    prevToSendBlock.Balances[LyraGlobal.LYRA_TICKER_CODE] - sourceBlock.Balances[LyraGlobal.LYRA_TICKER_CODE] - (sourceBlock as IFeebleBlock).Fee;
        //    }
        //    else
        //    if (block.BlockType == BlockTypes.ReceiveFee || block.BlockType == BlockTypes.OpenAccountWithReceiveFee)
        //    {
        //        sendTransaction = new TransactionInfo() { TokenCode = LyraGlobal.OFFICIALTICKERCODE, Amount = sourceBlock.Fee };
        //    }
        //    else
        //        return APIResultCodes.InvalidBlockType;

        //    if (sendTransaction.Amount != receiveTransaction.Amount)
        //        return APIResultCodes.TransactionAmountDoesNotMatch;

        //    if (sendTransaction.TokenCode != receiveTransaction.TokenCode)
        //        return APIResultCodes.TransactionTokenDoesNotMatch;

        //    return APIResultCodes.Success;
        //}

    }
}
