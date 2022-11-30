using Akka.Actor;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using Lyra.Data.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Lyra.Core.Decentralize.ConsensusService;

namespace Lyra.Core.Authorizers
{
    public abstract class TransactionAuthorizer : BaseAuthorizer
    {
        static readonly List<string> TradeOnlyDomains = new List<string>
        {
            "tot", "sku", "svc"
        };
        static readonly List<string> NonFungibleDomains = new List<string>{
            "nft", "tot", "sku", "svc"
        };
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            var tx = tblock as TransactionBlock;
            if (tx == null)
                return APIResultCodes.InvalidBlockType;

            if (!Signatures.ValidateAccountId(tx.AccountID))
                return APIResultCodes.InvalidAccountId;

            if (tx.Balances == null)
                return APIResultCodes.InvalidBalance;

            if (tx.Balances.Values.Any(x => x < 0))
                return APIResultCodes.InvalidBalance;

            if (tx.Fee < 0)
                return APIResultCodes.InvalidFeeAmount;

            if (tx.FeeCode != LyraGlobal.OFFICIALTICKERCODE)
                return APIResultCodes.InvalidFeeTicker;

            if(!(tblock is IPool || tblock is IBrokerAccount) && tx.FeeType != GetFeeType())
                return APIResultCodes.InvalidFeeType;

            if (tx.FeeType == AuthorizationFeeTypes.NoFee && tx.Fee > 0)
                return APIResultCodes.InvalidFeeAmount;

            if (tx.FeeType == AuthorizationFeeTypes.Regular)
                if (tx.Fee != GetFeeAmount())
                    return APIResultCodes.InvalidFeeAmount;

