using Lyra.Core.Blocks;
using Lyra.Core.Cryptography;
using Lyra.Core.API;
using System;
using Lyra.Core.Utils;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Lyra.Core.Decentralize;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Linq;
using Neo;
using Akka.Actor;
using static Lyra.Core.Decentralize.ConsensusService;

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

        public virtual Task<(APIResultCodes, AuthorizationSignature)> AuthorizeAsync<T>(DagSystem sys, T tblock)
        {
            throw new NotImplementedException("Must override");
        }

        //public virtual APIResultCodes Commit<T>(T tblock)
        //{
        //    throw new NotImplementedException("Must override");
        //}

        protected async Task<APIResultCodes> VerifyBlockAsync(DagSystem sys, Block block, Block previousBlock)
        {
            if (previousBlock != null && !block.IsBlockValid(previousBlock))
                return APIResultCodes.BlockValidationFailed;

            //if (!Signatures.VerifySignature(block.Hash, block.AccountID, block.Signature))
            //    return APIResultCodes.BlockSignatureValidationFailed;

            if(block is ServiceBlock bsb)
            {
                var board = await sys.Consensus.Ask<BillBoard>(new AskForBillboard());
                if (board.CurrentLeader != bsb.Leader)
                    return APIResultCodes.InvalidLeaderInServiceBlock;

                var result = block.VerifySignature(board.CurrentLeader);
                if (!result)
                {
                    _log.LogWarning($"VerifySignature failed for ServiceBlock Index: {block.Height} with Leader {board.CurrentLeader}");
                    return APIResultCodes.BlockSignatureValidationFailed;
                }
            }
            else if(block is TransactionBlock)
            {
                var blockt = block as TransactionBlock;

                if (!blockt.VerifyHash())
                    _log.LogWarning($"VerifyBlock VerifyHash failed for TransactionBlock Index: {block.Height} by {block.GetHashInput()}");

                var result = block.VerifySignature(blockt.AccountID);
                if (!result)
                {
                    _log.LogWarning($"VerifyBlock failed for TransactionBlock Index: {block.Height} Type: {block.BlockType} by {blockt.AccountID}");
                    return APIResultCodes.BlockSignatureValidationFailed;
                }

                // check if this Index already exists (double-spending, kind of)
                if (block.BlockType != BlockTypes.NullTransaction && await (sys.Storage.FindBlockByIndexAsync(blockt.AccountID, block.Height)) != null)
                    return APIResultCodes.BlockWithThisIndexAlreadyExists;

                // check service hash
                if (string.IsNullOrWhiteSpace(blockt.ServiceHash))
                    return APIResultCodes.ServiceBlockNotFound;

                var svcBlock = await sys.Storage.GetLastServiceBlockAsync();
                if (blockt.ServiceHash != svcBlock.Hash)
                    return APIResultCodes.ServiceBlockNotFound;

                if (!await ValidateRenewalDateAsync(sys, blockt, previousBlock as TransactionBlock))
                    return APIResultCodes.TokenExpired;
            }         

            // This is the double-spending check for send block!
            if (!string.IsNullOrEmpty(block.PreviousHash) && (await sys.Storage.FindBlockByPreviousBlockHashAsync(block.PreviousHash)) != null)
                return APIResultCodes.BlockWithThisPreviousHashAlreadyExists;

            if (block.Height <= 0)
                return APIResultCodes.InvalidIndexSequence;

            if (block.Height > 1 && previousBlock == null)       // bypass genesis block
                return APIResultCodes.PreviousBlockNotFound;

            if (block.Height == 1 && previousBlock != null)
                return APIResultCodes.InvalidIndexSequence;

            if (previousBlock != null && block.Height != previousBlock.Height + 1)
                return APIResultCodes.InvalidIndexSequence;

            return APIResultCodes.Success;
        }

        protected async Task<bool> ValidateRenewalDateAsync(DagSystem sys, TransactionBlock block, TransactionBlock previousBlock)
        {
            if (previousBlock == null)
                return true;

            var trs = block.GetTransaction(previousBlock);

            if (trs.Amount <= 0)
                return true;

            var token = await sys.Storage.FindTokenGenesisBlockAsync(null, trs.TokenCode);
            if (token != null)
                if (token.RenewalDate < DateTime.Now)
                    return false;

            return true;
        }

        // common validations for Send and Receive blocks
        protected async Task<APIResultCodes> VerifyTransactionBlockAsync(DagSystem sys, TransactionBlock block)
        {
            // Validate the account id
            if (!Signatures.ValidateAccountId(block.AccountID))
                return APIResultCodes.InvalidAccountId;

            if (!string.IsNullOrEmpty(block.PreviousHash)) // not for new account
            {
                // verify the entire account chain to make sure all account's blocks are valid
                TransactionBlock prevBlock, thisBlock = block;
                //while (thisBlock.BlockType != BlockTypes.OpenWithReceiveTransfer && thisBlock.BlockType != BlockTypes.OpenWithReceiveFee)
                //while (!(thisBlock is IOpeningBlock))
                if (!(thisBlock is IOpeningBlock))      //save time
                {
                    prevBlock = await sys.Storage.FindBlockByHashAsync(thisBlock.PreviousHash) as TransactionBlock;
                    if (!thisBlock.IsBlockValid(prevBlock))
                        return APIResultCodes.AccountChainBlockValidationFailed;

                    var result = Signatures.VerifyAccountSignature(thisBlock.Hash, thisBlock.AccountID, thisBlock.Signature);
                    if (!result)
                        return APIResultCodes.AccountChainSignatureValidationFailed;

                    thisBlock = prevBlock;
                }

                // verify the spending
                TransactionBlock previousTransaction = await sys.Storage.FindBlockByHashAsync(block.PreviousHash) as TransactionBlock;
                foreach (var prevbalance in previousTransaction.Balances)
                {
                    // make sure all balances from the previous block are present in a new block even if they are unchanged
                    if (!block.Balances.ContainsKey(prevbalance.Key))
                        return APIResultCodes.AccountChainBalanceValidationFailed;
                }

                // TODO: fee aggregation
                //// Verify fee
                //if (block.BlockType == BlockTypes.SendTransfer)
                //    if ((block as SendTransferBlock).Fee != await sys.Storage.GetLastServiceBlock().TransferFee)
                //        return APIResultCodes.InvalidFeeAmount;

                //if (block.BlockType == BlockTypes.TokenGenesis)
                //    if ((block as TokenGenesisBlock).Fee != await sys.Storage.GetLastServiceBlock().TokenGenerationFee)
                //        return APIResultCodes.InvalidFeeAmount;
            }

            var res = await ValidateFeeAsync(sys, block);
            if (res != APIResultCodes.Success)
                return res;

            return APIResultCodes.Success;
        }

        //protected abstract Task<APIResultCodes> ValidateFeeAsync(TransactionBlock block);

        protected virtual Task<APIResultCodes> ValidateFeeAsync(DagSystem sys, TransactionBlock block)
        {
            APIResultCodes result;
            if (block.FeeType != AuthorizationFeeTypes.NoFee)
                result = APIResultCodes.InvalidFeeAmount;

            if (block.Fee != 0)
                result = APIResultCodes.InvalidFeeAmount;

            result = APIResultCodes.Success;

            return Task.FromResult(result);
        }

        protected virtual async Task<APIResultCodes> ValidateNonFungibleAsync(DagSystem sys, TransactionBlock send_or_receice_block, TransactionBlock previousBlock)
        {
            TransactionInfoEx transaction = send_or_receice_block.GetTransaction(previousBlock);

            if (transaction.TokenCode == LyraGlobal.OFFICIALTICKERCODE)
                return APIResultCodes.Success;

            var token_block = await sys.Storage.FindTokenGenesisBlockAsync(null, transaction.TokenCode);
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

        protected AuthorizationSignature Sign<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is Block))
                throw new System.ApplicationException("APIResultCodes.InvalidBlockType");

            var block = tblock as Block;

            //if (block is TransactionBlock)
            //{
            //    // ServiceHash is excluded when calculating the block hash,
            //    // but it is included when creating/validating the authorization signature
            //    (block as TransactionBlock).ServiceHash = (await sys.Storage.GetSyncBlockAsync()).Hash;
            //}

            // sign with the authorizer key
            AuthorizationSignature authSignature = new AuthorizationSignature
            {
                Key = sys.PosWallet.AccountId,
                Signature = Signatures.GetSignature(sys.PosWallet.PrivateKey,
                    block.Hash, sys.PosWallet.AccountId)
            };

            return authSignature;
        }

        //protected async Task<bool> VerifyAuthorizationSignaturesAsync(TransactionBlock block)
        //{
        //    //block.ServiceHash = await sys.Storage.ServiceAccount.GetLatestBlock(block.ServiceHash);

        //    // TO DO - support multy nodes
        //    if (block.Authorizations == null || block.Authorizations.Count != 1)
        //        return false;

        //    if (block.Authorizations[0].Key != await sys.Storage.ServiceAccount.AccountId)
        //        return false;

        //    return Signatures.VerifyAuthorizerSignature(block.Hash + block.ServiceHash, block.Authorizations[0].Key, block.Authorizations[0].Signature);

        //}
    }
}
