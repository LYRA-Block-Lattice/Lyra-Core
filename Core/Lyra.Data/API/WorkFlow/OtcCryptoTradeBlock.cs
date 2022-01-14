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
    public enum OtcCryptoTradeStatus { Open, FiatSent, FiatReceived, CryptoReleased, Closed, Dispute };
    public interface IOtcCryptoTrade : IBrokerAccount
    {
        OTCCryptoTrade Trade { get; set; }
        OtcCryptoTradeStatus Status { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class OtcCryptoTradeRecvBlock : BrokerAccountRecv, IOtcCryptoTrade
    {
        public OTCCryptoTrade Trade { get; set; }
        public OtcCryptoTradeStatus Status { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.OTCCryptoTradeRecv;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as OtcCryptoTradeRecvBlock;

            return base.AuthCompare(ob) &&
                Status == ob.Status &&
                    Trade.Equals(ob.Trade);
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += Trade.GetExtraData() + "|";
            extraData += $"{Status}|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"{Trade}\n";
            result += $"Status: {Status}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class OtcCryptoTradeSendBlock : BrokerAccountSend, IOtcCryptoTrade
    {
        public OTCCryptoTrade Trade { get; set; }
        public OtcCryptoTradeStatus Status { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.OTCCryptoTradeSend;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as OtcCryptoTradeSendBlock;

            return base.AuthCompare(ob) &&
                Status == ob.Status &&
                    Trade.Equals(ob.Trade);
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += Trade.GetExtraData() + "|";
            extraData += $"{Status}|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"{Trade}\n";
            result += $"Status: {Status}\n";
            return result;
        }
    }


    [BsonIgnoreExtraElements]
    public class OtcCryptoTradeGenesisBlock : OtcCryptoTradeRecvBlock, IOpeningBlock
    {
        public AccountTypes AccountType { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.OTCCryptoTradeGenesis;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as OtcCryptoTradeGenesisBlock;

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
