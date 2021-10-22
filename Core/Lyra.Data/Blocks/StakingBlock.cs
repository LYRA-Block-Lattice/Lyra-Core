using Lyra.Core.API;
using Lyra.Data.Blocks;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Blocks
{

    public interface IStaking : IBrokerAccount
    {
        // staking take effect after 1 day.
        public string Voting { get; set; }

        // min 3 days. after the time, voting is not effect and can ben redeemed any time.
        // if redeem earlier, a 1.2% fee will be charged. (SEC: < 2%)
        public int Days { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class StakingBlock : ReceiveTransferBlock, IStaking
    {
        public string Name { get; set; }
        public string OwnerAccountId { get; set; }
        public string RelatedTx { get; set; }

        public string Voting { get; set; }
        public int Days { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.Staking;
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
            extraData += Voting.ToString() + "|";
            extraData += Days.ToString() + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Name: {Name}\n";
            result += $"OwnerAccountId: {OwnerAccountId}\n";
            result += $"Voting: {Voting}\n";
            result += $"Days: {Days}\n";
            result += $"RelatedTx: {RelatedTx}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class StakingGenesis: StakingBlock, IOpeningBlock
    {
        public AccountTypes AccountType { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.StakingGenesis;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += AccountType + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"AccountType: {AccountType}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class UnStakingBlock : SendTransferBlock, IStaking
    {
        public AccountTypes AccountType { get; set; }
        public string Name { get; set; }        
        public string OwnerAccountId { get; set; }
        public string RelatedTx { get; set; }

        public string Voting { get; set; }
        public int Days { get; set; }

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
            extraData += Days.ToString() + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Name: {Name}\n";
            result += $"AccountType: {AccountType}\n";
            result += $"OwnerAccountId: {OwnerAccountId}\n";
            result += $"Voting: {Voting}\n";
            result += $"Days: {Days}\n";
            result += $"RelatedTx: {RelatedTx}\n";
            return result;
        }
    }

}
