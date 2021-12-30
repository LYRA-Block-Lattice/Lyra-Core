using Lyra.Core.API;
using Lyra.Data.Blocks;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Blocks
{
    public interface IOrgnization
    {
        Dictionary<string, long> Shares { get; set; }
        public string RelatedTx { get; set; }
    }
    /// <summary>
    /// 
    /// </summary>
    [BsonIgnoreExtraElements]
    public class OrgnizationBlock : ReceiveTransferBlock, IOrgnization
    {
        public string RelatedTx { get; set; }
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.DaoOrgnization;
        }

        // AccountId -> Share
        // Initial pool token is 1M
        // make sure sum(share) is always 1M
        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<string, long> Shares { get; set; }

        public override bool AuthCompare(Block other)
        {
            var ob = other as PoolDepositBlock;

            return base.AuthCompare(ob) &&
                CompareShares(ob.Shares) &&
                RelatedTx == ob.RelatedTx;
        }

        private bool CompareShares(Dictionary<string, long> otherShares)
        {
            if (Shares == null && otherShares == null)
                return true;

            if (Shares.Count != otherShares.Count)
                return false;

            foreach(var kvp in Shares)
            {
                if (!otherShares.ContainsKey(kvp.Key))
                    return false;

                if (otherShares[kvp.Key] != kvp.Value)
                    return false;
            }

            return true;
        }

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


}
