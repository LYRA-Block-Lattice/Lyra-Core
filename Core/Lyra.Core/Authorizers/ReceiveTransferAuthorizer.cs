using System;
using System.Collections.Generic;
using Lyra.Core.Blocks;
using Lyra.Core.API;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using Lyra.Core.Utils;
using Lyra.Core.Accounts;
using System.Linq;
using Lyra.Data.Blocks;

namespace Lyra.Core.Authorizers
{
    public class ReceiveTransferAuthorizer: TransactionAuthorizer
    {
        static List<BlockTypes> AllowedOpeningBlockTypes = new List<BlockTypes> { 
            BlockTypes.ProfitingGenesis,
            BlockTypes.StakingGenesis,
            BlockTypes.PoolGenesis,
            BlockTypes.OpenAccountWithReceiveTransfer,
            BlockTypes.LyraTokenGenesis,
            BlockTypes.DexWalletGenesis
        };
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is ReceiveTransferBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as ReceiveTransferBlock;

            if (block.AccountID.Equals(LyraGlobal.BURNINGACCOUNTID))
                return APIResultCodes.InvalidAccountId;

            // 1. check if the account already exists
            //if(block is ProfitingGenesis || block is StakingGenesis || block is PoolGenesisBlock)
            //{
            //    return APIResultCodes.Success;
            //}

            if(block is IOpeningBlock)
            {
                if (block.Height != 1)
                    return APIResultCodes.InvalidOpeningAccount;

                if(!AllowedOpeningBlockTypes.Any(a => a == block.BlockType))
                    return APIResultCodes.InvalidOpeningAccount;
            }
            else
            {
                if (!await sys.Storage.AccountExistsAsync(block.AccountID))
                    return APIResultCodes.AccountDoesNotExist;

                TransactionBlock lastBlock = await sys.Storage.FindLatestBlockAsync(block.AccountID) as TransactionBlock;
                if (lastBlock == null)
                    return APIResultCodes.CouldNotFindLatestBlock;

                var result = await VerifyTransactionBlockAsync(sys, block);
                if (result != APIResultCodes.Success)
                    return result;

                if (!block.ValidateTransaction(lastBlock))
                    return APIResultCodes.ReceiveTransactionValidationFailed;

                result = await ValidateReceiveTransAmountAsync(sys, block, block.GetBalanceChanges(lastBlock));
                if (result != APIResultCodes.Success)
                    return result;

                if(block.BlockType != BlockTypes.TokenGenesis)
                {
                    result = await ValidateNonFungibleAsync(sys, block, lastBlock);
                    if (result != APIResultCodes.Success)
                        return result;
                }
            }

            if (await sys.Storage.WasAccountImportedAsync(block.AccountID))
                return APIResultCodes.CannotModifyImportedAccount;

            // Check duplicate receives (kind of double spending up down)
            var duplicate_block = await sys.Storage.FindBlockBySourceHashAsync(block.SourceHash);
            if (duplicate_block != null)
                return APIResultCodes.DuplicateReceiveBlock;

            return await MeasureAuthAsync(this.GetType().Name, base.GetType().Name, base.AuthorizeImplAsync(sys, tblock));
        }

        protected override bool IsManagedBlockAllowed(DagSystem sys, TransactionBlock block)
        {
            if (block.AccountID == PoolFactoryBlock.FactoryAccount
                || block is IPool
                || block is IProfiting)
                return true;

            return base.IsManagedBlockAllowed(sys, block);
        }

        protected override AuthorizationFeeTypes GetFeeType()
        {
            return AuthorizationFeeTypes.NoFee;
        }

