using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.Blocks
{
    public interface IDao : IBrokerAccount
    {
        public string Description { get; set; }
        public Dictionary<string, long> Treasure { get; set; }
        //public Dictionary<string, long> Collateral { get; set; }
        //public Dictionary<string, string> WorkFlows { get; set; }
    }
    public class DaoBlock : BrokerAccountRecv, IDao
    {
        public string Description { get; set; }
        public Dictionary<string, long> Treasure { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.Orgnization;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as DaoBlock;

            return base.AuthCompare(ob) &&
                Description == ob.Description &&
                CompareDict(Treasure, ob.Treasure)
                ;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += Description + "|";
            extraData += DictToStr(Treasure) + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Description: {Description}\n";
            result += $"Treasure: {DictToStr(Treasure)}\n";
            return result;
        }
    }
}
