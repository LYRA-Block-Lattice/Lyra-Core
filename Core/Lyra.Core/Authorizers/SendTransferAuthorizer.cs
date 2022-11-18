using System;
using System.Collections.Generic;
using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Lyra.Core.Utils;
using Lyra.Core.Accounts;
using System.Diagnostics;
using Lyra.Core.API;
using System.Linq;
using Lyra.Data.API;
using Lyra.Data.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.Utils;
using Lyra.Core.WorkFlow;

namespace Lyra.Core.Authorizers
{
    public class SendTransferAuthorizer : TransactionAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.SendTransfer;
        }

        // decouple send. minimal verification.
        // target normal account: ok or lost.
        // target IBrokerAccount: unreceive by workflow.
        // most important, no lock at all!!!
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is SendTransferBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as SendTransferBlock;

            // Diego SCAM contract temperory lock down address
            // should be removed if token back
            try
            {
                var addrs = new List<string>
                {
                    "LVFhxi6f89bzoa7vGM5aizhGWutLXYu3YqaxtfeYpvBbLQvfSJLokxiumt5ryHZWrWWgQXHXLjt6HTZDj7F4PU9vtgNwhJ",
                    "L4BsJXEb7zB7PMd1tg3VV594y2KwksrbooaghiqbWQ5hFFcy5gLiDbsH1Htvc8KxiXhH6soxAUubGQiWgeAgfgDkH2VJy2"
                };

                // Lyra team's address
                var target = "L5ViiZbSmLJJpXppwBCNPuCzRds2VMkydvfcENp3SxqAfLNuqk5JuuDrshmJNCjTo6oKgXRagCnTrVXseyxn2q74vXmYcG";

                if (addrs.Contains(block.AccountID) && block.DestinationAccountId != target)
                {
                    return APIResultCodes.AccountLockDown;
                }
            }
            catch (Exception) { }
            // end

            if (block.AccountID.Equals(block.DestinationAccountId))
                return APIResultCodes.CannotSendToSelf;

            if (block.AccountID.Equals(LyraGlobal.BURNINGACCOUNTID))
                return APIResultCodes.InvalidAccountId;

            // fiat token never be in wallet.
            if(block.Balances.Any(a => a.Key.ToLower().StartsWith("fiat/")))
                return APIResultCodes.InvalidBalance;

            //// 1. check if the account already exists
            //if (!await sys.Storage.AccountExists(block.AccountID))
            //    return APIResultCodes.AccountDoesNotExist;
            //var stopwatch = Stopwatch.StartNew();

            //TransactionBlock lastBlock = null;
            //int count = 50;
            //while(count-- > 0)
            //{
            //    lastBlock = await sys.Storage.FindBlockByHashAsync(block.PreviousHash);
            //    if (lastBlock != null)
            //        break;
            //    Task.Delay(100).Wait();
            //}

            if (await sys.Storage.WasAccountImportedAsync(block.AccountID))
                return APIResultCodes.CannotModifyImportedAccount;

            TransactionBlock lastBlock = await sys.Storage.FindBlockByHashAsync(block.PreviousHash) as TransactionBlock;

            //TransactionBlock lastBlock = await sys.Storage.FindLatestBlock(block.AccountID);
            if (lastBlock == null)
                return APIResultCodes.PreviousBlockNotFound;
            
            //if (lastBlock.Balances[LyraGlobal.LYRA_TICKER_CODE] <= block.Balances[LyraGlobal.LYRA_TICKER_CODE] + block.Fee)
            //    return AuthorizationResultCodes.NegativeTransactionAmount;

            // Validate the destination account id
            if (!Signatures.ValidateAccountId(block.DestinationAccountId))
                return APIResultCodes.InvalidDestinationAccountId;

            //var stopwatch2 = Stopwatch.StartNew();
            var result = await VerifyTransactionBlockAsync(sys, block);
            //stopwatch2.Stop();
            //Console.WriteLine($"SendTransfer VerifyTransactionBlock takes {stopwatch2.ElapsedMilliseconds} ms.");
            if (result != APIResultCodes.Success)
                return result;

            //var stopwatch3 = Stopwatch.StartNew();
            if (!block.ValidateTransaction(lastBlock))
                return APIResultCodes.SendTransactionValidationFailed;

            result = await ValidateNonFungibleAsync(sys, block, lastBlock);
            if (result != APIResultCodes.Success)
                return result;

            //stopwatch3.Stop();
            //Console.WriteLine($"SendTransfer ValidateTransaction & ValidateNonFungible takes {stopwatch3.ElapsedMilliseconds} ms.");

