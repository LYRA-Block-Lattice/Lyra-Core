using Lyra.Core.Blocks;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Data.Blocks
{
    public interface IBrokerAccount
    {
        public string Name { get; set; }
        public string OwnerAccountId { get; set; }
        public string RelatedTx { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class BrokerAccountRecv : ReceiveTransferBlock, IBrokerAccount, IOpeningBlock
    {
        // user specified string, less thant 32 char
        public string Name { get; set; }
        public AccountTypes AccountType { get; set; }
        public string OwnerAccountId { get; set; }
        public string RelatedTx { get; set; }

        public override BlockTypes GetBlockType()
        {
            throw new NotImplementedException();
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            var plainTextBytes = Encoding.UTF8.GetBytes(Name);
            var nameEnc = Convert.ToBase64String(plainTextBytes);   // to avoid attack
            extraData += nameEnc + "|";
            extraData += OwnerAccountId + "|";
            if (RelatedTx != null)
                extraData += RelatedTx + "|";       // for compatible
            extraData += AccountType + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Name: {Name}\n";
            result += $"OwnerAccountId: {OwnerAccountId}\n";
            result += $"RelatedTx: {RelatedTx}\n";
            result += $"AccountType: {AccountType}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class BrokerAccountSend : SendTransferBlock, IBrokerAccount, IOpeningBlock
    {
        public AccountTypes AccountType { get; set; }
        public string OwnerAccountId { get; set; }
        public string RelatedTx { get; set; }

        // user specified string, less thant 32 char
        public string Name { get; set; }

        public override BlockTypes GetBlockType()
        {
            throw new NotImplementedException();
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            var plainTextBytes = Encoding.UTF8.GetBytes(Name);
            var nameEnc = Convert.ToBase64String(plainTextBytes);   // to avoid attack
            extraData += nameEnc + "|";
            extraData += OwnerAccountId + "|";
            if (RelatedTx != null)
                extraData += RelatedTx + "|";       // for compatible
            extraData += AccountType + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Name: {Name}\n";
            result += $"OwnerAccountId: {OwnerAccountId}\n";
            result += $"RelatedTx: {RelatedTx}\n";
            result += $"AccountType: {AccountType}\n";
            return result;
        }
    }
}
