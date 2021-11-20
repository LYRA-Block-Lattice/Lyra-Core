
using Lyra.Core.API;
using Lyra.Data.Blocks;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Blocks
{
    /// <summary>
    /// Token burn to a user's dex broker account chain.
    /// When user receive, he/she will receive it.
    /// like normal receive, it just not change the balance.
    /// </summary>
    [BsonIgnoreExtraElements]
    public class TokenBurnBlock : TransactionBlock, IDexWallet
    {
        public string Name { get; set; }
        public string OwnerAccountId { get; set; }
        public string RelatedTx { get; set; }

        public string IntSymbol { get; set; }
        public string ExtSymbol { get; set; }
        public string ExtProvider { get; set; }
        public string ExtAddress { get; set; }

        /// <summary>
        /// always be a DEX server's account ID
        /// </summary>
        public string BurnBy { get; set; }

        /// <summary>
        /// genesisblock's hash. it will have the properties.
        /// </summary>
        public string GenesisHash { get; set; }
        
        public long BurnAmount { get; set; }

        public override bool AuthCompare(Block other)
        {
            var ob = other as TokenBurnBlock;

            return base.AuthCompare(ob) &&
                IntSymbol == ob.IntSymbol &&
                ExtSymbol == ob.ExtSymbol &&
                ExtProvider == ob.ExtProvider &&
                ExtAddress == ob.ExtAddress &&
                Name == ob.Name &&
                OwnerAccountId == ob.OwnerAccountId &&
                RelatedTx == ob.RelatedTx &&
                BurnBy == ob.BurnBy &&
                GenesisHash == ob.GenesisHash &&
                BurnAmount == ob.BurnAmount
                ;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            var plainTextBytes = Encoding.UTF8.GetBytes(Name);
            var nameEnc = Convert.ToBase64String(plainTextBytes);   // to avoid attack
            extraData += nameEnc + "|";
            extraData += OwnerAccountId + "|";
            extraData += RelatedTx + "|";
            extraData += IntSymbol + "|";
            extraData += ExtSymbol + "|";
            extraData += ExtProvider + "|";
            extraData += ExtAddress + "|";
            extraData += BurnBy + "|";
            extraData += GenesisHash + "|";
            extraData += BurnAmount.ToString() + "|";
            return extraData;
        }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.DexTokenBurn;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Name: {Name}\n";
            result += $"OwnerAccountId: {OwnerAccountId}\n";
            result += $"RelatedTx: {RelatedTx}\n";
            result += $"IntSymbol: {IntSymbol}\n";
            result += $"ExtSymbol: {ExtSymbol}\n";
            result += $"ExtProvider: {ExtProvider}\n";
            result += $"ExtAddress: {ExtAddress}\n";
            result += $"BurnBy: {BurnBy}\n";
            result += $"GenesisHash: {GenesisHash}\n";
            result += $"BurnAmount: {BurnAmount.ToBalanceDecimal()}\n";
            return result;
        }

    }

    [BsonIgnoreExtraElements]
    public class TokenWithdrawBlock : TokenBurnBlock
    {
        public string WithdrawToExtAddress { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.DexWithdrawToken;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as TokenWithdrawBlock;

            return base.AuthCompare(ob) &&
                WithdrawToExtAddress == ob.WithdrawToExtAddress
                ;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += WithdrawToExtAddress + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"WithdrawToExtAddress: {WithdrawToExtAddress}\n";
            return result;
        }
    }
}