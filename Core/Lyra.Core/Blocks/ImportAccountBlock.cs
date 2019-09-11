using System;
namespace Lyra.Core.Blocks.Transactions
{
    public class ImportAccountBlock : ReceiveTransferBlock
    {
        public string ImportedAccountId { get; set; }
        public string ImportedAccountSignature { get; set; }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData = extraData + ImportedAccountId + "|";
            extraData = extraData + ImportedAccountSignature + "|";
            return extraData;
        }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.ImportAccount;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"ImportedAccountId: {ImportedAccountId}\n";
            result += $"ImportedAccountSignature: {ImportedAccountSignature}\n";
            return result;
        }
    }

    public class OpenAccountWithImportBlock : ImportAccountBlock, IOpeningBlock
    {
        public AccountTypes AccountType { get; set; }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData = extraData + AccountType + "|";
            return extraData;
        }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.OpenAccountWithImport;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"AccountType: {AccountType}\n";
            return result;
        }
    }

}
