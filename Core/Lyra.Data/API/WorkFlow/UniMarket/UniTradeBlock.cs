using Lyra.Core.Blocks;
using Lyra.Data.Blocks;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API.WorkFlow.UniMarket
{
    /// <summary>
    /// 
    /// </summary>
    public interface IUniTrade : IBrokerAccount
    {
        UniTrade Trade { get; set; }
        UniTradeStatus OTStatus { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class UniTradeRecvBlock : BrokerAccountRecv, IUniTrade
    {
        public UniTrade Trade { get; set; }
        public UniTradeStatus OTStatus { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.UniTradeRecv;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as UniTradeRecvBlock;

            return base.AuthCompare(ob) &&
                OTStatus == ob.OTStatus &&
                    Trade.Equals(ob.Trade);
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += Trade.GetExtraData(this) + "|";
            extraData += $"{OTStatus}|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"{Trade}\n";
            result += $"Status: {OTStatus}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class UniTradeSendBlock : BrokerAccountSend, IUniTrade
    {
        public UniTrade Trade { get; set; }
        public UniTradeStatus OTStatus { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.UniTradeSend;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as UniTradeSendBlock;

            return base.AuthCompare(ob) &&
                OTStatus == ob.OTStatus &&
                    Trade.Equals(ob.Trade);
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += Trade.GetExtraData(this) + "|";
            extraData += $"{OTStatus}|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"{Trade}\n";
            result += $"Status: {OTStatus}\n";
            return result;
        }
    }


    [BsonIgnoreExtraElements]
    public class UniTradeGenesisBlock : UniTradeRecvBlock, IOpeningBlock
    {
        public AccountTypes AccountType { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.UniTradeGenesis;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as UniTradeGenesisBlock;

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
