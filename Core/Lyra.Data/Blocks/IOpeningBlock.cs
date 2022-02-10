namespace Lyra.Core.Blocks
{
    public interface IOpeningBlock
    {
        AccountTypes AccountType { get; set; }
    }

    //// Account opening block - each account starts from such block
    //public class OpenBlock : ReceiveTransferBlock
    //{

    //    public AccountTypes AccountType { get; set; }

    //    public override string GetExtraData()
    //    {
    //        string extraData = base.GetExtraData();
    //        extraData = extraData + AccountType.ToString();
    //        return extraData;
    //    }

    //    protected override BlockTypes GetBlockType()
    //    {
    //        return BlockTypes.OpenWithReceiveTransfer;
    //    }
    //}
}
