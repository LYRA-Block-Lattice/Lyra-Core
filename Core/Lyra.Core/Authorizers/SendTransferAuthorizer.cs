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

            // temperory lock down address
            //try
            //{
            //    var addrs = new List<string>();
            //    addrs.Add("LVFhxi6f89bzoa7vGM5aizhGWutLXYu3YqaxtfeYpvBbLQvfSJLokxiumt5ryHZWrWWgQXHXLjt6HTZDj7F4PU9vtgNwhJ");
            //    addrs.Add("L4BsJXEb7zB7PMd1tg3VV594y2KwksrbooaghiqbWQ5hFFcy5gLiDbsH1Htvc8KxiXhH6soxAUubGQiWgeAgfgDkH2VJy2");

            //    // Lyra team's address
            //    var target = "L5ViiZbSmLJJpXppwBCNPuCzRds2VMkydvfcENp3SxqAfLNuqk5JuuDrshmJNCjTo6oKgXRagCnTrVXseyxn2q74vXmYcG";

            //    if (addrs.Contains(block.AccountID) && block.DestinationAccountId != target)
            //    {
            //        return APIResultCodes.AccountLockDown;
            //    }
            //}
            //catch (Exception) { }
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
                if (block.Tags != null
                        && block.Tags.ContainsKey("token0") && await CheckTokenAsync(sys, block.Tags["token0"])
                        && block.Tags.ContainsKey("token1") && await CheckTokenAsync(sys, block.Tags["token1"])
                        && block.Tags["token0"] != block.Tags["token1"]
                        && (block.Tags["token0"] == LyraGlobal.OFFICIALTICKERCODE || block.Tags["token1"] == LyraGlobal.OFFICIALTICKERCODE)
                        )
                {
                    if (block.DestinationAccountId == PoolFactoryBlock.FactoryAccount)
                    {
                        var chgs = block.GetBalanceChanges(lastBlock);
                        if (!chgs.Changes.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
                            return APIResultCodes.InvalidFeeAmount;

                        if (chgs.Changes.Count > 1)
                            return APIResultCodes.InvalidFeeAmount;

                        if (block.Tags[Block.REQSERVICETAG] == "")
                        {
                            if (chgs.Changes[LyraGlobal.OFFICIALTICKERCODE] != PoolFactoryBlock.PoolCreateFee)
                                return APIResultCodes.InvalidFeeAmount;

                            // check if pool exists
                            var factory = await sys.Storage.GetPoolFactoryAsync();
                            if (factory == null)
                                return APIResultCodes.SystemNotReadyToServe;

                            var poolGenesis = await sys.Storage.GetPoolAsync(block.Tags["token0"], block.Tags["token1"]);
                            if (poolGenesis != null)
                                return APIResultCodes.PoolAlreadyExists;
                        }
                        else if (block.Tags[Block.REQSERVICETAG] == "poolwithdraw")
                        {
                            var poolGenesis = await sys.Storage.GetPoolAsync(block.Tags["token0"], block.Tags["token1"]);
                            if (poolGenesis == null)
                                return APIResultCodes.PoolNotExists;

                            if (chgs.Changes[LyraGlobal.OFFICIALTICKERCODE] != 1m)
                                return APIResultCodes.InvalidFeeAmount;

                            var pool = await sys.Storage.FindLatestBlockAsync(poolGenesis.AccountID) as IPool;
                            if (pool == null)
                                return APIResultCodes.PoolNotExists;

                            if (!pool.Shares.ContainsKey(block.AccountID))
                                return APIResultCodes.PoolShareNotExists;
                        }
                        else
                        {
                            return APIResultCodes.InvalidPoolOperation;
                        }
                    }
                    else  // target should be a pool
                    {
                        var poolGenesis = await sys.Storage.GetPoolAsync(block.Tags["token0"], block.Tags["token1"]);
                        if (poolGenesis == null)
                            return APIResultCodes.PoolNotExists;

                        var poolGenesis2 = await sys.Storage.FindFirstBlockAsync(block.DestinationAccountId);
                        if (poolGenesis2 == null)
                            return APIResultCodes.PoolNotExists;

                        if (poolGenesis.Hash != poolGenesis2.Hash)
                            return APIResultCodes.PoolNotExists;

                        if(block.Tags[Block.REQSERVICETAG] == "")
                        {
                            var chgs = block.GetBalanceChanges(lastBlock);
                            if (chgs.Changes.Count != 2)
                                return APIResultCodes.InvalidPoolDepositionAmount;

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
                        }

                        else if(block.Tags[Block.REQSERVICETAG] == "swaptoken")
                        {
                            var chgs = block.GetBalanceChanges(lastBlock);
                            if (chgs.Changes.Count != 1)
                                return APIResultCodes.InvalidTokenToSwap;

                            string tokenToSwap = null;
                            var kvp = chgs.Changes.First();
                            if (kvp.Key == poolGenesis.Token0)
                                tokenToSwap = poolGenesis.Token0;
                            else if (kvp.Key == poolGenesis.Token1)
                                tokenToSwap = poolGenesis.Token1;

                            if(tokenToSwap == null)
                                return APIResultCodes.InvalidTokenToSwap;

                            // check amount
                            var poolLatest = await sys.Storage.FindLatestBlockAsync(block.DestinationAccountId) as TransactionBlock;
                            //if (kvp.Value > poolLatest.Balances[tokenToSwap].ToBalanceDecimal() / 2)
                            //    return APIResultCodes.TooManyTokensToSwap;
                            // uniswap AMM don't mind how many token want to swap

                            if(block.Tags.ContainsKey("minrecv"))
                            {
                                long toGetLong;

                                if (!long.TryParse(block.Tags["minrecv"], out toGetLong))
                                    return APIResultCodes.InvalidSwapSlippage;

                                decimal toGet = toGetLong.ToBalanceDecimal();

                                if(toGet <= 0)
                                    return APIResultCodes.InvalidSwapSlippage;

                                if (poolLatest.Balances.Any(a => a.Value == 0))
                                {
                                    // can't calculate rito
                                    return APIResultCodes.PoolOutOfLiquidaty;
                                }

                                var cal = new SwapCalculator(poolGenesis.Token0, poolGenesis.Token1, poolLatest,
                                    chgs.Changes.First().Key, chgs.Changes.First().Value, 0);
                                
                                if(cal.SwapOutAmount < toGet)
                                {
                                    return APIResultCodes.SwapSlippageExcceeded;
                                }
                            }
                        }
                    }
                }
                else
                    return APIResultCodes.InvalidTokenPair;
            }
            else if (block.DestinationAccountId == PoolFactoryBlock.FactoryAccount)
            {
                return APIResultCodes.InvalidPoolOperation;
            }

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