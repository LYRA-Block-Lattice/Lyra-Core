
using Lyra.Core.API;
using MongoDB.Bson.Serialization.Attributes;

namespace Lyra.Core.Blocks
{
    [BsonIgnoreExtraElements]
    public class ReceiveAuthorizerFeeBlock : ReceiveTransferBlock
    {        
        public long ServiceBlockStartHeight { get; set; }
        public long ServiceBlockEndHeight { get; set; }
        public string ToAccountId { get; set; }
        public decimal AuthorizerFee { get; set; }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData = extraData + AuthorizerFee + "|";
            extraData += ToAccountId + "|";
            extraData += $"{ServiceBlockStartHeight}|";
            extraData += $"{ServiceBlockEndHeight}|";
            return extraData;
        }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.ReceiveAuthorizerFee;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"ServiceBlock Start Height: {ServiceBlockStartHeight}";
            result += $"ServiceBlock End Height: {ServiceBlockEndHeight}";
            result += $"To AccountId: {ToAccountId}\n";
            result += $"Authorizer Fee: {AuthorizerFee}\n";
            return result;
        }
    }
}
