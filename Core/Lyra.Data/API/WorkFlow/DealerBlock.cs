//using Lyra.Core.API;
//using Lyra.Core.Blocks;
//using Lyra.Data.Blocks;
//using MongoDB.Bson.Serialization.Attributes;
//using MongoDB.Bson.Serialization.Options;
//using System;
//using System.Collections.Generic;
//using System.Globalization;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace Lyra.Data.API.WorkFlow
//{
//    public interface IDealer : IProfiting
//    {
//        public string Endpoint { get; set; }
//        public Dictionary<string, long> Treasure { get; set; }
//        public string Description { get; set; }    // dao configuration record hash, in other db collection
//    }

//    [BsonIgnoreExtraElements]
//    public class DealerRecvBlock : BrokerAccountRecv, IDealer
//    {
//        // profiting
//        public ProfitingType PType { get; set; }
//        public decimal ShareRito { get; set; }
//        public int Seats { get; set; }

//        // dealer
//        public string Endpoint { get; set; }

//        public string Description { get; set; }

//        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
//        public Dictionary<string, long> Treasure { get; set; }

//        protected override BlockTypes GetBlockType()
//        {
//            return BlockTypes.DealerRecv;
//        }

//        public override bool AuthCompare(Block other)
//        {
//            var ob = other as DealerRecvBlock;

//            return base.AuthCompare(ob) &&
//                PType == ob.PType &&
//                ShareRito == ob.ShareRito &&
//                Seats == ob.Seats &&
//                Endpoint == ob.Endpoint &&
//                Description == ob.Description &&
//                CompareDict(Treasure, ob.Treasure)
//                ;
//        }

//        protected override string GetExtraData()
//        {
//            string extraData = base.GetExtraData();

//            extraData += PType.ToString() + "|";
//            extraData += ShareRito.ToBalanceLong().ToString() + "|";
//            extraData += Seats.ToString() + "|";
//            extraData += Endpoint + "|";

//            extraData += DictToStr(Treasure) + "|";
//            extraData += Description + "|";
//            return extraData;
//        }

//        public override string Print()
//        {
//            string result = base.Print();

//            result += $"Profiting Type: {PType}\n";
//            result += $"Share Rito: {ShareRito}\n";
//            result += $"Seats: {Seats}\n";
//            result += $"Endpoint URL: {Endpoint}\n";

//            result += $"Description: {Description}";
//            result += $"Treasure: {DictToStr(Treasure)}\n";
//            return result;
//        }
//    }

//    [BsonIgnoreExtraElements]
//    public class DealerSendBlock : BrokerAccountSend, IDealer
//    {
//        // profiting
//        public ProfitingType PType { get; set; }
//        public decimal ShareRito { get; set; }
//        public int Seats { get; set; }

//        // dealer
//        public string Endpoint { get; set; }

//        public string Description { get; set; }

//        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
//        public Dictionary<string, long> Treasure { get; set; }

//        protected override BlockTypes GetBlockType()
//        {
//            return BlockTypes.DealerSend;
//        }

//        public override bool AuthCompare(Block other)
//        {
//            var ob = other as DealerRecvBlock;

//            return base.AuthCompare(ob) &&
//                PType == ob.PType &&
//                ShareRito == ob.ShareRito &&
//                Seats == ob.Seats &&
//                Endpoint == ob.Endpoint &&
//                Description == ob.Description &&
//                CompareDict(Treasure, ob.Treasure)
//                ;
//        }

//        protected override string GetExtraData()
//        {
//            string extraData = base.GetExtraData();

//            extraData += PType.ToString() + "|";
//            extraData += ShareRito.ToBalanceLong().ToString() + "|";
//            extraData += Seats.ToString() + "|";
//            extraData += Endpoint + "|";

//            extraData += DictToStr(Treasure) + "|";
//            extraData += Description + "|";
//            return extraData;
//        }

//        public override string Print()
//        {
//            string result = base.Print();

//            result += $"Profiting Type: {PType}\n";
//            result += $"Share Rito: {ShareRito}\n";
//            result += $"Seats: {Seats}\n";
//            result += $"Endpoint URL: {Endpoint}\n";

//            result += $"Description: {Description}";
//            result += $"Treasure: {DictToStr(Treasure)}\n";
//            return result;
//        }
//    }


//    [BsonIgnoreExtraElements]
//    public class DealerGenesisBlock : DaoRecvBlock, IOpeningBlock
//    {
//        public AccountTypes AccountType { get; set; }

//        protected override BlockTypes GetBlockType()
//        {
//            return BlockTypes.DealerGenesis;
//        }

//        public override bool AuthCompare(Block other)
//        {
//            var ob = other as DaoGenesisBlock;

//            return base.AuthCompare(ob) &&
//                AccountType == ob.AccountType
//                ;
//        }

//        protected override string GetExtraData()
//        {
//            string extraData = base.GetExtraData();
//            extraData += AccountType + "|";
//            return extraData;
//        }

//        public override string Print()
//        {
//            string result = base.Print();
//            result += $"AccountType: {AccountType}\n";
//            return result;
//        }
//    }
//}