/*            // a normal send is success.
            // monitor special account
            if (block.Tags?.ContainsKey(Block.REQSERVICETAG) == true)
            {
                var svcReqResult = APIResultCodes.InvalidServiceRequest;

                if (BrokerFactory.DynWorkFlows.ContainsKey(block.Tags[Block.REQSERVICETAG]))
                {
                    var wf = BrokerFactory.DynWorkFlows[block.Tags[Block.REQSERVICETAG]];

                    var rl = await wf.PreAuthAsync(sys, block, lastBlock);
                    svcReqResult = rl.Result;

                    if(rl.Result == APIResultCodes.Success)
                    {
                        // lock IDs
                        _lockedIds = rl.LockedIDs;
                    }
                    else
                    {
                        _lockedIds = null;
                    }
                }

                if (svcReqResult != APIResultCodes.Success)
                {
                    Console.WriteLine($"SVCREQ failed for {block.Tags[Block.REQSERVICETAG]}: {svcReqResult}");
                    return svcReqResult;
                }                    
            }
            else
            {
                // not allow to send to pf
                if(block.DestinationAccountId == PoolFactoryBlock.FactoryAccount)
                {
                    return APIResultCodes.InvalidDestinationAccountId;
                }
            }*/

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "SendTransferAuthorizer->TransactionAuthorizer");
        }


        protected override decimal GetFeeAmount()
        {
            return 1m;
        }

        protected override AuthorizationFeeTypes GetFeeType()
        {
            return AuthorizationFeeTypes.Regular;
        }

        protected override async Task<APIResultCodes> ValidateCollectibleNFTAsync(DagSystem sys, TransactionBlock send_or_receice_block, TokenGenesisBlock token_block)
        {
            if (send_or_receice_block.NonFungibleToken.Denomination != 1)
                return APIResultCodes.InvalidCollectibleNFTDenomination;

            // allow serial to be null. so the NFT is really unique.
            //if (string.IsNullOrEmpty(send_or_receice_block.NonFungibleToken.SerialNumber))
            //    return APIResultCodes.InvalidCollectibleNFTSerialNumber;

            bool nft_instance_exists = await WasNFTInstanceIssuedAsync(sys, token_block, send_or_receice_block.NonFungibleToken.SerialNumber);
            bool is_there_a_token = await sys.Storage.DoesAccountHaveCollectibleNFTInstanceAsync(send_or_receice_block.AccountID, token_block, send_or_receice_block.NonFungibleToken.SerialNumber);

            if (nft_instance_exists) // this is a transfer of existing instance to another account
            {
                if (!is_there_a_token && send_or_receice_block.AccountID == token_block.AccountID)
                    return APIResultCodes.DuplicateNFTCollectibleSerialNumber;

                if (!is_there_a_token && send_or_receice_block.AccountID != token_block.AccountID)
                    return APIResultCodes.InsufficientFunds;
            }
            else // otherwise, this is an attempt to issue a new instance
            {
                // Only the owner of the genesis token block can issue a new instance of NFT
                if (send_or_receice_block.AccountID != token_block.AccountID)
                    return APIResultCodes.NFTCollectibleSerialNumberDoesNotExist;
            }

            return APIResultCodes.Success;
        }

    }
}