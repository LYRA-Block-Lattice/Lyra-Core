using Lyra.Core.Protos;
using System;

namespace Lyra.Core.Blocks.Transactions
{
    public enum ContractTypes: uint
    {
        Default = 0,

        RewardPoint = 10,

        RedeemedDiscount = 20,

        GiftCard = 30,

        DiscountCoupon = 40,

        StoreCredit = 50,

        Cryptocurrency = 100,

        FiatCurrency = 200,

        Collectible = 300,
               
        Custom = 1000
    }
    
    // Creates a new token type
    public class TokenGenesisBlock : ReceiveTransferBlock//, IFeebleBlock
    {
        // This is the unique token ID, cannot be used twice in the network
        // Example: Sturbucks.Rewards
        public string Ticker { get; set; }

        /// <summary>
        /// example: Starbucks
        /// Default: Lyra
        /// </summary>
        public string DomainName { get; set; }

        /// <summary>
        /// Preprogrammed token behaviour 
        /// </summary>
        public ContractTypes ContractType { get; set; }

        public DateTime RenewalDate { get; set; }

        // It is incremented when additinal tokens are re-issued, or the display name is changed
        public int Edition { get; set; }

        // Free text
        public string Description { get; set; }

        // Number of digits after decimal point, can be 0 - 12.
        public sbyte Precision { get; set; }

        // Can issue extra tokens?
        public bool IsFinalSupply { get; set; }


        //// the extra fee that the owner receives (in additiuona to authorization fee) when token is transacted
        //public long CustomFee { get; set; }

        //// where to send the extra fee
        //public string CustomFeeAccountId { get; set; }

        //// IFeebleBlock implementation
        //public long Fee { get; set; }

        //// IFeebleBlock implementation
        //public string FeeCode { get; set; }

        //// IFeebleBlock implementation
        //public AuthorizationFeeTypes FeeType { get; set; }

        // This one to be used for non-fungible tokens like gift cards, one-time coupons, collectibles, etc. 
        // Each token derived from this genesis is unique
        public bool IsNonFungible { get; set; }

        public NonFungibleTokenTypes NonFungibleType { get; set; }

        /// <summary>
        /// This is the public key that will be used to sign each newly created instance of non-fungible token derived from this genesis block.
        /// To simplify things it can be set to AccountId of the owner's account, so the same account's private key can be used to sign the tokens as well.
        /// But the owner might want to generate tokens from multiple accounts, so this key can be used to separate the token signing from block signing.
        /// </summary>
        public string NonFungibleKey { get; set; }

        public string Owner { get; set; }

        public string Address { get; set; }

        public string Currency { get; set; }

        public string Icon { get; set; }

        public string Image { get; set; }

        public string Custom1 { get; set; }

        public string Custom2 { get; set; }

        public string Custom3 { get; set; }



