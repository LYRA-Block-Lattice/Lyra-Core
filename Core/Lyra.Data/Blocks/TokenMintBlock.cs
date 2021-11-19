
using Lyra.Core.API;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace Lyra.Core.Blocks
{
    /// <summary>
    /// Token mint to a user's dex broker account chain.
    /// When user receive, he/she will receive it.
    /// </summary>
    [BsonIgnoreExtraElements]
    public class TokenMintBlock : TransactionBlock
    {
        /// <summary>
        /// always be a DEX server's account ID
        /// </summary>
        public string MintBy { get; set; }

        /// <summary>
        /// genesisblock's hash. it will have the properties.
        /// </summary>
        public string GenesisHash { get; set; }
        
        public long MintAmount { get; set; }

        public override bool AuthCompare(Block other)
        {
            var ob = other as TokenMintBlock;

            return base.AuthCompare(ob) &&
                MintBy == ob.MintBy &&
                GenesisHash == ob.GenesisHash &&
                MintAmount == ob.MintAmount
                ;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += MintBy + "|";
            extraData += GenesisHash + "|";
            extraData += MintAmount.ToString() + "|";
            return extraData;
        }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.DexTokenMint;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"MintBy: {MintBy}\n";
            result += $"GenesisHash: {GenesisHash}\n";
            result += $"MintAmount: {MintAmount.ToBalanceDecimal()}\n";
            return result;
        }

    }
}