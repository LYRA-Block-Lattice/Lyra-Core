using Lyra.Core.Blocks.Transactions;
using Lyra.Core.Protos;

namespace Lyra.Core.Blocks.Fees
{
    public class ReceiveFeeBlock : ReceiveTransferBlock
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.ReceiveFee;
        }
    }

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
