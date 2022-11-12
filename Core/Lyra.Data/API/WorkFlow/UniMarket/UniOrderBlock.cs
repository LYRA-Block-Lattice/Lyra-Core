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
        Open, 
        Partial, 
        Closed, 
        Delist 
    };
    public interface IUniOrder : IBrokerAccount
    {
        UniOrder Order { get; set; }
        UniOrderStatus OOStatus { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class UniOrderRecvBlock : BrokerAccountRecv, IUniOrder
    {
        public UniOrder Order { get; set; }
        public UniOrderStatus OOStatus { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.UniOrderRecv;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as UniOrderRecvBlock;

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
    public class UniOrderSendBlock : BrokerAccountSend, IUniOrder
    {
        public UniOrder Order { get; set; }
        public UniOrderStatus OOStatus { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.UniOrderSend;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as UniOrderSendBlock;

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
    public class UniOrderGenesisBlock : UniOrderRecvBlock, IOpeningBlock
    {
        public AccountTypes AccountType { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.UniOrderGenesis;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as UniOrderGenesisBlock;

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
