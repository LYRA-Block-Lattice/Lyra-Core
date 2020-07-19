
using Lyra.Core.API;
using MongoDB.Bson.Serialization.Attributes;

namespace Lyra.Core.Blocks
{
    [BsonIgnoreExtraElements]
    public class ReceiveAuthorizerFeeBlock : ReceiveTransferBlock
    {        
        public long ServiceBlockHeight { get; set; }
        public string ToAccountId { get; set; }
        public long AuthorizerFee { get; set; }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData = extraData + AuthorizerFee + "|";
            extraData += ToAccountId + "|";
            extraData += $"{ServiceBlockHeight}|";
            return extraData;
        }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.ReceiveAuthorizerFee;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"ServiceBlockHeight: {ServiceBlockHeight}";
            result += $"ToAccountId: {ToAccountId}\n";
            result += $"AuthorizerFee: {AuthorizerFee}\n";
            return result;
        }
    }
}
