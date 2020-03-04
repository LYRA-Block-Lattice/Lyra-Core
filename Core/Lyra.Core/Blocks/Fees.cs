using MongoDB.Bson.Serialization.Attributes;

namespace Lyra.Core.Blocks.Fees
{
    [BsonIgnoreExtraElements]
    public class ReceiveFeeBlock : ReceiveTransferBlock
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.ReceiveFee;
        }
    }

    [BsonIgnoreExtraElements]
    public class OpenWithReceiveFeeBlock : OpenWithReceiveTransferBlock//ReceiveFeeBlock, IOpeningBlock
    {
//        public AccountTypes AccountType { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.OpenAccountWithReceiveFee;
        }

        //public override string GetExtraData()
        //{
        //    string extraData = base.GetExtraData();
        //    extraData = extraData + AccountType;
        //    return extraData;
        //}
    }

}
