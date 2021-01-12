using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Blocks
{
    public interface IPool
    {
        Dictionary<string, long> Shares { get; set; }
    }
    /// <summary>
    /// 
    /// </summary>
    [BsonIgnoreExtraElements]
    public class PoolDepositBlock : ReceiveTransferBlock, IPool
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.PoolDeposit;
        }

        // AccountId -> Share
        // Initial pool token is 1M
        // make sure sum(share) is always 1M
        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<string, long> Shares { get; set; }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += DictToStr(Shares) + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Shares: {DictToStr(Shares)}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class PoolWithdrawRequestBlock : ReceiveTransferBlock, IPool
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.PoolWithdrawReq;
        }

        // AccountId -> Share
        // Initial pool token is 1M
        // make sure sum(share) is always 1M
        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<string, long> Shares { get; set; }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += DictToStr(Shares) + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Shares: {DictToStr(Shares)}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class PoolWithdrawBlock : SendTransferBlock, IPool
    {
        /// <summary>
        /// the hash of requested SendTransferBlock and ReceiveTransferBlock onside pool factory's chain
        /// RelatedTx -> RecvBlock (SourceHash) -> SendTransferBlcok
        /// on pool action to one send/recv combine
        /// </summary>
        public string RelatedTx { get; set; }
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.PoolWithdraw;
        }

        // AccountId -> Share
        // Initial pool token is 1M
        // make sure sum(share) is always 1M
        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<string, long> Shares { get; set; }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += DictToStr(Shares) + "|";
            extraData += RelatedTx + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"RelatedTx: {RelatedTx}\n";
            result += $"Shares: {DictToStr(Shares)}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class PoolSwapInBlock : ReceiveTransferBlock, IPool
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.PoolSwapIn;
        }

        // AccountId -> Share
        // Initial pool token is 1M
        // make sure sum(share) is always 1M
        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<string, long> Shares { get; set; }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += DictToStr(Shares) + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Shares: {DictToStr(Shares)}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class PoolSwapOutBlock : SendTransferBlock, IPool
    {
        public string RelatedTx { get; set; }
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.PoolSwapOut;
        }

        // AccountId -> Share
        // Initial pool token is 1M
        // make sure sum(share) is always 1M
        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<string, long> Shares { get; set; }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += DictToStr(Shares) + "|";
            extraData += RelatedTx + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"RelatedTx: {RelatedTx}\n";
            result += $"Shares: {DictToStr(Shares)}\n";
            return result;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    [BsonIgnoreExtraElements]
    public class PoolGenesisBlock : ReceiveTransferBlock, IOpeningBlock
    {
        public string Token0 { get; set; }
        public string Token1 { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.PoolGenesis;
        }

        public AccountTypes AccountType { get; set; }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += Token0 + "|";
            extraData += Token1 + "|";
            extraData += AccountType + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Token0: {Token0}\n";
            result += $"Token1: {Token1}\n";
            result += $"AccountType: {AccountType}\n";
            return result;
        }
    }
}
