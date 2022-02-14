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
    public enum OTCTradeStatus { Open, FiatSent, FiatReceived, CryptoReleased, Closed, Dispute };
    public interface IOtcTrade : IBrokerAccount
    {
        OTCTrade Trade { get; set; }
        OTCTradeStatus OTStatus { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class OtcTradeRecvBlock : BrokerAccountRecv, IOtcTrade
    {
        public OTCTrade Trade { get; set; }
        public OTCTradeStatus OTStatus { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.OTCTradeRecv;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as OtcTradeRecvBlock;

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
    public class OtcTradeSendBlock : BrokerAccountSend, IOtcTrade
    {
        public OTCTrade Trade { get; set; }
        public OTCTradeStatus OTStatus { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.OTCTradeSend;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as OtcTradeSendBlock;

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
    public class OtcTradeGenesisBlock : OtcTradeRecvBlock, IOpeningBlock
    {
        public AccountTypes AccountType { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.OTCTradeGenesis;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as OtcTradeGenesisBlock;

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
