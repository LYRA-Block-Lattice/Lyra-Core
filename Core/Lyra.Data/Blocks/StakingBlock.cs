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
    public class StakingBlock : BrokerAccountRecv, IStaking
    {
        public string Voting { get; set; }
        public int Days { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.Staking;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += Voting.ToString() + "|";
            extraData += Days.ToString() + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Voting: {Voting}\n";
            result += $"Days: {Days}\n";
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
    public class UnStakingBlock : BrokerAccountSend, IStaking
    {
        public string Voting { get; set; }
        public int Days { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.UnStaking;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();

            extraData += Voting.ToString() + "|";
            extraData += Days.ToString() + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Voting: {Voting}\n";
            result += $"Days: {Days}\n";
            return result;
        }
    }

}