            var vf = await ValidateFeeAsync(sys, tx);
            if (vf != APIResultCodes.Success)
                return vf;

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "TransactionAuthorizer->BaseAuthorizer");
        }

        protected override async Task<APIResultCodes> VerifyWithPrevAsync(DagSystem sys, Block block, Block previousBlock)
        {
            if (previousBlock != null && (block as TransactionBlock).AccountID != (previousBlock as TransactionBlock).AccountID)
                return APIResultCodes.InvalidAccountId;

            var blockt = block as TransactionBlock;
            var uniNow = DateTime.UtcNow;

            if (!blockt.VerifyHash())
                _log.LogWarning($"VerifyBlock VerifyHash failed for TransactionBlock Index: {block.Height} by {block.GetHashInput()}");

            var verifyAgainst = blockt.AccountID;

            if (block.Height > 1 && previousBlock == null)
                return APIResultCodes.InvalidPreviousBlock;

            if (block.ContainsTag(Block.MANAGEDTAG))
            {
                //if(!Enum.TryParse(block.Tags[Block.MANAGEDTAG], out Block.ManagedState mgdstate))
                //    return APIResultCodes.InvalidManagementBlock;

                //if (block.Tags[Block.MANAGEDTAG] != "")
                //{
                //    Console.WriteLine("block.Tags[Block.MANAGEDTAG] != ''");
                //    return APIResultCodes.InvalidManagementBlock;
                //}

                //if (!(block is IBrokerAccount) && !(block is PoolFactoryBlock) && !(block is IPool))
                //    return APIResultCodes.InvalidBrokerAcount;

                if (!IsManagedBlockAllowed(sys, blockt))
                {
                    Console.WriteLine("!IsManagedBlockAllowed");
                    return APIResultCodes.InvalidManagementBlock;
                }                    

                string reltx = null;
                if (block is IBrokerAccount ib)
                    reltx = ib.RelatedTx;
                else if (block is IPool ip)
                    reltx = ip.RelatedTx;

                if (reltx != null)
                {
                    var txnew = await ConsensusService.Singleton.GetBlockByRelatedTxForCompareAuthAsync(reltx) as Block;
                    //var wf = BrokerFactory.GetBlueprint(reltx);
                    //if (wf == null)
                    //{
                    //    Console.WriteLine("wf == null");
                    //    return APIResultCodes.InvalidManagementBlock;
                    //}

                    //TransactionBlock txnew = null;
                    //await wf.ExecuteAsync(sys, (blk) =>
                    //{
                    //    txnew = blk;
                    //    return Task.CompletedTask;
                    //}, "TransactionAuthorizer");

                    //if (txnew == null)
                    //{
                    //    Console.WriteLine("txnew == null");
                    //    return APIResultCodes.InvalidManagementBlock;
                    //}

                    // compare block
                    if (!block.AuthCompare(txnew))
                    {
                        Console.WriteLine("\n!block.AuthCompare(txnew). Block vs TxNew");
                        Console.WriteLine(ObjectDumper.Dump(block));
                        Console.WriteLine(ObjectDumper.Dump(txnew));
                        Console.WriteLine();
                        return APIResultCodes.InvalidManagedTransaction;
                    }                        
                }

                var board = await sys.Consensus.Ask<BillBoard>(new AskForBillboard());
                verifyAgainst = board.CurrentLeader;
            }
            else
            {
                if (block is IBrokerAccount)
                    return APIResultCodes.InvalidBrokerAcount;

                if (block.Height > 1)
                {
                    var firstBlock = await sys.Storage.FindFirstBlockAsync(blockt.AccountID);
                    if (firstBlock is IBrokerAccount || firstBlock.ContainsTag(Block.MANAGEDTAG))
                        return APIResultCodes.InvalidBrokerAcount;
                }
            }

            if (previousBlock != null && previousBlock.ContainsTag(Block.MANAGEDTAG))
            {
                if (!blockt.ContainsTag(Block.MANAGEDTAG))
                {
                    Console.WriteLine("!blockt.ContainsTag(Block.MANAGEDTAG)");
                    return APIResultCodes.InvalidManagementBlock;
                }

                //if (!Enum.TryParse(blockt.Tags[Block.MANAGEDTAG], out Block.ManagedState mgdstate))
                //    return APIResultCodes.InvalidManagementBlock;

                //if (blockt.Tags[Block.MANAGEDTAG] != "")
                //{
                //    Console.WriteLine("blockt.Tags[Block.MANAGEDTAG] != ''");
                //    return APIResultCodes.InvalidManagementBlock;
                //}

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
            {
                // verify svc hash exists
                var svc2 = await sys.Storage.FindBlockByHashAsync(blockt.ServiceHash);
                if (svc2 == null)
                    return APIResultCodes.ServiceBlockNotFound;
            }

            //if (!await ValidateRenewalDateAsync(sys, blockt, previousBlock as TransactionBlock))
            //    return APIResultCodes.TokenExpired;

            if (sys.ConsensusState != BlockChainState.StaticSync)
            {
                if (block.TimeStamp < uniNow.AddSeconds(-120) || block.TimeStamp > uniNow.AddSeconds(3))
                {
                    _log.LogInformation($"TimeStamp 2: {block.TimeStamp} Universal Time Now: {uniNow}");
                    return APIResultCodes.InvalidBlockTimeStamp;
                }
            }

            return await base.VerifyWithPrevAsync(sys, block, previousBlock);
        }

        protected virtual bool IsManagedBlockAllowed(DagSystem sys, TransactionBlock block)
        {
            return block is IPool || block is IBrokerAccount;
        }

        protected virtual decimal GetFeeAmount()
        {
            return 0;
        }
        protected virtual AuthorizationFeeTypes GetFeeType()
        {
            throw new NotImplementedException();
        }

        protected virtual Task<APIResultCodes> ValidateFeeAsync(DagSystem sys, TransactionBlock block)
        {
            return Task.FromResult(APIResultCodes.Success);
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

                    //if(block.ContainsTag(Block.MANAGEDTAG))
                    //{
                    //    var svcBlock = await sys.Storage.FindBlockByHashAsync(block.ServiceHash) as ServiceBlock;
                    //    var result = Signatures.VerifyAccountSignature(thisBlock.Hash, svcBlock.Leader, thisBlock.Signature);
                    //    if (!result)
                    //        return APIResultCodes.AccountChainSignatureValidationFailed;
                    //}
                    //else
                    //{
                    //    var result = Signatures.VerifyAccountSignature(thisBlock.Hash, thisBlock.AccountID, thisBlock.Signature);
                    //    if (!result)
                    //        return APIResultCodes.AccountChainSignatureValidationFailed;
                    //}

                    thisBlock = prevBlock;
                }

                // verify the spending
                //TransactionBlock previousTransaction = await sys.Storage.FindBlockByHashAsync(block.PreviousHash) as TransactionBlock;
                //foreach (var prevbalance in previousTransaction.Balances)
                //{
                //    // make sure all balances from the previous block are present in a new block even if they are unchanged
                //    if (!block.Balances.ContainsKey(prevbalance.Key))
                //        return APIResultCodes.AccountChainBalanceValidationFailed;
                //}

                // no, don't do it. zero/zoombie balance should go.
            }

            return APIResultCodes.Success;
        }

        protected async Task<bool> ValidateRenewalDateAsync(DagSystem sys, TransactionBlock block, TransactionBlock previousBlock)
        {
            if (previousBlock == null)
                return true;

            var trs = block.GetBalanceChanges(previousBlock);

            foreach (var chg in trs.Changes)
            {
                var token = await sys.Storage.FindTokenGenesisBlockAsync(null, chg.Key);
                if (token != null && token.RenewalDate < DateTime.UtcNow)
                    return false;

                if (token == null && block is TokenGenesisBlock gen && gen.Ticker != chg.Key)
                    return false;
            }

            return true;
        }

        protected virtual async Task<APIResultCodes> ValidateNonFungibleAsync(DagSystem sys, TransactionBlock send_or_receice_block, TransactionBlock previousBlock)
        {
            var transaction = send_or_receice_block.GetBalanceChanges(previousBlock);

            if (transaction.Changes.Count == 1 && transaction.Changes.First().Key == LyraGlobal.OFFICIALTICKERCODE)
                return APIResultCodes.Success;

            foreach (var chg in transaction.Changes)
            {
                var tokenCode = chg.Key;
                var tokenAmount = chg.Value;

                if(tokenCode.Contains("#"))
                {
                    tokenCode = tokenCode.Split('#')[0];
                }

                var nftgen = await sys.Storage.FindTokenGenesisBlockAsync(null, tokenCode);
                if (nftgen == null)
                    return APIResultCodes.TokenGenesisBlockNotFound;

                if (NonFungibleDomains.Contains(nftgen.DomainName) && Math.Round(tokenAmount, 0) != tokenAmount)
                    return APIResultCodes.InvalidNonFungibleAmount;

                if(send_or_receice_block.BlockType == BlockTypes.SendTransfer && TradeOnlyDomains.Contains(nftgen.DomainName))
                {
                    if (send_or_receice_block.Tags == null)
                        return APIResultCodes.TotTransferNotAllowed;

                    // verify tot
                    if (send_or_receice_block.Tags.ContainsKey(Block.MANAGEDTAG))
                    {
                        // default contract generated block is allowed.
                    }
                    else
                    {
                        // non contract blocks 
                        if (!send_or_receice_block.Tags.ContainsKey(Block.REQSERVICETAG))
                        {
                            return APIResultCodes.TotTransferNotAllowed;
                        }

                        if (send_or_receice_block.Tags[Block.REQSERVICETAG] != BrokerActions.BRK_UNI_CRODR
                            && send_or_receice_block.Tags[Block.REQSERVICETAG] != BrokerActions.BRK_UNI_CRTRD)
                        {
                            return APIResultCodes.TotTransferNotAllowed;
                        }
                    }
                }

                //INonFungibleToken non_fungible_token = send_block.GetNonFungibleTransaction(previousBlock);
                if (send_or_receice_block.NonFungibleToken == null)
                {

                }
                else
                {
                    if (send_or_receice_block.NonFungibleToken.Denomination != tokenAmount)
                        return APIResultCodes.InvalidNonFungibleAmount;

                    if (send_or_receice_block.NonFungibleToken.TokenCode != tokenCode)
                        return APIResultCodes.InvalidNonFungibleTokenCode;

                    var vr = send_or_receice_block.NonFungibleToken.VerifySignature(nftgen.NonFungibleKey ?? nftgen.AccountID);
                    if (!vr)
                        return APIResultCodes.NonFungibleSignatureVerificationFailed;

                    if (nftgen.ContractType == ContractTypes.Collectible)
                    {
                        var res = await ValidateCollectibleNFTAsync(sys, send_or_receice_block, nftgen);
                        if (res != APIResultCodes.Success)
                            return res;
                    }
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
    }
}
