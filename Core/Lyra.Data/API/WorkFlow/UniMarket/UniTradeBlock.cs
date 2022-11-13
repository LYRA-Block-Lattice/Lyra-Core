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
    public enum UniTradeStatus
    {
        // start 
        Open,

        // trade begins
        BidSent = 10,
        BidReceived,
        OfferSent,
        OfferReceived,

        // special trade has special state, add bellow.

        // trade ends successfull
        Closed = 30,

        // trade in abnormal states
        Dispute = 40,
        DisputeClosed,

        // canceled trade. not count.
        Canceled = 50,
    };

    /// <summary>
    /// 
    /// </summary>
    public interface IUniTrade : IBrokerAccount
    {
        UniTrade Trade { get; set; }
        UniTradeStatus UTStatus { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class UniTradeRecvBlock : BrokerAccountRecv, IUniTrade
    {
        public UniTrade Trade { get; set; } = null!;
        public UniTradeStatus UTStatus { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.UniTradeRecv;
        }

        public override bool AuthCompare(Block? other)
        {
            var ob = other as UniTradeRecvBlock;
            if (ob == null) return false;

            return base.AuthCompare(ob) &&
                UTStatus == ob.UTStatus &&
                    Trade.Equals(ob.Trade);
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += Trade.GetExtraData(this) + "|";
            extraData += $"{UTStatus}|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"{Trade}\n";
            result += $"Status: {UTStatus}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class UniTradeSendBlock : BrokerAccountSend, IUniTrade
    {
        public UniTrade Trade { get; set; } = null!;
        public UniTradeStatus UTStatus { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.UniTradeSend;
        }

        public override bool AuthCompare(Block? other)
        {
            var ob = other as UniTradeSendBlock;
            if (ob == null) return false;

            return base.AuthCompare(ob) &&
                UTStatus == ob.UTStatus &&
                    Trade.Equals(ob.Trade);
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += Trade.GetExtraData(this) + "|";
            extraData += $"{UTStatus}|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"{Trade}\n";
            result += $"Status: {UTStatus}\n";
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

        public override bool AuthCompare(Block? other)
        {
            var ob = other as UniTradeGenesisBlock;
            if (ob == null) return false;

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
