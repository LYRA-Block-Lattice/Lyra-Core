
using Lyra.Core.API;
using Lyra.Data.Blocks;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Blocks
{
    /// <summary>
    /// Token mint to a user's dex broker account chain.
    /// When user receive, he/she will receive it.
    /// </summary>
    [BsonIgnoreExtraElements]
    public class FiatPrintBlock : TransactionBlock, IFiatWallet
    {
        public string Name { get; set; }
        public string OwnerAccountId { get; set; }
        public string RelatedTx { get; set; }

        public string GenesisHash { get; set; }
        public string ExtSymbol { get; set; }
       
        public long MintAmount { get; set; }

        public override bool AuthCompare(Block other)
        {
            var ob = other as FiatPrintBlock;

            return base.AuthCompare(ob) &&
                ExtSymbol == ob.ExtSymbol &&
                Name == ob.Name &&
                OwnerAccountId == ob.OwnerAccountId &&
                RelatedTx == ob.RelatedTx &&
                GenesisHash == ob.GenesisHash &&
                MintAmount == ob.MintAmount 
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
            extraData += ExtSymbol + "|";
            extraData += GenesisHash + "|";
            extraData += MintAmount.ToString() + "|";
            return extraData;
        }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.FiatTokenPrint;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Name: {Name}\n";
            result += $"OwnerAccountId: {OwnerAccountId}\n";
            result += $"RelatedTx: {RelatedTx}\n";
            result += $"ExtSymbol: {ExtSymbol}\n";
            result += $"GenesisHash: {GenesisHash}\n";
            result += $"MintAmount: {MintAmount.ToBalanceDecimal()}\n";
            return result;
        }

    }
}