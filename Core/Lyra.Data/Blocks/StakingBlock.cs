using Lyra.Core.API;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Blocks
{
    [BsonIgnoreExtraElements]
    public class ProfitingBlock : ReceiveTransferBlock, IOpeningBlock
    {
        public string OwnerAccountId { get; set; }
        public ProfitingType PType { get; set; }
        public decimal ShareRito { get; set; }
        public int Seats { get; set; }
        public string RelatedTx { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.Profiting;
        }

        public AccountTypes AccountType { get; set; }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += PType.ToString() + "|";
            extraData += ShareRito.ToString() + "|";
            extraData += Seats.ToString() + "|";
            if (RelatedTx != null)
                extraData += RelatedTx + "|";       // for compatible
            extraData += AccountType + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Profiting Type: {PType}\n";
            result += $"Share Rito: {ShareRito}\n";
            result += $"Seats: {Seats}\n";
            result += $"RelatedTx: {RelatedTx}\n";
            result += $"AccountType: {AccountType}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class StakingBlock : ReceiveTransferBlock, IOpeningBlock
    {
        public string OwnerAccountId { get; set; }
        public string RelatedTx { get; set; }
        public long Amount { get; set; }
        public string Voting { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.Staking;
        }

        public AccountTypes AccountType { get; set; }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += Amount.ToString() + "|";
            extraData += Voting.ToString() + "|";
            if (RelatedTx != null)
                extraData += RelatedTx + "|";       // for compatible
            extraData += AccountType + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Amount: {Amount}\n";
            result += $"Voting: {Voting}\n";
            result += $"RelatedTx: {RelatedTx}\n";
            result += $"AccountType: {AccountType}\n";
            return result;
        }
    }

}
