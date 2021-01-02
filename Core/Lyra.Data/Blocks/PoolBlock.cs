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
    }

    [BsonIgnoreExtraElements]
    public class PoolWithdrawBlock : SendTransferBlock, IPool
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.PoolWithdraw;
        }

        // AccountId -> Share
        // Initial pool token is 1M
        // make sure sum(share) is always 1M
        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<string, long> Shares { get; set; }
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
    }

    [BsonIgnoreExtraElements]
    public class PoolSwapOutBlock : SendTransferBlock, IPool
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.PoolSwapOut;
        }

        // AccountId -> Share
        // Initial pool token is 1M
        // make sure sum(share) is always 1M
        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<string, long> Shares { get; set; }
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
    }
}
