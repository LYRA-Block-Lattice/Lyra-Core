
using Lyra.Core.API;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace Lyra.Core.Blocks
{
    /// <summary>
    /// Token burn to a user's dex broker account chain.
    /// When user receive, he/she will receive it.
    /// like normal receive, it just not change the balance.
    /// </summary>
    [BsonIgnoreExtraElements]
    public class TokenBurnBlock : TransactionBlock
    {
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
                BurnBy == ob.BurnBy &&
                GenesisHash == ob.GenesisHash &&
                BurnAmount == ob.BurnAmount
                ;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
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
            result += $"BurnBy: {BurnBy}\n";
            result += $"GenesisHash: {GenesisHash}\n";
            result += $"MintAmount: {BurnAmount.ToBalanceDecimal()}\n";
            return result;
        }

    }
}