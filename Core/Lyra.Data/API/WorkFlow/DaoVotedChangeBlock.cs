using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.Blocks;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API.WorkFlow
{
    public interface IVoteExec
    {
        string voteid { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class DaoVotedChangeBlock: DaoRecvBlock, IVoteExec
    {
        public string voteid { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.OrgnizationChange;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as DaoVotedChangeBlock;

            return base.AuthCompare(ob) &&
                voteid == ob.voteid

                ;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();

            extraData += voteid + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Vote ID: {voteid}\n";
            return result;
        }
    }
}
