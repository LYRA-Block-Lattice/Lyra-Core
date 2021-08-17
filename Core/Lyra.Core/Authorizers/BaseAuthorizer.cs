using Lyra.Core.Blocks;
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
using Lyra.Shared;
using Lyra.Data.API;
using Lyra.Data.Crypto;
using Lyra.Data.Utils;

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

        protected virtual async Task<APIResultCodes> VerifyBlockAsync(DagSystem sys, Block block, Block previousBlock)
        {
            if (previousBlock != null && !block.IsBlockValid(previousBlock))
                return APIResultCodes.InvalidPreviousBlock;

            // allow time drift: form -5 to +3
            var uniNow = DateTime.UtcNow;
            if (block is ServiceBlock bsb)
            {
                var board = await sys.Consensus.Ask<BillBoard>(new AskForBillboard());
                if (board.LeaderCandidate != bsb.Leader)
                {
                    _log.LogWarning($"Invalid leader. was {bsb.Leader.Shorten()} should be {board.LeaderCandidate.Shorten()}");
                    return APIResultCodes.InvalidLeaderInServiceBlock;
                }

                var result = block.VerifySignature(board.LeaderCandidate);
                if (!result)
                {
                    _log.LogWarning($"VerifySignature failed for ServiceBlock Index: {block.Height} with Leader {board.CurrentLeader}");
                    return APIResultCodes.BlockSignatureValidationFailed;
                }

                if (sys.ConsensusState != BlockChainState.StaticSync)
                {
                    if (block.TimeStamp < uniNow.AddSeconds(-18) || block.TimeStamp > uniNow.AddSeconds(3))
                    {
                        _log.LogInformation($"TimeStamp 1: {block.TimeStamp} Universal Time Now: {uniNow}");
                        return APIResultCodes.InvalidBlockTimeStamp;
                    }
                }
            }
            else if (block is TransactionBlock)
            {
                var blockt = block as TransactionBlock;

                if (!blockt.VerifyHash())
                    _log.LogWarning($"VerifyBlock VerifyHash failed for TransactionBlock Index: {block.Height} by {block.GetHashInput()}");

                var verifyAgainst = blockt.AccountID;
                if (blockt.ContainsTag(Block.MANAGEDTAG))      // pool block is both service and transaction
                {
                    var board = await sys.Consensus.Ask<BillBoard>(new AskForBillboard());
                    verifyAgainst = board.CurrentLeader;
                }

                var result = block.VerifySignature(verifyAgainst);
                if (!result)
                {
                    _log.LogWarning($"VerifyBlock failed for TransactionBlock Index: {block.Height} Type: {block.BlockType} by {blockt.AccountID}");
                    return APIResultCodes.BlockSignatureValidationFailed;
                }

                // check if this Index already exists (double-spending, kind of)
                if (await sys.Storage.FindBlockByIndexAsync(blockt.AccountID, block.Height) != null)
                    return APIResultCodes.BlockWithThisIndexAlreadyExists;

                // check service hash
                if (string.IsNullOrWhiteSpace(blockt.ServiceHash))
                    return APIResultCodes.ServiceBlockNotFound;

                var svcBlock = await sys.Storage.GetLastServiceBlockAsync();
                if (blockt.ServiceHash != svcBlock.Hash)
                    return APIResultCodes.ServiceBlockNotFound;

                if (!await ValidateRenewalDateAsync(sys, blockt, previousBlock as TransactionBlock))
                    return APIResultCodes.TokenExpired;

                if (sys.ConsensusState != BlockChainState.StaticSync)
                {
                    if (block.TimeStamp < uniNow.AddSeconds(-18) || block.TimeStamp > uniNow.AddSeconds(3))
                    {
                        _log.LogInformation($"TimeStamp 2: {block.TimeStamp} Universal Time Now: {uniNow}");
                        return APIResultCodes.InvalidBlockTimeStamp;
                    }
                }
            }
            else if (block is ConsolidationBlock cons)
            {
                if (sys.ConsensusState != BlockChainState.StaticSync)
                {
                    // time shift 10 seconds.
                    if (block.TimeStamp < uniNow.AddSeconds(-60) || block.TimeStamp > uniNow.AddSeconds(3))
                    {
                        _log.LogInformation($"TimeStamp 3: {block.TimeStamp} Universal Time Now: {uniNow}");
                        return APIResultCodes.InvalidBlockTimeStamp;
                    }
                }

                var board = await sys.Consensus.Ask<BillBoard>(new AskForBillboard());
                if (board.CurrentLeader != cons.createdBy)
                {
                    _log.LogWarning($"Invalid leader. was {cons.createdBy.Shorten()} should be {board.CurrentLeader.Shorten()}");
                    return APIResultCodes.InvalidLeaderInConsolidationBlock;
                }

                var result = block.VerifySignature(board.CurrentLeader);
                if (!result)
                {
                    _log.LogWarning($"VerifySignature failed for ConsolidationBlock Index: {block.Height} with Leader {board.CurrentLeader}");
                    return APIResultCodes.BlockSignatureValidationFailed;
                }
            }
            else
            {
                return APIResultCodes.InvalidBlockType;
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

            var trs = block.GetBalanceChanges(previousBlock);

            foreach(var chg in trs.Changes)
            {
                var token = await sys.Storage.FindTokenGenesisBlockAsync(null, chg.Key);
                if (token != null && token.RenewalDate < DateTime.UtcNow)
                    return false;

                if (token == null && block is TokenGenesisBlock gen && gen.Ticker != chg.Key)
                    return false;
            }

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

                    if(block.ContainsTag(Block.MANAGEDTAG))
                    {
                        var svcBlock = await sys.Storage.FindBlockByHashAsync(block.ServiceHash) as ServiceBlock;
                        var result = Signatures.VerifyAccountSignature(thisBlock.Hash, svcBlock.Leader, thisBlock.Signature);
                        if (!result)
                            return APIResultCodes.AccountChainSignatureValidationFailed;
                    }
                    else
                    {
                        var result = Signatures.VerifyAccountSignature(thisBlock.Hash, thisBlock.AccountID, thisBlock.Signature);
                        if (!result)
                            return APIResultCodes.AccountChainSignatureValidationFailed;
                    }

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
            var transaction = send_or_receice_block.GetBalanceChanges(previousBlock);

            if (transaction.Changes.Count == 1 && transaction.Changes.First().Key == LyraGlobal.OFFICIALTICKERCODE)
                return APIResultCodes.Success;

            foreach(var chg in transaction.Changes)
            {
                var tokenCode = chg.Key;
                var tokenAmount = chg.Value;

                var token_block = await sys.Storage.FindTokenGenesisBlockAsync(null, tokenCode);
                if (token_block == null)
                    return APIResultCodes.TokenGenesisBlockNotFound;

                if (!token_block.IsNonFungible)
                    return APIResultCodes.Success;

                //INonFungibleToken non_fungible_token = send_block.GetNonFungibleTransaction(previousBlock);

                if (send_or_receice_block.NonFungibleToken == null)
                    return APIResultCodes.MissingNonFungibleToken;

                if (send_or_receice_block.NonFungibleToken.Denomination != tokenAmount)
                    return APIResultCodes.InvalidNonFungibleAmount;

                if (send_or_receice_block.NonFungibleToken.TokenCode != tokenCode)
                    return APIResultCodes.InvalidNonFungibleTokenCode;

                var vr = send_or_receice_block.NonFungibleToken.VerifySignature(token_block.NonFungibleKey);
                if (!vr)
                    return APIResultCodes.NonFungibleSignatureVerificationFailed;

                if (token_block.ContractType == ContractTypes.Collectible)
                {
                    var res = await ValidateCollectibleNFTAsync(sys, send_or_receice_block, token_block);
                    if (res != APIResultCodes.Success)
                        return res;
                }
            }

            return APIResultCodes.Success;
        }

        // must be overriden and implemented in both send and receive authorizers as they have very different logic
        protected virtual async Task<APIResultCodes> ValidateCollectibleNFTAsync(DagSystem sys, TransactionBlock send_or_receice_block, TokenGenesisBlock token_block)
        {
            return await Task.FromResult(APIResultCodes.Success);
        }

            // check if NFT instance with this serial number already exists to avoid duplicate serial numbers
        protected async Task<bool> WasNFTInstanceIssuedAsync(DagSystem sys, TokenGenesisBlock token_block, string SerialNumber)
        {
            var non_fungible_tokens = await sys.Storage.GetIssuedNFTInstancesAsync(GetOnlySendBlocks: true, token_block.AccountID, token_block.Ticker);
            if (non_fungible_tokens == null)
                return false;
            foreach (var nft in non_fungible_tokens)
                if (nft.SerialNumber == SerialNumber)
                    return true;
            return false;
        }

        //protected async Task<bool> DoesAccountHaveNFTInstanceAsync(DagSystem sys, string owner_account_id, TokenGenesisBlock token_block, string SerialNumber)
        //{
        //    var non_fungible_tokens = await sys.Storage.GetIssuedNFTInstancesAsync(GetOnlySendBlocks: false, owner_account_id, token_block.Ticker);
        //    if (non_fungible_tokens == null)
        //        return false;

        //    int block_count = 0;

        //    foreach (var nft in non_fungible_tokens)
        //        if (nft.SerialNumber == SerialNumber)
        //            block_count++;

        //    // the issuer's account has one extra send block created when the NFT instance was issued 
        //    if (owner_account_id == token_block.AccountID)
        //        block_count--;

        //    if (block_count <= 0)
        //        return false;


        //    // even number (block_count mod 2 result is zero) means that there was at least a couple of blocks with the serila number, 
        //    // which means that there was receive and send for the same serial number. So the token was received and sent to another account.So there is no token on this account.
        //    if (block_count % 2 == 0)
        //        return false;
        //    return true;
        //}

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

        protected async Task<bool> CheckTokenAsync(DagSystem sys, string tokenName)
        {
            var tokn = await sys.Storage.FindTokenGenesisBlockAsync(null, tokenName);
            return tokn != null;
        }

        private async Task<string[]> GetProperTokenNameAsync(DagSystem sys, string[] tokenNames)
        {
            var result = await tokenNames.SelectAsync(async a => await sys.Storage.FindTokenGenesisBlockAsync(null, a));
            return result.Select(b => b?.Ticker)
                .OrderBy(a => a)
                .ToArray();
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
