using Lyra.Core.API;
using Lyra.Data.Blocks;
using MongoDB.Bson;
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
        public DateTime Start { get; set; }
        public bool CompoundMode { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class StakingBlock : BrokerAccountRecv, IStaking
    {
        public string Voting { get; set; }
        public int Days { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        [BsonRepresentation(BsonType.Document)]
        public DateTime Start { get; set; }
        public bool CompoundMode { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.Staking;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as StakingBlock;

            return base.AuthCompare(ob) &&
                Voting == ob.Voting &&
                Days == ob.Days
                ;
        }

        public decimal GetAmount()
        {
            if (Balances.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
                return Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();
            else
                return 0;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += Voting.ToString() + "|";
            extraData += Days.ToString() + "|";
            if (Version >= 4)
            {
                extraData += DateTimeToString(Start) + "|";
                extraData += CompoundMode + "|";
            }
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Voting: {Voting}\n";
            result += $"Days: {Days}\n";
            result += $"Start From: {DateTimeToString(Start)}\n";
            result += $"Compound Mode: {CompoundMode}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class UnStakingBlock : BrokerAccountSend, IStaking
    {
        public string Voting { get; set; }
        public int Days { get; set; }
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        [BsonRepresentation(BsonType.Document)]
        public DateTime Start { get; set; }
        public bool CompoundMode { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.UnStaking;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as UnStakingBlock;

            return base.AuthCompare(ob) &&
                Voting == ob.Voting &&
                Days == ob.Days
                ;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();

            extraData += Voting.ToString() + "|";
            extraData += Days.ToString() + "|";
            if (Version >= 4)
            {
                extraData += DateTimeToString(Start) + "|";
                extraData += CompoundMode + "|";
            }

            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Voting: {Voting}\n";
            result += $"Days: {Days}\n";
            result += $"Start From: {Start}\n";
            result += $"Compound Mode: {CompoundMode}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class StakingGenesis : StakingBlock, IOpeningBlock
    {
        public AccountTypes AccountType { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.StakingGenesis;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as StakingGenesis;

            return base.AuthCompare(ob) &&
                AccountType == ob.AccountType
                ;
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
}
