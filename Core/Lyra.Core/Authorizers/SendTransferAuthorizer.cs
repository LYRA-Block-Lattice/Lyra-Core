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

namespace Lyra.Core.Authorizers
{
    public class SendTransferAuthorizer : BaseAuthorizer
    {
        public SendTransferAuthorizer()
        {
        }

        public override async Task<(APIResultCodes, AuthorizationSignature)> AuthorizeAsync<T>(DagSystem sys, T tblock)
        {
            var result = await AuthorizeImplAsync(sys, tblock);
            if (APIResultCodes.Success == result)
                return (APIResultCodes.Success, Sign(sys, tblock));
            else
                return (result, (AuthorizationSignature)null);
        }
        private async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
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
            
            var result = await VerifyBlockAsync(sys, block, lastBlock);
            //stopwatch.Stop();
            //Console.WriteLine($"SendTransfer VerifyBlock takes {stopwatch.ElapsedMilliseconds} ms.");

            if (result != APIResultCodes.Success)
                return result;

            //if (lastBlock.Balances[LyraGlobal.LYRA_TICKER_CODE] <= block.Balances[LyraGlobal.LYRA_TICKER_CODE] + block.Fee)
            //    return AuthorizationResultCodes.NegativeTransactionAmount;

            // Validate the destination account id
            if (!Signatures.ValidateAccountId(block.DestinationAccountId))
                return APIResultCodes.InvalidDestinationAccountId;

            //var stopwatch2 = Stopwatch.StartNew();
            result = await VerifyTransactionBlockAsync(sys, block);
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

            // a normal send is success.
            // monitor special account
            if (block.Tags?.ContainsKey(Block.REQSERVICETAG) == true)
            {
                var svcReqResult = APIResultCodes.Success;
                
                switch(block.Tags[Block.REQSERVICETAG])
                {
                    case BrokerActions.BRK_POOL_CRPL:
                        if (sys.Storage.GetAllBlueprints().Any(x => x.action == BrokerActions.BRK_POOL_CRPL))
                            return APIResultCodes.SystemBusy;

                        svcReqResult = await VerifyCreatingPoolAsync(sys, block, lastBlock);
                        break;
                    case BrokerActions.BRK_POOL_RMLQ:
                        svcReqResult = await VerifyWithdrawFromPoolAsync(sys, block, lastBlock);
                        break;
                    case BrokerActions.BRK_POOL_ADDLQ:
                        svcReqResult = await VerifyAddLiquidateToPoolAsync(sys, block, lastBlock);
                        break;
                    case BrokerActions.BRK_POOL_SWAP:
                        svcReqResult = await VerifyPoolSwapAsync(sys, block, lastBlock);
                        break;
                    default:
                        svcReqResult = await VerifyStkPftAsync(sys, block, lastBlock);
                        break;
                }

                return svcReqResult;  
            }
            else
            {
                // not allow to send to pf
                if(block.DestinationAccountId == PoolFactoryBlock.FactoryAccount)
                {
                    return APIResultCodes.InvalidDestinationAccountId;
                }
            }

            return APIResultCodes.Success;
        }

