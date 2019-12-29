namespace Lyra.Core.Blocks
{
    public class ExchangingBlock : SendTransferBlock
    {
        public const decimal FEE = 0.0m;

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.ExchangingTransfer;
        }
    }
}
