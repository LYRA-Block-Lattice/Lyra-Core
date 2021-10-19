using Lyra.Core.API;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Blocks
{
    [BsonIgnoreExtraElements]
    public class BrokerAccountBase : ReceiveTransferBlock, IOpeningBlock
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

    [BsonIgnoreExtraElements]
    public class ProfitingBlock : BrokerAccountBase
    {
        public ProfitingType PType { get; set; }
        public decimal ShareRito { get; set; }
        public int Seats { get; set; }        

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.Profiting;
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

    [BsonIgnoreExtraElements]
    public class StakingBlock : BrokerAccountBase
    {
        public long Amount { get; set; }
        public string Voting { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.Staking;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += Amount.ToString() + "|";
            extraData += Voting.ToString() + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Amount: {Amount}\n";
            result += $"Voting: {Voting}\n";
            return result;
        }
    }

}
