using Lyra.Core.Blocks;
using Lyra.Data.Blocks;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API.WorkFlow
{
    public enum SubjectType { None, OTCDispute }
    public class VotingSubject {
        public SubjectType Type { get; set; }

        /// <summary>
        /// who created the vote
        /// </summary>
        public string Issuer { get; set; }  // who create the vote

        /// <summary>
        /// all dao's stake holder has the ablility to vote.
        /// dao will specified how vote counted (by staking or by seat)
        /// </summary>
        public string DaoId { get; set; }

        /// <summary>
        /// the voting will valid through, in minutes
        /// </summary>
        public int TimeSpan { get; set; }

        public string Title { get; set; }   // the title
        public string Description { get; set; } // the description of vote
        /// <summary>
        /// voter chose one
        /// </summary>
        public string[] Options { get; set; }

        public string GetExtraData()
        {
            var extraData = $"{Type}|";
            extraData += $"{Issuer}|";
            extraData += $"{DaoId}|";
            extraData += $"{TimeSpan}|";
            extraData += $"{Convert.ToBase64String(Encoding.UTF8.GetBytes(Title))}|";
            extraData += $"{Convert.ToBase64String(Encoding.UTF8.GetBytes(Description))}|";
            extraData += $"{string.Join(',', Options)}|";
            return extraData;
        }
    }

    [BsonIgnoreExtraElements]
    public class VotingBlock : BrokerAccountRecv
    {
        public int OptionIndex { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.Voting;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as VotingBlock;

            return base.AuthCompare(ob) &&
                    OptionIndex.Equals(ob.OptionIndex);
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += $"{OptionIndex}|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"OptionIndex: {OptionIndex}\n";
            return result;
        }
    }


    [BsonIgnoreExtraElements]
    public class VotingGenesisBlock : OtcOrderRecvBlock, IOpeningBlock
    {
        public AccountTypes AccountType { get; set; }
        public VotingSubject Subject { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.VoteGenesis;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as VotingGenesisBlock;

            return base.AuthCompare(ob) &&
                AccountType == ob.AccountType &&
                Subject.GetExtraData() == ob.Subject.GetExtraData()
                ;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += AccountType + "|";
            extraData += Subject.GetExtraData() + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"AccountType: {AccountType}\n";
            result += $"Subject: {Subject}\n";
            return result;
        }
    }
}
