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
    public interface IOtcTrade : IBrokerAccount
    {
        OTCTrade Order { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class OtcTradeBlock : BrokerAccountRecv, IOtcTrade
    {
        public OTCTrade Order { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.OTCTrade;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as OtcTradeBlock;

            return base.AuthCompare(ob) &&
                    Order.Equals(ob.Order);
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += Order.GetExtraData() + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"{Order}\n";
            return result;
        }
    }


    [BsonIgnoreExtraElements]
    public class OtcTradeGenesis : OtcOrderBlock, IOpeningBlock
    {
        public AccountTypes AccountType { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.OTCOrderGenesis;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as OtcTradeGenesis;

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
