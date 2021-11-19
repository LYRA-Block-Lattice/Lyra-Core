
using Lyra.Core.API;
using Lyra.Data.Blocks;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace Lyra.Core.Blocks
{
    [BsonIgnoreExtraElements]
    public class ReceiveNodeProfitBlock : ProfitingBlock
    {        
        public long ServiceBlockStartHeight { get; set; }
        public long ServiceBlockEndHeight { get; set; }
        public decimal AuthorizerFee { get; set; }

        public override bool AuthCompare(Block other)
        {
            var ob = other as ReceiveNodeProfitBlock;

            return base.AuthCompare(ob) &&
                ServiceBlockStartHeight == ob.ServiceBlockStartHeight &&
                ServiceBlockEndHeight == ob.ServiceBlockEndHeight &&
                AuthorizerFee == ob.AuthorizerFee
                ;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += AuthorizerFee.ToString("0.########") + "|";
            extraData += $"{ServiceBlockStartHeight}|";
            extraData += $"{ServiceBlockEndHeight}|";
            return extraData;
        }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.ReceiveNodeProfit;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"ServiceBlock Start Height: {ServiceBlockStartHeight}\n";
            result += $"ServiceBlock End Height: {ServiceBlockEndHeight}\n";
            result += $"Authorizer Fee: {AuthorizerFee}\n";
            return result;
        }
    }
}
