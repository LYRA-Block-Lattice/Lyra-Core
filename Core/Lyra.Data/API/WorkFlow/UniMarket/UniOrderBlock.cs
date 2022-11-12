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
    public enum UniOrderStatus { 
        Open,       // just add, trade begin
        Partial,    // partial traded, total count reduced
        Closed,     // close order and all pending trading, get back collateral
        Delist      // prevent order from trading, but wait for all trading finished. after which order can be closed.
    };
    public interface IUniOrder : IBrokerAccount
    {
        UniOrder Order { get; set; }
        UniOrderStatus UOStatus { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class UniOrderRecvBlock : BrokerAccountRecv, IUniOrder
    {
        public UniOrder Order { get; set; } = null!;
        public UniOrderStatus UOStatus { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.UniOrderRecv;
        }

        public override bool AuthCompare(Block? other)
        {
            var ob = other as UniOrderRecvBlock;
            if (ob == null) return false;

            return base.AuthCompare(ob) &&
                    Order.Equals(ob.Order) &&
                    UOStatus.Equals(ob.UOStatus);
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += Order.GetExtraData(this) + "|";
            extraData += $"{UOStatus}|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"{Order}\n";
            result += $"Status: {UOStatus}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class UniOrderSendBlock : BrokerAccountSend, IUniOrder
    {
        public UniOrder Order { get; set; } = null!;
        public UniOrderStatus UOStatus { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.UniOrderSend;
        }

        public override bool AuthCompare(Block? other)
        {
            var ob = other as UniOrderSendBlock;
            if(ob == null) return false;

            return base.AuthCompare(ob) &&
                    Order.Equals(ob.Order) &&
                    UOStatus.Equals(ob.UOStatus);
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += Order.GetExtraData(this) + "|";
            extraData += $"{UOStatus}|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"{Order}\n";
            result += $"Status: {UOStatus}\n";
            return result;
        }
    }


    [BsonIgnoreExtraElements]
    public class UniOrderGenesisBlock : UniOrderRecvBlock, IOpeningBlock
    {
        public AccountTypes AccountType { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.UniOrderGenesis;
        }

        public override bool AuthCompare(Block? other)
        {
            var ob = other as UniOrderGenesisBlock;
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
