using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.Blocks;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API.WorkFlow
{
    public interface IDao : IProfiting
    {
        public decimal SellerFeeRatio { get; set; }
        public decimal BuyerFeeRatio { get; set; }

        /// <summary>
        /// percentage, 0% ~ 1000%, convert to 0 ~ 10 in decimal
        /// </summary>
        public int SellerPar { get; set; }

        /// <summary>
        /// percentage, 0% ~ 1000%, convert to 0 ~ 10 in decimal
        /// </summary>
        public int BuyerPar { get; set; }
        public SortedDictionary<string, long> Treasure { get; set; }
        public string Description { get; set; }    // dao configuration record hash, in other db collection
    }

    [BsonIgnoreExtraElements]
    public class DaoRecvBlock : BrokerAccountRecv, IDao
    {
        // profiting
        public ProfitingType PType { get; set; }
        public decimal ShareRito { get; set; }
        public int Seats { get; set; }

        // dao
        public decimal SellerFeeRatio { get; set; }
        public decimal BuyerFeeRatio { get; set; }
        public int SellerPar { get; set; }
        public int BuyerPar { get; set; }
        public string Description { get; set; }

        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public SortedDictionary<string, long> Treasure { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.OrgnizationRecv;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as DaoRecvBlock;

            if (Version > 7)
            {
                return base.AuthCompare(ob) &&
                    PType == ob.PType &&
                    ShareRito == ob.ShareRito &&
                    Seats == ob.Seats &&
                    SellerFeeRatio == ob.SellerFeeRatio &&
                    BuyerFeeRatio == ob.BuyerFeeRatio &&
                    SellerPar == ob.SellerPar &&
                    BuyerPar == ob.BuyerPar &&
                    Description == ob.Description &&
                    CompareDict(Treasure, ob.Treasure)
                    ;
            }
            else if (Version > 6)
                return base.AuthCompare(ob) &&
                    PType == ob.PType &&
                    ShareRito == ob.ShareRito &&
                    Seats == ob.Seats &&
                    SellerPar == ob.SellerPar &&
                    BuyerPar == ob.BuyerPar &&
                    Description == ob.Description &&
                    CompareDict(Treasure, ob.Treasure)
                    ;
            else
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

            if (Version > 7)
            {
                extraData += PType.ToString() + "|";
                extraData += ShareRito.ToBalanceLong().ToString() + "|";
                extraData += Seats.ToString() + "|";
                extraData += SellerFeeRatio.ToBalanceLong().ToString() + "|";
                extraData += BuyerFeeRatio.ToBalanceLong().ToString() + "|";
            }
            else if (Version > 6)
            {
                extraData += PType.ToString() + "|";
                extraData += ShareRito.ToBalanceLong().ToString() + "|";
                extraData += Seats.ToString() + "|";
            }
            extraData += $"{SellerPar}|";
            extraData += $"{BuyerPar}|";
            extraData += DictToStr(Treasure) + "|";
            extraData += Description + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            if (Version > 7)
            {
                result += $"Profiting Type: {PType}\n";
                result += $"Share Rito: {ShareRito}\n";
                result += $"Seats: {Seats}\n";
                result += $"Seller Fee Ratio: {SellerFeeRatio}\n";
                result += $"Buyer Fee Ratio: {BuyerFeeRatio}\n";
            }
            else if (Version > 6)
            {
                result += $"Profiting Type: {PType}\n";
                result += $"Share Rito: {ShareRito}\n";
                result += $"Seats: {Seats}\n";
            }
            result += $"SellerCollateralPercentage: {SellerPar}\n";
            result += $"ByerCollateralPercentage: {BuyerPar}\n";
            result += $"Description: {Description}\n";
            result += $"Treasure: {DictToStr(Treasure)}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class DaoSendBlock : BrokerAccountSend, IDao
    {
        // profiting
        public ProfitingType PType { get; set; }
        public decimal ShareRito { get; set; }
        public int Seats { get; set; }

        // dao
        public decimal SellerFeeRatio { get; set; }
        public decimal BuyerFeeRatio { get; set; }
        public int SellerPar { get; set; }
        public int BuyerPar { get; set; }
        public string Description { get; set; }

        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public SortedDictionary<string, long> Treasure { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.OrgnizationSend;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as DaoSendBlock;

            if (Version > 7)
            {
                return base.AuthCompare(ob) &&
                    PType == ob.PType &&
                    ShareRito == ob.ShareRito &&
                    Seats == ob.Seats &&
                    SellerFeeRatio == ob.SellerFeeRatio &&
                    BuyerFeeRatio == ob.BuyerFeeRatio &&
                    SellerPar == ob.SellerPar &&
                    BuyerPar == ob.BuyerPar &&
                    Description == ob.Description &&
                    CompareDict(Treasure, ob.Treasure)
                    ;
            }
            else if (Version > 6)
            {
                return base.AuthCompare(ob) &&
                    PType == ob.PType &&
                    ShareRito == ob.ShareRito &&
                    Seats == ob.Seats &&
                    SellerPar == ob.SellerPar &&
                    BuyerPar == ob.BuyerPar &&
                    Description == ob.Description &&
                    CompareDict(Treasure, ob.Treasure)
                    ;
            }
            else
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
            if (Version > 7)
            {
                extraData += PType.ToString() + "|";
                extraData += ShareRito.ToBalanceLong().ToString() + "|";
                extraData += Seats.ToString() + "|";
                extraData += SellerFeeRatio.ToBalanceLong().ToString() + "|";
                extraData += BuyerFeeRatio.ToBalanceLong().ToString() + "|";
            }
            else if (Version > 6)
            {
                extraData += PType.ToString() + "|";
                extraData += ShareRito.ToBalanceLong().ToString() + "|";
                extraData += Seats.ToString() + "|";
            }
            extraData += $"{SellerPar}|";
            extraData += $"{BuyerPar}|";
            extraData += DictToStr(Treasure) + "|";
            extraData += Description + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            if (Version > 7)
            {
                result += $"Profiting Type: {PType}\n";
                result += $"Share Rito: {ShareRito}\n";
                result += $"Seats: {Seats}\n";
                result += $"Seller Fee Ratio: {SellerFeeRatio}\n";
                result += $"Buyer Fee Ratio: {BuyerFeeRatio}\n";
            }
            else if (Version > 6)
            {
                result += $"Profiting Type: {PType}\n";
                result += $"Share Rito: {ShareRito}\n";
                result += $"Seats: {Seats}\n";
            }
            result += $"SellerCollateralPercentage: {SellerPar}\n";
            result += $"ByerCollateralPercentage: {BuyerPar}\n";
            result += $"Description: {Description}\n";
            result += $"Treasure: {DictToStr(Treasure)}\n";
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
