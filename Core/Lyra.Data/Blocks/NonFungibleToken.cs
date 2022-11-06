using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace Lyra.Core.Blocks
{
    // Gift token 

    // Container for Non-fungible discount token that is linked to specific discount code at Shopify
    // Discount token is "unlocked" gift token so it has a fixed amount ("denomination") which is linked to the Shopify discount
    // "Unlocked" means it is non-transferrable, so it cannot moved between account, it can only be redeemed
    // Gift token is "locked" so it can be securely sent to another owner (change ownership).
    // once the new owner decides to redeem the gift token, it should be "unlocked", i.e. transformed to discount token with the redemption code
    // Discount token can only be sent once from it's creator to the customer, and then it can be redeemed by the creator by sending sendredeem block.
    // SendConfirmRedeem block will signal and enable removal of the used discount token from the customer's account (customer account should create ReceiveConfirmRedeem block) 
    //public class LoyaltyDiscountToken: NonFungibleToken
    //{
    //    /// <summary>
    //    /// This is encrypted redemption code.
    //    /// It is encrypted by Diffie Helman using sender's private key and recipient's public key
    //    /// </summary>
    //    public string RedemptionCode { get; set; }

    //    // This is the actual date which is set at time of token generation by the sender
    //    // according to the value in the genesis block
    //    public DateTime ExpirationDate { get; set; }

    //    // should be overriden in specific instance to get the correct hash claculated from the entire block data 
    //    protected override string GetExtraData()
    //    {
    //        return RedemptionCode + ExpirationDate.ToString("yyyy-MM-dd");
    //    }
    //}

    public enum NonFungibleTokenTypes : ushort
    {
        NotSet = 0,

        // LoyalShopper Shopify discount code 
        LoyaltyDiscount = 1,

        // Lyra or Custom Collectible NFT
        Collectible = 2,

        // external NFT
        //ERC1155 = 3,  
    }


    public class NonFungibleToken: SignableObject
    {
        // Token ticker - link to the token's genesis block
        // normaly nft full ticker, "nft/0000-000..."
        public string TokenCode { get; set; }

        ///// <summary>
        //// Unique token identifier.
        ///  Must be set by the token originator.
        //// It should never be changed by future token recipients or senders.
        ///// </summary>
        public string SerialNumber { get; set; }

        // The token's currency amount.
        // It can't be changed after the token is generated.
        // The currency and precision are defined by the genesis block.
        public decimal Denomination { get; set; }

        /// <summary>
        /// This is encrypted redemption code.
        /// It is encrypted by Diffie Helman using sender's private key and recipient's public key
        /// </summary>
        public string? RedemptionCode { get; set; }

        // This is the actual date which is set at time of token generation by the sender
        // according to the value in the genesis block
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime ExpirationDate { get; set; }

        public override string GetHashInput()
        {
            return TokenCode+ "|" +
                  SerialNumber + "|" +
                   JsonConvert.SerializeObject(Denomination) + "|" +
                   RedemptionCode + "|" +
                   DateTimeToString(ExpirationDate) + "|" +
                   GetExtraData();
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"TokenCode: {TokenCode}\n";
            result += $"SerialNumber: {SerialNumber}\n";
            result += $"Denomination: {JsonConvert.SerializeObject(Denomination)}\n";
            result += $"RedemptionCode: {RedemptionCode}\n";
            result += $"ExpirationDate: {DateTimeToString(ExpirationDate)}\n";
            return result;
        }


        protected override string GetExtraData() { return string.Empty; }

    }
}
