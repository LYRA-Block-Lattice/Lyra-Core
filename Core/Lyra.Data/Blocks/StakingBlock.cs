using Lyra.Core.API;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Blocks
{
    public interface IStaking
    {
        public bool IsStakingBlock { get; }
        Dictionary<string, long> Stakings { get; set; }
    }
    /// <summary>
    /// 
    /// </summary>
    [BsonIgnoreExtraElements]
    public class StakingDepoistBlock : ReceiveTransferBlock, IStaking
    {
        public bool IsStakingBlock => true;
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.StakingDeposit;
        }

        // AccountId -> Staking Balance
        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<string, long> Stakings { get; set; }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += DictToStr(Stakings) + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Shares: {DictToStr(Stakings)}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class StakingWithdrawBlock : SendTransferBlock, IStaking
    {
        public bool IsStakingBlock => true;

        /// <summary>
        /// the hash of requested SendTransferBlock and ReceiveTransferBlock onside pool factory's chain
        /// RelatedTx -> RecvBlock (SourceHash) -> SendTransferBlcok
        /// on pool action to one send/recv combine
        /// </summary>
        public string RelatedTx { get; set; }
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.StakingWithdraw;
        }

        // AccountId -> Share
        // Initial pool token is 1M
        // make sure sum(share) is always 1M
        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<string, long> Stakings { get; set; }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += DictToStr(Stakings) + "|";
            extraData += RelatedTx + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"RelatedTx: {RelatedTx}\n";
            result += $"Shares: {DictToStr(Stakings)}\n";
            return result;
        }
    }

 
}
