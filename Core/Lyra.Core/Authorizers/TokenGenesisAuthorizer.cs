using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using Lyra.Data.Utils;
using Neo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class TokenGenesisAuthorizer : ReceiveTransferAuthorizer
    {
        private readonly List<string> _reservedDomains = new List<string> {
            "wizard", "official", "tether", "contract"
        };

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is TokenGenesisBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as TokenGenesisBlock;

            // Local node validations - before it sends it out to the authorization sample:
            // 1. check if the account already exists
            if(!(tblock is LyraTokenGenesisBlock))
            {
                if (!await sys.Storage.AccountExistsAsync(block.AccountID))
                    return APIResultCodes.AccountDoesNotExist; // 

                TransactionBlock lastBlock = await sys.Storage.FindLatestBlockAsync(block.AccountID) as TransactionBlock;
                if (lastBlock == null)
                    return APIResultCodes.CouldNotFindLatestBlock;

                // check LYR balance
                if (lastBlock.Balances[LyraGlobal.OFFICIALTICKERCODE] != block.Balances[LyraGlobal.OFFICIALTICKERCODE] + block.Fee.ToBalanceLong())
                    return APIResultCodes.InvalidNewAccountBalance;

                // check ticker name
                // https://www.quora.com/What-characters-are-not-allowed-in-a-domain-name
                var r = new Regex(@"[^\w-]|^-|-$");
                if (!block.Ticker.Contains(block.DomainName + "/") || r.IsMatch(block.DomainName))
                    return APIResultCodes.InvalidDomainName;

                if (r.IsMatch(block.Ticker.Replace(block.DomainName + "/", "")))
                    return APIResultCodes.InvalidTickerName;

                if (block.RenewalDate > DateTime.UtcNow.Add(TimeSpan.FromDays(3660)) || block.RenewalDate < DateTime.UtcNow)
                    return APIResultCodes.InvalidTokenRenewalDate;

                if (string.IsNullOrWhiteSpace(block.DomainName))
                    return APIResultCodes.EmptyDomainName;

                bool tokenIssuerIsSeed0 = block.AccountID == ProtocolSettings.Default.StandbyValidators[0];
                if (!tokenIssuerIsSeed0 && block.AccountID != LyraGlobal.GetDexServerAccountID(LyraNodeConfig.GetNetworkId()))
                {
                    if (block.DomainName.Length < 6)
                        return APIResultCodes.DomainNameTooShort;
                    if (_reservedDomains.Any(a => a.Equals(block.DomainName, StringComparison.InvariantCultureIgnoreCase)))
                        return APIResultCodes.DomainNameReserved;
                }
            }

            if (await sys.Storage.WasAccountImportedAsync(block.AccountID))
                return APIResultCodes.CannotModifyImportedAccount;

            var tresult = await VerifyTransactionBlockAsync(sys, block);
            if (tresult != APIResultCodes.Success)
                return tresult;

            // check length
            if (string.IsNullOrWhiteSpace(block.DomainName) || string.IsNullOrWhiteSpace(block.Ticker))
                return APIResultCodes.InvalidTickerName;

            if (block.DomainName.Length >= 64 || block.Ticker.Length >= 64)
                return APIResultCodes.InvalidTickerName;

            // check if this token already exists
            //AccountData genesis_blocks = _accountCollection.GetAccount(AccountCollection.GENESIS_BLOCKS);
            //if (genesis_blocks.FindTokenGenesisBlock(testTokenGenesisBlock) != null)
            if (await sys.Storage.FindTokenGenesisBlockAsync(block.Hash, block.Ticker) != null)
                return APIResultCodes.TokenGenesisBlockAlreadyExists;

            if (block.Fee != (await sys.Storage.GetLastServiceBlockAsync()).TokenGenerationFee)
                return APIResultCodes.InvalidFeeAmount;

            if (block.NonFungibleType == NonFungibleTokenTypes.Collectible && !block.IsNonFungible)
                return APIResultCodes.InvalidNFT;

            if (block.IsNonFungible)
            {
                if (!Signatures.ValidateAccountId(block.NonFungibleKey))
                    return APIResultCodes.InvalidNonFungiblePublicKey;

                // Validate Collectible NFT
                if (block.ContractType == ContractTypes.Collectible)
                {
                    if (block.Precision != 0)
                        return APIResultCodes.InvalidNFT;

                    if (block.NonFungibleType != NonFungibleTokenTypes.Collectible)
                        return APIResultCodes.InvalidNFT;
                }
            }

            return await base.AuthorizeImplAsync(sys, tblock);
        }

        protected override AuthorizationFeeTypes GetFeeType()
        {
            return AuthorizationFeeTypes.Regular;
        }

        protected override decimal GetFeeAmount()
        {
            return 10000m;
        }
    }

    public class LyraGenesisAuthorizer : TokenGenesisAuthorizer
    {
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is LyraTokenGenesisBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as LyraTokenGenesisBlock;

            if ((block as LyraTokenGenesisBlock).Ticker != LyraGlobal.OFFICIALTICKERCODE)
                return APIResultCodes.InvalidBlockType;

            // Local node validations - before it sends it out to the authorization sample:
            // 1. check if the account already exists
            if (await sys.Storage.AccountExistsAsync(block.AccountID))
                return APIResultCodes.AccountAlreadyExists; // 

            // check if this token already exists
            //AccountData genesis_blocks = _accountCollection.GetAccount(AccountCollection.GENESIS_BLOCKS);
            //if (genesis_blocks.FindTokenGenesisBlock(testTokenGenesisBlock) != null)
            if (await sys.Storage.FindTokenGenesisBlockAsync(block.Hash, LyraGlobal.OFFICIALTICKERCODE) != null)
                return APIResultCodes.TokenGenesisBlockAlreadyExists;

            return await base.AuthorizeImplAsync(sys, tblock);
        }
    }
}
