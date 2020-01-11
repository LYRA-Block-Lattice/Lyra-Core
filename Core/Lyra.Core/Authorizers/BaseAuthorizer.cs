using Lyra.Core.Blocks;
using Lyra.Core.Cryptography;
using Lyra.Core.API;
using System;
using Lyra.Core.Utils;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Lyra.Core.Decentralize;
using Microsoft.Extensions.Logging;

namespace Lyra.Core.Authorizers
{
    public delegate void AuthorizeCompleteEventHandler(object sender, AuthorizeCompletedEventArgs e);

    public class AuthorizeCompletedEventArgs : EventArgs
    {
        public Block Result { get; }
        public AuthorizeCompletedEventArgs(Block block)
        {
            Result = block;
        }
    }

    public abstract class BaseAuthorizer : IAuthorizer
    {
        ILogger _log;
        public BaseAuthorizer()
        {
            _log = new SimpleLogger("BaseAuthorizer").Logger;
        }

        public virtual (APIResultCodes, AuthorizationSignature) Authorize<T>(T tblock, bool WithSign = true)
        {
            throw new NotImplementedException("Must override");
        }

        public virtual APIResultCodes Commit<T>(T tblock)
        {
            throw new NotImplementedException("Must override");
        }

        protected APIResultCodes VerifyBlock(TransactionBlock block, TransactionBlock previousBlock)
        {
            if (LyraSystem.Singleton.NetworkId != block.NetworkId)
                return APIResultCodes.InvalidNetworkId;

            if (previousBlock != null && !block.IsBlockValid(previousBlock))
                return APIResultCodes.BlockValidationFailed;

            //if (!Signatures.VerifySignature(block.Hash, block.AccountID, block.Signature))
            //    return APIResultCodes.BlockSignatureValidationFailed;

            if(block is ServiceBlock)
            {
                var accountId = (block as ServiceBlock).SvcAccountID;
                var result = block.VerifySignature(accountId);
                if (!result)
                {
                    _log.LogWarning($"VerifyBlock failed for ServiceBlock UIndex: {block.UIndex} by {accountId}");
                    return APIResultCodes.BlockSignatureValidationFailed;
                }                    
            }
            else
            {
                if(!block.VerifyHash())
                    _log.LogWarning($"VerifyBlock VerifyHash failed for TransactionBlock UIndex: {block.UIndex} by {block.GetHashInput()}");

                var result = block.VerifySignature(block.AccountID);
                if (!result)
                {
                    _log.LogWarning($"VerifyBlock failed for TransactionBlock UIndex: {block.UIndex} Type: {block.BlockType} by {block.AccountID}");
                    return APIResultCodes.BlockSignatureValidationFailed;
                }

                // check if this Index already exists (double-spending, kind of)
                if (block.BlockType != BlockTypes.NullTransaction && BlockChain.Singleton.FindBlockByIndex(block.AccountID, block.Index) != null)
                    return APIResultCodes.BlockWithThisIndexAlreadyExists;
            }         

            // This is the double-spending check for send block!
            if (!string.IsNullOrEmpty(block.PreviousHash) && BlockChain.Singleton.FindBlockByPreviousBlockHash(block.PreviousHash) != null)
                return APIResultCodes.BlockWithThisPreviousHashAlreadyExists;

            if (block.Index <= 0)
                return APIResultCodes.InvalidIndexSequence;

            if (block.Index > 1 && previousBlock == null)
                return APIResultCodes.CouldNotFindLatestBlock;

            if (block.Index == 1 && previousBlock != null)
                return APIResultCodes.InvalidIndexSequence;

            if (previousBlock != null && block.Index != previousBlock.Index + 1)
                return APIResultCodes.InvalidIndexSequence;

            if(!(block is ConsolidationBlock) && !(block is NullTransactionBlock))
            {
                if (!ValidateRenewalDate(block, previousBlock))
                    return APIResultCodes.TokenExpired;
            }

            return APIResultCodes.Success;
        }

        protected bool ValidateRenewalDate(TransactionBlock block, TransactionBlock previousBlock)
        {
            if (previousBlock == null)
                return true;

            var trs = block.GetTransaction(previousBlock);

            if (trs.Amount <= 0)
                return true;

            var token = BlockChain.Singleton.FindTokenGenesisBlock(null, trs.TokenCode);
            if (token != null)
                if (token.RenewalDate < DateTime.Now)
                    return false;

            return true;
        }