        private async Task<APIResultCodes> VerifyStkPftAsync(DagSystem sys, SendTransferBlock block, TransactionBlock lastBlock)
        {
            var chgs = block.GetBalanceChanges(lastBlock);
            if (!chgs.Changes.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
                return APIResultCodes.InvalidFeeAmount;

            switch (block.Tags[Block.REQSERVICETAG])
            {
                case BrokerActions.BRK_STK_ADDSTK:
                    if (block.Tags.Count == 1)
                    {
                        // verify sender is the owner of stkingblock
                        var stks = await sys.Storage.FindAllStakingAccountForOwnerAsync(block.AccountID);
                        if (!stks.Any(a => a.AccountID == block.DestinationAccountId))
                            return APIResultCodes.InvalidStakingAccount;
                    }
                    else
                        return APIResultCodes.InvalidBlockTags;
                    break;
                case BrokerActions.BRK_STK_UNSTK:
                    if (
                        block.Tags.ContainsKey("stkid") && !string.IsNullOrWhiteSpace(block.Tags["stkid"])
                        && block.Tags.Count == 2
                        )
                    {
                        if (block.DestinationAccountId != PoolFactoryBlock.FactoryAccount)
                            return APIResultCodes.InvalidServiceRequest;

                        // verify sender is the owner of stkingblock
                        var stks = await sys.Storage.FindAllStakingAccountForOwnerAsync(block.AccountID);
                        if (!stks.Any(a => a.AccountID == block.Tags["stkid"]))
                            return APIResultCodes.InvalidStakingAccount;
                    }
                    else
                        return APIResultCodes.InvalidBlockTags;
                    break;
                case BrokerActions.BRK_STK_CRSTK:   // create staking
                    if (chgs.Changes[LyraGlobal.OFFICIALTICKERCODE] != PoolFactoryBlock.StakingAccountCreateFee)
                        return APIResultCodes.InvalidFeeAmount;

                    string votefor;
                    int days;
                    if (
                        block.Tags.ContainsKey("name") && !string.IsNullOrWhiteSpace(block.Tags["name"]) &&
                        block.Tags.ContainsKey("days") && int.TryParse(block.Tags["days"], out days) && days >= 3 &&
                        block.Tags.ContainsKey("voting") && !string.IsNullOrEmpty(block.Tags["voting"]) &&
                        block.Tags.Count == 4
                        )
                    {
                        var stks = await sys.Storage.FindAllStakingAccountForOwnerAsync(block.AccountID);
                        if (stks.Any(a => a.Name == block.Tags["name"]))
                            return APIResultCodes.DuplicatedName;

                        votefor = block.Tags["voting"];
                        if (!Signatures.ValidateAccountId(votefor))
                        {
                            return APIResultCodes.InvalidProfitingAccount;
                        }
                        var pftgen = await sys.Storage.FindFirstBlockAsync(votefor) as ProfitingGenesis;
                        if(pftgen == null || pftgen.AccountType != AccountTypes.Profiting)
                        {
                            return APIResultCodes.InvalidProfitingAccount;
                        }
                        if(days < 1)
                        {
                            return APIResultCodes.VotingDaysTooSmall;
                        }
                    }
                    else
                        return APIResultCodes.InvalidBlockTags;
                    break;
                case BrokerActions.BRK_PFT_CRPFT:   // create profiting
                    if (chgs.Changes[LyraGlobal.OFFICIALTICKERCODE] != PoolFactoryBlock.ProfitingAccountCreateFee)
                        return APIResultCodes.InvalidFeeAmount;

                    ProfitingType ptype;
                    decimal shareRito;
                    int seats;
                    if (
                        block.Tags.ContainsKey("name") && !string.IsNullOrWhiteSpace(block.Tags["name"]) &&
                        block.Tags.ContainsKey("ptype") && Enum.TryParse(block.Tags["ptype"], false, out ptype)
                        && block.Tags.ContainsKey("share") && decimal.TryParse(block.Tags["share"], out shareRito)
                        && block.Tags.ContainsKey("seats") && int.TryParse(block.Tags["seats"], out seats)
                        && block.Tags.Count == 5
                        )
                    {
                        if (shareRito >= 0m && shareRito <= 1m && seats >= 0 && seats <= 100)
                        {
                            // name dup check
                            var pfts = await sys.Storage.FindAllProfitingAccountForOwnerAsync(block.AccountID);
                            if (pfts.Any(a => a.Name == block.Tags["name"]))
                                return APIResultCodes.DuplicatedName;
                        }
                        else
                        {
                            return APIResultCodes.InvalidShareOfProfit;
                        }
                    }
                    else
                        return APIResultCodes.InvalidBlockTags;
                    break;
                //case BrokerActions.BRK_PFT_FEEPFT:  //TODO: add support
                //    var nodeid = block.Tags.ContainsKey("nodeid") ? block.Tags["nodeid"] : null;
                //    if (nodeid == null)
                //        return APIResultCodes.InvalidAccountId;

                //    var pfts2 = await sys.Storage.FindAllProfitingAccountForOwnerAsync(nodeid);
                //    if(pfts2.Count > 0)
                //    {
                //        var pftid2 = pfts2.First().AccountID;

                //        var pft2 = await sys.Storage.FindFirstBlockAsync(pftid2) as ProfitingGenesis;
                //        if (pft2 == null)
                //            return APIResultCodes.InvalidAccountId;

                //        var stkrs2 = sys.Storage.FindAllStakings(pftid2, DateTime.UtcNow);
                //        if (!stkrs2.Any(a => a.user == block.AccountID) && pft2.OwnerAccountId != block.AccountID)
                //            return APIResultCodes.RequestNotPermited;

                //        // no concurency
                //        if (sys.Storage.GetAllBlueprints().Any(x => x.brokerAccount == pftid2))
                //            return APIResultCodes.SystemBusy;
                //    }

                //    return APIResultCodes.Success;
                case BrokerActions.BRK_PFT_GETPFT:
                    var pftid = block.Tags.ContainsKey("pftid") ? block.Tags["pftid"] : null;
                    if (pftid == null)
                        return APIResultCodes.InvalidAccountId;

                    var pft = await sys.Storage.FindFirstBlockAsync(pftid) as ProfitingGenesis;
                    if (pft == null)
                        return APIResultCodes.InvalidAccountId;

                    var stkrs = sys.Storage.FindAllStakings(pftid, DateTime.UtcNow);
                    if (!stkrs.Any(a => a.user == block.AccountID) && pft.OwnerAccountId != block.AccountID)
                        return APIResultCodes.RequestNotPermited;

                    // no concurency
                    if (sys.Storage.GetAllBlueprints().Any(x => x.brokerAccount == pftid))
                        return APIResultCodes.SystemBusy;

                    return APIResultCodes.Success;
                default:
                    return APIResultCodes.InvalidServiceRequest;
            }
            return APIResultCodes.Success;
        }

        private async Task<APIResultCodes> CheckTagsAsync(DagSystem sys, Block block, int tagsCount = 3)
        {
            if (block.Tags.ContainsKey("token0") && await CheckTokenAsync(sys, block.Tags["token0"])
                && block.Tags.ContainsKey("token1") && await CheckTokenAsync(sys, block.Tags["token1"])
                && block.Tags["token0"] != block.Tags["token1"]
                && (block.Tags["token0"] == LyraGlobal.OFFICIALTICKERCODE || block.Tags["token1"] == LyraGlobal.OFFICIALTICKERCODE)
                && block.Tags.Count == tagsCount
                )
                return APIResultCodes.Success;
            else
                return APIResultCodes.InvalidBlockTags;
        }

        private async Task<APIResultCodes> VerifyCreatingPoolAsync(DagSystem sys, SendTransferBlock block, TransactionBlock lastBlock)
        {
            // generic pool factory
            var tgc = await CheckTagsAsync(sys, block);
            if (tgc != APIResultCodes.Success)
                return tgc;

            var chgs = block.GetBalanceChanges(lastBlock);
            if (!chgs.Changes.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
                return APIResultCodes.InvalidFeeAmount;

            if (chgs.Changes.Count > 1)
                return APIResultCodes.InvalidFeeAmount;

            // check if pool exists
            var factory = await sys.Storage.GetPoolFactoryAsync();
            if (factory == null)
                return APIResultCodes.SystemNotReadyToServe;

            // action

            if (chgs.Changes[LyraGlobal.OFFICIALTICKERCODE] != PoolFactoryBlock.PoolCreateFee)
                return APIResultCodes.InvalidFeeAmount;

            var poolGenesis = await sys.Storage.GetPoolAsync(block.Tags["token0"], block.Tags["token1"]);
            if (poolGenesis != null)
                return APIResultCodes.PoolAlreadyExists;

            return APIResultCodes.Success;
        }

        private async Task<APIResultCodes> VerifyWithdrawFromPoolAsync(DagSystem sys, SendTransferBlock block, TransactionBlock lastBlock)
        {
            var tgc = await CheckTagsAsync(sys, block);
            if (tgc != APIResultCodes.Success)
                return tgc;

            var poolGenesis = await sys.Storage.GetPoolAsync(block.Tags["token0"], block.Tags["token1"]);
            if (poolGenesis == null)
                return APIResultCodes.PoolNotExists;

            var chgs = block.GetBalanceChanges(lastBlock);

            if (chgs.Changes[LyraGlobal.OFFICIALTICKERCODE] != 1m)
                return APIResultCodes.InvalidFeeAmount;

            if (!(await sys.Storage.FindLatestBlockAsync(poolGenesis.AccountID) is IPool pool))
                return APIResultCodes.PoolNotExists;

            if (!pool.Shares.ContainsKey(block.AccountID))
                return APIResultCodes.PoolShareNotExists;

            // check pending swap
            if (sys.Storage.GetAllBlueprints().Any(x => x.brokerAccount == poolGenesis.AccountID))
                return APIResultCodes.ReQuotaNeeded;

            return APIResultCodes.Success;
        }

        private async Task<APIResultCodes> VerifyPoolAsync(DagSystem sys, SendTransferBlock block, TransactionBlock lastBlock)
        {
            var poolGenesis = await sys.Storage.GetPoolAsync(block.Tags["token0"], block.Tags["token1"]);
            if (poolGenesis == null)
                return APIResultCodes.PoolNotExists;

            var poolGenesis2 = await sys.Storage.FindFirstBlockAsync(block.DestinationAccountId);
            if (poolGenesis2 == null)
                return APIResultCodes.PoolNotExists;

            if (poolGenesis.Hash != poolGenesis2.Hash)
                return APIResultCodes.PoolNotExists;

            return APIResultCodes.Success;
        }

        private async Task<APIResultCodes> VerifyAddLiquidateToPoolAsync(DagSystem sys, SendTransferBlock block, TransactionBlock lastBlock)
        {
            var tgc = await CheckTagsAsync(sys, block);
            if (tgc != APIResultCodes.Success)
                return tgc;

            var vp = await VerifyPoolAsync(sys, block, lastBlock);
            if (vp != APIResultCodes.Success)
                return vp;

            var chgs = block.GetBalanceChanges(lastBlock);
            if (chgs.Changes.Count != 2)
                return APIResultCodes.InvalidPoolDepositionAmount;

            var poolGenesis = await sys.Storage.GetPoolAsync(block.Tags["token0"], block.Tags["token1"]);

            if (!chgs.Changes.ContainsKey(poolGenesis.Token0) || !chgs.Changes.ContainsKey(poolGenesis.Token1))
                return APIResultCodes.InvalidPoolDepositionAmount;

            var poolLatest = await sys.Storage.FindLatestBlockAsync(block.DestinationAccountId) as TransactionBlock;
            // compare rito
            if (poolLatest.Balances.ContainsKey(poolGenesis.Token0) && poolLatest.Balances.ContainsKey(poolGenesis.Token1)
                && poolLatest.Balances[poolGenesis.Token0] > 0 && poolLatest.Balances[poolGenesis.Token1] > 0
                )
            {
                var rito = (poolLatest.Balances[poolGenesis.Token0].ToBalanceDecimal() / poolLatest.Balances[poolGenesis.Token1].ToBalanceDecimal());
                var token0Amount = chgs.Changes[poolGenesis.Token0];
                var token1AmountShouldBe = Math.Round(token0Amount / rito, 8);
                if (chgs.Changes[poolGenesis.Token1] != token1AmountShouldBe
                    && Math.Abs(chgs.Changes[poolGenesis.Token1] - token1AmountShouldBe) / token1AmountShouldBe > 0.0000001m
                    )
                    return APIResultCodes.InvalidPoolDepositionRito;
            }

            // check pending swap
            if (sys.Storage.GetAllBlueprints().Any(x => x.brokerAccount == block.DestinationAccountId))
                return APIResultCodes.ReQuotaNeeded;

            return APIResultCodes.Success;
        }

        private async Task<APIResultCodes> VerifyPoolSwapAsync(DagSystem sys, SendTransferBlock block, TransactionBlock lastBlock)
        {
            var tgc = await CheckTagsAsync(sys, block, 4);
            if (tgc != APIResultCodes.Success)
                return tgc;

            var vp = await VerifyPoolAsync(sys, block, lastBlock);
            if (vp != APIResultCodes.Success)
                return vp;

            var chgs = block.GetBalanceChanges(lastBlock);
            var poolGenesis = await sys.Storage.GetPoolAsync(block.Tags["token0"], block.Tags["token1"]);

            if (chgs.Changes.Count != 1)
                return APIResultCodes.InvalidTokenToSwap;

            string tokenToSwap = null;
            var kvp = chgs.Changes.First();
            if (kvp.Key == poolGenesis.Token0)
                tokenToSwap = poolGenesis.Token0;
            else if (kvp.Key == poolGenesis.Token1)
                tokenToSwap = poolGenesis.Token1;

            if (tokenToSwap == null)
                return APIResultCodes.InvalidTokenToSwap;

            // check amount
            var poolLatest = await sys.Storage.FindLatestBlockAsync(block.DestinationAccountId) as TransactionBlock;
            //if (kvp.Value > poolLatest.Balances[tokenToSwap].ToBalanceDecimal() / 2)
            //    return APIResultCodes.TooManyTokensToSwap;
            // uniswap AMM don't mind how many token want to swap

            if (block.Tags.ContainsKey("minrecv"))
            {
                if (!long.TryParse(block.Tags["minrecv"], out long toGetLong))
                    return APIResultCodes.InvalidSwapSlippage;

                decimal toGet = toGetLong.ToBalanceDecimal();

                if (toGet <= 0)
                    return APIResultCodes.InvalidSwapSlippage;

                if (poolLatest.Balances.Any(a => a.Value == 0))
                {
                    // can't calculate rito
                    return APIResultCodes.PoolOutOfLiquidaty;
                }

                var cal = new SwapCalculator(poolGenesis.Token0, poolGenesis.Token1, poolLatest,
                    chgs.Changes.First().Key, chgs.Changes.First().Value, 0);

                if (cal.SwapOutAmount < toGet)
                {
                    return APIResultCodes.SwapSlippageExcceeded;
                }
            }

            // check pending swap
            if (sys.Storage.GetAllBlueprints().Any(x => x.brokerAccount == block.DestinationAccountId))
                return APIResultCodes.ReQuotaNeeded;

            return APIResultCodes.Success;
        }


        protected override async Task<APIResultCodes> ValidateFeeAsync(DagSystem sys, TransactionBlock block)
        {
            APIResultCodes result = APIResultCodes.Success;
            if (block.FeeType != AuthorizationFeeTypes.Regular)
                result = APIResultCodes.InvalidFeeAmount;

            if (block.Fee != (await sys.Storage.GetLastServiceBlockAsync()).TransferFee)
                result = APIResultCodes.InvalidFeeAmount;

            return result;
        }

        protected override async Task<APIResultCodes> ValidateCollectibleNFTAsync(DagSystem sys, TransactionBlock send_or_receice_block, TokenGenesisBlock token_block)
        {
            if (send_or_receice_block.NonFungibleToken.Denomination != 1)
                return APIResultCodes.InvalidCollectibleNFTDenomination;

            if (string.IsNullOrEmpty(send_or_receice_block.NonFungibleToken.SerialNumber))
                return APIResultCodes.InvalidCollectibleNFTSerialNumber;

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