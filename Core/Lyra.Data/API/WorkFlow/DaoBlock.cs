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
        public int SellerPar { get; set; }
        public int BuyerPar { get; set; }
        public Dictionary<string, long> Treasure { get; set; }
        public string Description { get; set; }    // dao configuration record hash, in other db collection
    }

    [BsonIgnoreExtraElements]
    public class DaoRecvBlock : BrokerAccountRecv, IDao
    {
        public int SellerPar { get; set; }
        public int BuyerPar { get; set; }
        public string Description { get; set; }

        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<string, long> Treasure { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.OrgnizationRecv;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as DaoRecvBlock;

            return base.AuthCompare(ob) &&
                SellerPar == ob.SellerPar &&
                BuyerPar == ob.BuyerPar &&
                Description == ob.Description &&
                CompareDict(Treasure, ob.Treasure)
                ;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += $"{SellerPar}|";
            extraData += $"{BuyerPar}|";
            extraData += DictToStr(Treasure) + "|";
            extraData += Description + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"SellerCollateralPercentage: {Description}\n";
            result += $"ByerCollateralPercentage: {Description}\n";
            result += $"Treasure: {DictToStr(Treasure)}\n";
            result += $"MetaHash: {Description}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class DaoSendBlock : BrokerAccountSend, IDao
    {
        public int SellerPar { get; set; }
        public int BuyerPar { get; set; }
        public string Description { get; set; }

        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<string, long> Treasure { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.OrgnizationSend;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as DaoSendBlock;

            return base.AuthCompare(ob) &&
                SellerPar == ob.SellerPar &&
                BuyerPar == ob.BuyerPar &&
                Description == ob.Description &&
                CompareDict(Treasure, ob.Treasure)
                ;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += $"{SellerPar}|";
            extraData += $"{BuyerPar}|";
            extraData += DictToStr(Treasure) + "|";
            extraData += Description + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"SellerCollateralPercentage: {Description}\n";
            result += $"ByerCollateralPercentage: {Description}\n";
            result += $"Treasure: {DictToStr(Treasure)}\n";
            result += $"MetaHash: {Description}\n";
            return result;
        }
    }


    [BsonIgnoreExtraElements]
    public class DaoGenesisBlock : DaoRecvBlock, IOpeningBlock
    {
        public AccountTypes AccountType { get; set; }

        protected override BlockTypes GetBlockType()
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