        // common validations for Send and Receive blocks
        protected APIResultCodes VerifyTransactionBlock(TransactionBlock block)
        {
            // Validate the account id
            if (!Signatures.ValidateAccountId(block.AccountID))
                return APIResultCodes.InvalidAccountId;

            if (!string.IsNullOrEmpty(block.PreviousHash)) // not for new account
            {
                // verify the entire account chain to make sure all account's blocks are valid
                TransactionBlock prevBlock, thisBlock = block;
                //while (thisBlock.BlockType != BlockTypes.OpenWithReceiveTransfer && thisBlock.BlockType != BlockTypes.OpenWithReceiveFee)
                while (!(thisBlock is IOpeningBlock))
                {
                    prevBlock = BlockChain.Singleton.FindBlockByHash(thisBlock.PreviousHash);
                    if (!thisBlock.IsBlockValid(prevBlock))
                        return APIResultCodes.AccountChainBlockValidationFailed;

                    var result = Signatures.VerifyAccountSignature(thisBlock.Hash, thisBlock.AccountID, thisBlock.Signature);
                    if (!result)
                        return APIResultCodes.AccountChainSignatureValidationFailed;

                    thisBlock = prevBlock;
                }

                // verify the spending
                TransactionBlock previousTransaction = BlockChain.Singleton.FindBlockByHash(block.PreviousHash);
                foreach (var prevbalance in previousTransaction.Balances)
                {
                    // make sure all balances from the previous block are present in a new block even if they are unchanged
                    if (!block.Balances.ContainsKey(prevbalance.Key))
                        return APIResultCodes.AccountChainBalanceValidationFailed;
                }

                // Verify fee
                if (block.BlockType == BlockTypes.SendTransfer)
                    if ((block as SendTransferBlock).Fee != BlockChain.Singleton.GetLastServiceBlock().TransferFee)
                        return APIResultCodes.InvalidFeeAmount;

                if (block.BlockType == BlockTypes.TokenGenesis)
                    if ((block as TokenGenesisBlock).Fee != BlockChain.Singleton.GetLastServiceBlock().TokenGenerationFee)
                        return APIResultCodes.InvalidFeeAmount;
            }

            var res = ValidateFee(block);
            if (res != APIResultCodes.Success)
                return res;

            return APIResultCodes.Success;
        }

        protected abstract APIResultCodes ValidateFee(TransactionBlock block);

        //protected virtual APIResultCodes ValidateFee(TransactionBlock block)
        //{
        //    if (block.Fee == 0 && block.FeeType != AuthorizationFeeTypes.NoFee)
        //        return APIResultCodes.InvalidFeeAmount;

        //    return APIResultCodes.Success;
        //}

        protected virtual APIResultCodes ValidateNonFungible(TransactionBlock send_or_receice_block, TransactionBlock previousBlock)
        {
            TransactionInfoEx transaction = send_or_receice_block.GetTransaction(previousBlock);

            if (transaction.TokenCode == LyraGlobal.LYRATICKERCODE)
                return APIResultCodes.Success;

            var token_block = BlockChain.Singleton.FindTokenGenesisBlock(null, transaction.TokenCode);
            if (token_block == null)
                return APIResultCodes.TokenGenesisBlockNotFound;

            if (!token_block.IsNonFungible)
                return APIResultCodes.Success;

            //INonFungibleToken non_fungible_token = send_block.GetNonFungibleTransaction(previousBlock);

            if (send_or_receice_block.NonFungibleToken == null)
                return APIResultCodes.MissingNonFungibleToken;

            if (send_or_receice_block.NonFungibleToken.Denomination != transaction.Amount)
                return APIResultCodes.InvalidNonFungibleAmount;

            if (send_or_receice_block.NonFungibleToken.TokenCode != transaction.TokenCode)
                return APIResultCodes.InvalidNonFungibleTokenCode;

            var vr = send_or_receice_block.NonFungibleToken.VerifySignature(token_block.NonFungibleKey);
            if (!vr)
                return APIResultCodes.NonFungibleSignatureVerificationFailed;

            return APIResultCodes.Success;
        }

        protected AuthorizationSignature Sign<T>(T tblock)
        {
            if (!(tblock is TransactionBlock))
                throw new System.ApplicationException("APIResultCodes.InvalidBlockType");

            var block = tblock as TransactionBlock;

            if (block.BlockType != BlockTypes.Consolidation)
            {
                // ServiceHash is excluded when calculating the block hash,
                // but it is included when creating/validating the authorization signature
                block.ServiceHash = BlockChain.Singleton.GetSyncBlock().Hash;
            }

            // sign with the authorizer key
            AuthorizationSignature authSignature = new AuthorizationSignature
            {
                Key = NodeService.Instance.PosWallet.AccountId,
                Signature = Signatures.GetSignature(NodeService.Instance.PosWallet.PrivateKey, block.Hash + block.ServiceHash, NodeService.Instance.PosWallet.AccountId)
            };

            return authSignature;
        }

        //protected async Task<bool> VerifyAuthorizationSignaturesAsync(TransactionBlock block)
        //{
        //    //block.ServiceHash = BlockChain.Singleton.ServiceAccount.GetLatestBlock(block.ServiceHash);

        //    // TO DO - support multy nodes
        //    if (block.Authorizations == null || block.Authorizations.Count != 1)
        //        return false;

        //    if (block.Authorizations[0].Key != BlockChain.Singleton.ServiceAccount.AccountId)
        //        return false;

        //    return Signatures.VerifyAuthorizerSignature(block.Hash + block.ServiceHash, block.Authorizations[0].Key, block.Authorizations[0].Signature);

        //}
    }
}