        // TO DO - add image, renewaldate
        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += Ticker + "|";
            extraData += DomainName + "|";
            extraData += ContractType + "|";
            extraData += DateTimeToString(RenewalDate) + "|";
            extraData += Edition.ToString() + "|";
            extraData += Description + "|";
            extraData += Precision.ToString() + "|";
            extraData += IsFinalSupply.ToString() + "|";
            extraData += IsNonFungible.ToString() + "|";
            extraData += NonFungibleType.ToString() + "|";
            extraData += NonFungibleKey + "|";
            extraData += Owner + "|";
            extraData += Address + "|";
            extraData += Currency + "|";
            extraData += Icon + "|";
            extraData += Image + "|";
            extraData += Custom1 + "|";
            extraData += Custom2 + "|";
            extraData += Custom3 + "|";
            return extraData;
        }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.TokenGenesis;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Ticker: {Ticker}\n";
            result += $"DomainName: {DomainName}\n";
            result += $"ContractType: {ContractType.ToString()}\n";
            result += $"RenewalDate: {DateTimeToString(RenewalDate)}\n";
            result += $"Edition: {Edition.ToString()}\n";
            result += $"Description: {Description}\n";
            result += $"Precision: {Precision.ToString()}\n";
            result += $"IsFinalSupply: {IsFinalSupply.ToString()}\n";
            result += $"IsNonFungible: {IsNonFungible.ToString()}\n";
            result += $"NonFungibleType: {NonFungibleType.ToString()}\n";
            result += $"NonFungibleKey: {NonFungibleKey}\n";
            result += $"Owner: {Owner}\n";
            result += $"Address: {Address}\n";
            result += $"Currency: {Currency}\n";
            result += $"Icon: {Icon}\n";
            result += $"Image: {Image}\n";
            result += $"Custom1: {Custom1}\n";
            result += $"Custom2: {Custom2}\n";
            result += $"Custom3: {Custom3}\n";
            return result;
        }

        public override bool IsBlockValid(Block prevBlock)
        {
            var result = base.IsBlockValid(prevBlock);
            if (!result)
                throw new ApplicationException("Base Block Validation Failed");

            if (!ValidateTokenName())
                throw new ApplicationException("Domain Name Validation Failed");

            if (Precision < 0 || Precision > 8)
                throw new ApplicationException("Precision is out of range");

            var Supply = Balances[Ticker];
            if (Supply <= 0 || Supply > 1000000000000000) // quadrillion, or 10^15, or 15 zeros
                throw new ApplicationException("Supply is out of range");

            return true;
        }


        protected virtual bool ValidateStringFields()
        {
            if (Description != null && Description.Length > MAX_STRING_LENGTH)
                throw new ApplicationException("Description too long");

            if (NonFungibleKey != null && NonFungibleKey.Length > MAX_STRING_LENGTH)
                throw new ApplicationException("Description too long");

            if (Owner != null && Owner.Length > MAX_STRING_LENGTH)
                throw new ApplicationException("Description too long");

            if (Address != null && Address.Length > MAX_STRING_LENGTH)
                throw new ApplicationException("Description too long");

            if (Currency != null && Currency.Length > MAX_STRING_LENGTH)
                throw new ApplicationException("Description too long");

            if (Icon != null && Icon.Length > MAX_STRING_LENGTH)
                throw new ApplicationException("Description too long");

            if (Image != null && Image.Length > MAX_STRING_LENGTH)
                throw new ApplicationException("Description too long");

            if (Custom1 != null && Custom1.Length > MAX_STRING_LENGTH)
                throw new ApplicationException("Description too long");

            if (Custom2 != null && Custom2.Length > MAX_STRING_LENGTH)
                throw new ApplicationException("Description too long");

            if (Custom3 != null && Custom3.Length > MAX_STRING_LENGTH)
                throw new ApplicationException("Description too long");

            return true;
        }
               

        protected virtual bool ValidateTokenName()
        {
            // domain
            if (string.IsNullOrEmpty(this.DomainName))
                throw new ApplicationException("DomainName is null or empty");

            // ticker
            if (string.IsNullOrEmpty(this.Ticker))
                throw new ApplicationException("Ticker is null or empty");

            if (this.Ticker.Length > 255)
                throw new ApplicationException("Ticker Length > 255");

            if (this.Ticker.Length < 3)
                throw new ApplicationException("Ticker Length < 3");

            char[] separator = { '.' };

            // using the method 
            String[] names = this.Ticker.Split(separator);
            if (names.Length < 2)
                throw new ApplicationException("Ticker does not contian domain name");

            if (names.Length > 2)
                throw new ApplicationException("Ticker contians more than one domain name");

            if (DomainName != names[0])
                throw new ApplicationException("Domain name mismatch");

            string TokenName = names[1];

            if (TokenName.Length < 1)
                throw new ApplicationException("Token name is too short");

            if (this.DomainName.ToLower() == "l" ||
                this.DomainName.ToLower() == "ly" ||
                this.DomainName.ToLower() == "lyr" ||
                this.DomainName.ToLower() == "lyra")
                throw new ApplicationException("Invalid Domain Name");

            if (DomainName.Length < 4)
                throw new ApplicationException("Domain Name is too short");

            if (DomainName.ToLower() == "graft")
                throw new ApplicationException("Invalid Domain Name");

            return true;
        }

    }

    //// This one to be used for non-fungible tokens like gift cards, one-time coupons, collectibles, etc. 
    //// Each token derived from this genesis is unique
    //public class NonFungibleTokenGenesisBlock: TokenGenesisBlock
    //{
    //    public string UniqueCode { get; set; }
    //    public DateTime ExpirationDate { get; set; }

    //    public override BlockTypes GetBlockType()
    //    {
    //        return BlockTypes.NonFungibleGenesis;
    //    }
    //}

    // This one can be used only once for very genesis block when the network is launched
    public class LyraTokenGenesisBlock : TokenGenesisBlock, IOpeningBlock
    {
        public AccountTypes AccountType { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.LyraTokenGenesis;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData = extraData + AccountType + "|";
            return extraData;
        }

        protected override bool ValidateTokenName()
        {
            // domain
            if (string.IsNullOrEmpty(this.DomainName))
                throw new ApplicationException("DomainName is null or empty");

            // ticker
            if (string.IsNullOrEmpty(this.Ticker))
                throw new ApplicationException("Ticker is null or empty");

            if (this.Ticker.Length > 255)
                throw new ApplicationException("Ticker Length > 255");

            if (this.Ticker.Length < 3)
                throw new ApplicationException("Ticker Length < 3");

            char[] separator = { '.' };

            // using the method 
            String[] names = this.Ticker.Split(separator);
            if (names.Length < 2)
                throw new ApplicationException("Ticker does not contian domain name");

            if (names.Length > 2)
                throw new ApplicationException("Ticker contians more than one domain name");

            if (DomainName != names[0])
                throw new ApplicationException("Domain name mismatch");

            return true;
        }
    }
}
