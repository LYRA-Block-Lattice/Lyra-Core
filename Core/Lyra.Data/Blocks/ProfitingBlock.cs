using Lyra.Core.API;
using Lyra.Core.Blocks;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Data.Blocks
{
    public interface IProfiting : IBrokerAccount
    {
        public ProfitingType PType { get; set; }
        public decimal ShareRito { get; set; }
        public int Seats { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class ProfitingBlock : BrokerAccountRecv, IProfiting
    {
        public ProfitingType PType { get; set; }
        public decimal ShareRito { get; set; }
        public int Seats { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.Profiting;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as ProfitingBlock;

            return base.AuthCompare(ob) &&
                PType == ob.PType &&
                ShareRito == ob.ShareRito &&
                Seats == ob.Seats
                ;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += PType.ToString() + "|";
            extraData += ShareRito.ToBalanceLong().ToString() + "|";
            extraData += Seats.ToString() + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Profiting Type: {PType}\n";
            result += $"Share Rito: {ShareRito}\n";
            result += $"Seats: {Seats}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class ProfitingGenesis : ProfitingBlock, IOpeningBlock
    {
        public AccountTypes AccountType { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.ProfitingGenesis;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as ProfitingGenesis;

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


    [BsonIgnoreExtraElements]
    public class BenefitingBlock : BrokerAccountSend, IProfiting
    {
        public ProfitingType PType { get; set; }
        public decimal ShareRito { get; set; }
        public int Seats { get; set; }

        // block specified property
        // when distribute profit, make sure the staking time frame overlap profit time.
        // staking -> last profiting -> this profiting -> unstaking
        public string StakingAccountId { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.Benefiting;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as BenefitingBlock;

            return base.AuthCompare(ob) &&
                PType == ob.PType &&
                ShareRito == ob.ShareRito &&
                Seats == ob.Seats &&
                StakingAccountId == ob.StakingAccountId
                ;
        }
        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += PType.ToString() + "|";
            extraData += ShareRito.ToString() + "|";
            extraData += Seats.ToString() + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Profiting Type: {PType}\n";
            result += $"Share Rito: {ShareRito}\n";
            result += $"Seats: {Seats}\n";
            return result;
        }
    }
}
