using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.Blocks;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API.WorkFlow
{
    public enum ClientMode { Permissionless, InviteOnly, ApprovedOnly }

    public interface IDealer : IBrokerAccount
    {
        public ClientMode DealerMode { get; set; }
        public string Endpoint { get; set; }
        public string Description { get; set; }    // dao configuration record hash, in other db collection
    }

    [BsonIgnoreExtraElements]
    public class DealerRecvBlock : BrokerAccountRecv, IDealer
    {
        // dealer
        public ClientMode DealerMode { get; set; }
        public string Endpoint { get; set; }

        public string Description { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.DealerRecv;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as DealerRecvBlock;

            return base.AuthCompare(ob) &&
                DealerMode == ob.DealerMode &&
                Endpoint == ob.Endpoint &&
                Description == ob.Description
                ;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();

            extraData += DealerMode + "|";
            extraData += Endpoint + "|";
            extraData += Description + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();

            result += $"Client Mode: {DealerMode}\n";
            result += $"Endpoint URL: {Endpoint}\n";
            result += $"Description: {Description}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class DealerSendBlock : BrokerAccountSend, IDealer
    {
        // dealer
        public ClientMode DealerMode { get; set; }
        public string Endpoint { get; set; }

        public string Description { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.DealerSend;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as DealerRecvBlock;

            return base.AuthCompare(ob) &&
                DealerMode == ob.DealerMode &&
                Endpoint == ob.Endpoint &&
                Description == ob.Description
                ;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += DealerMode + "|";
            extraData += Endpoint + "|";
            extraData += Description + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();

            result += $"Client Mode: {DealerMode}\n";
            result += $"Endpoint URL: {Endpoint}\n";
            result += $"Description: {Description}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class DealerGenesisBlock : DealerRecvBlock, IOpeningBlock
    {
        public AccountTypes AccountType { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.DealerGenesis;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as DealerGenesisBlock;

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
