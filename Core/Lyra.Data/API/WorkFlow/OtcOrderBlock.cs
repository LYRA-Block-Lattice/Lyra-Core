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
    public enum OTCOrderStatus { 
        Open, 
        Partial, 
        Closed, 
        Delist 
    };
    public interface IOtcOrder : IBrokerAccount
    {
        OTCOrder Order { get; set; }
        OTCOrderStatus OOStatus { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class OtcOrderRecvBlock : BrokerAccountRecv, IOtcOrder
    {
        public OTCOrder Order { get; set; }
        public OTCOrderStatus OOStatus { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.OTCOrderRecv;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as OtcOrderRecvBlock;

            return base.AuthCompare(ob) &&
                    Order.Equals(ob.Order) &&
                    OOStatus.Equals(ob.OOStatus);
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += Order.GetExtraData(this) + "|";
            extraData += $"{OOStatus}|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"{Order}\n";
            result += $"Status: {OOStatus}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class OtcOrderSendBlock : BrokerAccountSend, IOtcOrder
    {
        public OTCOrder Order { get; set; }
        public OTCOrderStatus OOStatus { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.OTCOrderSend;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as OtcOrderSendBlock;

            return base.AuthCompare(ob) &&
                    Order.Equals(ob.Order) &&
                    OOStatus.Equals(ob.OOStatus);
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += Order.GetExtraData(this) + "|";
            extraData += $"{OOStatus}|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"{Order}\n";
            result += $"Status: {OOStatus}\n";
            return result;
        }
    }


    [BsonIgnoreExtraElements]
    public class OTCOrderGenesisBlock : OtcOrderRecvBlock, IOpeningBlock
    {
        public AccountTypes AccountType { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.OTCOrderGenesis;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as OTCOrderGenesisBlock;

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
