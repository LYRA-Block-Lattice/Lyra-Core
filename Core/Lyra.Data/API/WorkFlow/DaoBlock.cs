using Lyra.Core.Blocks;
using Lyra.Data.Blocks;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API.WorkFlow
{
    public interface IDao : IBrokerAccount
    {
        // percentage, 0 ~ 1000%
        public int SellerCollateralPercentage { get; set; }
        public int ByerCollateralPercentage { get; set; }
        public Dictionary<string, long> Treasure { get; set; }
        public string MetaHash { get; set; }    // dao configuration record hash, in other db collection
    }

    [BsonIgnoreExtraElements]
    public class DaoBlock : BrokerAccountRecv, IDao
    {
        public int SellerCollateralPercentage { get; set; }
        public int ByerCollateralPercentage { get; set; }
        public string MetaHash { get; set; }

        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<string, long> Treasure { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.Orgnization;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as DaoBlock;

            return base.AuthCompare(ob) &&
                SellerCollateralPercentage == ob.SellerCollateralPercentage &&
                ByerCollateralPercentage == ob.ByerCollateralPercentage &&
                MetaHash == ob.MetaHash &&
                CompareDict(Treasure, ob.Treasure)
                ;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += $"{SellerCollateralPercentage}|";
            extraData += $"{ByerCollateralPercentage}|";
            extraData += DictToStr(Treasure) + "|";
            extraData += MetaHash + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"SellerCollateralPercentage: {MetaHash}\n";
            result += $"ByerCollateralPercentage: {MetaHash}\n";
            result += $"Treasure: {DictToStr(Treasure)}\n";
            result += $"MetaHash: {MetaHash}\n";
            return result;
        }
    }


    [BsonIgnoreExtraElements]
    public class DaoGenesisBlock : DaoBlock, IOpeningBlock
    {
        public AccountTypes AccountType { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.OrgnizationGenesis;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as DaoGenesisBlock;

            return base.AuthCompare(ob) &&
                AccountType == ob.AccountType
                ;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += AccountType + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"AccountType: {AccountType}\n";
            return result;
        }
    }
}
