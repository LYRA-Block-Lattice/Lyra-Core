using Lyra.Data.Crypto;
using MongoDB.Bson.Serialization.Attributes;

namespace Lyra.Core.Blocks
{
    [BsonIgnoreExtraElements]
    public class ImportAccountBlock : ReceiveTransferBlock
    {
        public string ImportedAccountId { get; set; }
        public string ImportedAccountSignature { get; set; }

        // the has hof the last block of the imported account; 
        // all the balances from this block are transferred into THIS account.
        // can't use Source for this purposed because Source must be unique, but last block can be a send to THIS account so receive will raise duplicate Source
        public string ImportedLastBlockHash { get; set; }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData = extraData + ImportedAccountId + "|";
            extraData = extraData + ImportedLastBlockHash + "|";
            return extraData;
        }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.ImportAccount;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"ImportedAccountId: {ImportedAccountId}\n";
            result += $"ImportedAccountSignature: {ImportedAccountSignature}\n";
            result += $"ImportedLastBlockHash: {ImportedLastBlockHash}\n";
            return result;
        }

        public override bool VerifySignature(string PublicKey)
        {
            if (!base.VerifySignature(PublicKey))
                return false;

           return Signatures.VerifyAccountSignature(Hash, ImportedAccountId, ImportedAccountSignature);
        }
    }

    [BsonIgnoreExtraElements]
    public class OpenAccountWithImportBlock : ImportAccountBlock, IOpeningBlock
    {
        public AccountTypes AccountType { get; set; }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData = extraData + AccountType + "|";
            return extraData;
        }

        protected override BlockTypes GetBlockType()
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
