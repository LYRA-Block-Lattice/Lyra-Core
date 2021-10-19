using Lyra.Core.API;
using Lyra.Data.Blocks;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Blocks
{

    public interface IStaking
    {
        public string Voting { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class StakingBlock : ReceiveTransferBlock, IBrokerAccount, IStaking
    {
        public AccountTypes AccountType { get; set; }
        public string Name { get; set; }
        public string Voting { get; set; }
        public string OwnerAccountId { get; set; }
        public string RelatedTx { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.Staking;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += AccountType + "|";

            var plainTextBytes = Encoding.UTF8.GetBytes(Name);
            var nameEnc = Convert.ToBase64String(plainTextBytes);   // to avoid attack
            extraData += nameEnc + "|";
            extraData += OwnerAccountId + "|";

            if (RelatedTx != null)
                extraData += RelatedTx + "|";       // for compatible
            extraData += Voting.ToString() + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Name: {Name}\n";
            result += $"AccountType: {AccountType}\n";
            result += $"OwnerAccountId: {OwnerAccountId}\n";
            result += $"Voting: {Voting}\n";
            result += $"RelatedTx: {RelatedTx}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class StakingGenesis: StakingBlock, IOpeningBlock
    {

    }

    [BsonIgnoreExtraElements]
    public class UnStakingBlock : SendTransferBlock, IBrokerAccount, IStaking
    {
        public AccountTypes AccountType { get; set; }
        public string Name { get; set; }
        public string Voting { get; set; }
        public string OwnerAccountId { get; set; }
        public string RelatedTx { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.UnStaking;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += AccountType + "|";

            var plainTextBytes = Encoding.UTF8.GetBytes(Name);
            var nameEnc = Convert.ToBase64String(plainTextBytes);   // to avoid attack
            extraData += nameEnc + "|";
            extraData += OwnerAccountId + "|";

            if (RelatedTx != null)
                extraData += RelatedTx + "|";       // for compatible
            extraData += Voting.ToString() + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Name: {Name}\n";
            result += $"AccountType: {AccountType}\n";
            result += $"OwnerAccountId: {OwnerAccountId}\n";
            result += $"Voting: {Voting}\n";
            result += $"RelatedTx: {RelatedTx}\n";
            return result;
        }
    }

}