        protected virtual async Task<APIResultCodes> ValidateReceiveTransAmountAsync(DagSystem sys, ReceiveTransferBlock block, BalanceChanges receiveTransaction)
        {
            //find the corresponding send block and validate the added transaction amount
            var srcBlock = await sys.Storage.FindBlockByHashAsync(block.SourceHash);
            if (srcBlock is TransactionBlock sourceBlock)
            {
                if (sourceBlock == null)
                    return APIResultCodes.SourceSendBlockNotFound;

                // find the actual amount of transaction 
                BalanceChanges sendTransaction;
                if (block.BlockType == BlockTypes.ReceiveTransfer || block.BlockType == BlockTypes.OpenAccountWithReceiveTransfer
                    || block.BlockType == BlockTypes.PoolDeposit || block.BlockType == BlockTypes.PoolSwapIn
                    || block.BlockType == BlockTypes.Staking || block.BlockType == BlockTypes.Profiting
                    || block.BlockType == BlockTypes.ReceiveAsFee
                    || block.BlockType == BlockTypes.DexRecvToken
                    )  // temp code. should use getbalancechanges
                {
                    if ((sourceBlock as SendTransferBlock).DestinationAccountId != block.AccountID)
                    {
                        // first check if the transfer was aimed to imported account
                        if (!await sys.Storage.WasAccountImportedAsync((sourceBlock as SendTransferBlock).DestinationAccountId, block.AccountID))
                            return APIResultCodes.InvalidDestinationAccountId;
                    }

                    TransactionBlock prevToSendBlock = await sys.Storage.FindBlockByHashAsync(sourceBlock.PreviousHash) as TransactionBlock;
                    if (prevToSendBlock == null)
                        return APIResultCodes.CouldNotTraceSendBlockChain;

                    sendTransaction = sourceBlock.GetBalanceChanges(prevToSendBlock);

                    if (!sourceBlock.ValidateTransaction(prevToSendBlock))
                        return APIResultCodes.SendTransactionValidationFailed;
                    //originallySentAmount = sendTransaction.Amount;
                    //originallySentAmount = 
                    //    prevToSendBlock.Balances[LyraGlobal.LYRA_TICKER_CODE] - sourceBlock.Balances[LyraGlobal.LYRA_TICKER_CODE] - (sourceBlock as IFeebleBlock).Fee;
                }
                else
                if (block.BlockType == BlockTypes.ReceiveFee || block.BlockType == BlockTypes.OpenAccountWithReceiveFee)
                {
                    sendTransaction = new BalanceChanges();
                    sendTransaction.Changes.Add(LyraGlobal.OFFICIALTICKERCODE, sourceBlock.Fee);
                }
                else
                    return APIResultCodes.InvalidBlockType;

                if(block.BlockType == BlockTypes.ReceiveAsFee)
                {
                    var send = sendTransaction.Changes[LyraGlobal.OFFICIALTICKERCODE];
                    var recv = receiveTransaction.Changes.Count == 0 ? 0 : receiveTransaction.Changes[LyraGlobal.OFFICIALTICKERCODE];
                    var fee = block.Fee;
                    if (fee != send || recv != 0)
                        return APIResultCodes.InvalidFeeAmount;
                }
                else
                {
                    if (!sendTransaction.Changes.OrderBy(kvp => kvp.Key)
                            .SequenceEqual(receiveTransaction.Changes.OrderBy(kvp => kvp.Key)))
                        return APIResultCodes.TransactionAmountDoesNotMatch;
                }

                //if (sendTransaction.Amount != receiveTransaction.Amount)
                //    return APIResultCodes.TransactionAmountDoesNotMatch;

                //if (sendTransaction.TokenCode != receiveTransaction.TokenCode)
                //    return APIResultCodes.TransactionTokenDoesNotMatch;
            }
            else if (srcBlock == null)
            {
                // TODO: verify NodeFeeBlock
                if (block is ReceiveNodeProfitBlock)
                    return APIResultCodes.Success;
            }
            else
            {
                return APIResultCodes.UnsupportedBlockType;
            }

            return APIResultCodes.Success;
        }

        protected override async Task<APIResultCodes> ValidateNonFungibleAsync(DagSystem sys, TransactionBlock send_or_receice_block, TransactionBlock previousBlock)
        {
            var result = await base.ValidateNonFungibleAsync(sys, send_or_receice_block, previousBlock);
            if (result != APIResultCodes.Success)
                return result;

            if (send_or_receice_block.NonFungibleToken == null)
                return APIResultCodes.Success;

            var originBlock = await sys.Storage.FindBlockByHashAsync((send_or_receice_block as ReceiveTransferBlock).SourceHash) as TransactionBlock;
            if (originBlock == null)
                return APIResultCodes.OriginNonFungibleBlockNotFound;

            if (!originBlock.ContainsNonFungibleToken())
                return APIResultCodes.OriginNonFungibleBlockNotFound;

            // this validation eliminates the need to make all the validations that already have been done on send block
            if (originBlock.NonFungibleToken.Hash != send_or_receice_block.NonFungibleToken.Hash)
                return APIResultCodes.OriginNonFungibleBlockHashDoesNotMatch;

            // this validation eliminates the need to make all the validations that already have been done on send block
            if (originBlock.NonFungibleToken.Signature != send_or_receice_block.NonFungibleToken.Signature)
                return APIResultCodes.NFTSignaturesDontMatch;

            return APIResultCodes.Success;
        }
    }
}
