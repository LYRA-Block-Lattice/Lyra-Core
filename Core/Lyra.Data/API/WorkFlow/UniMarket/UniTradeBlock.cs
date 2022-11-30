using Humanizer;
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
        Processing,
        //Arrived,
        //BidSent = 10,
        //BidReceived,
        //OfferSent,
        //OfferReceived,

        //// sku to sku
        //BothShipping,
        //BothConfirmed,

        // special trade has special state, add bellow.


        // trade ends successfull
        Closed = 30,

        // trade in abnormal states
        Dispute = 40,
        DisputeClosed = 45,

        // canceled trade. not count.
        Canceled = 50,
    };

    public enum PoDCatalog
    {
        BidSent,
        BidReceived,
        OfferSent,
        OfferReceived,
    }

    // to protect privacy we only store signature in block.
    // user create this structure and send it to peer in protected messaging tunnel.
    // peer can verify it.
    public class ProofOfDilivery : SignableObject
    {
        public PoDCatalog Catalog { get; set; }
        public string Owner { get; set; } = null!;
        public string? Carrier { get; set; }
        public string? TrackingTag { get; set; }
        public string? ExtraInfo { get; set; }  // any suppliciant info

        public override string GetHashInput()
        {
            return $"{Catalog}|{Owner}|{Carrier}|{TrackingTag}|{ExtraInfo}";
        }

        protected override string GetExtraData()
        {
            return "";
        }
    }

    public class DeliveryStatus
    {
        // catalog vs PoD signature
        public Dictionary<PoDCatalog, string>? Proofs { get; set; }

        public override string ToString()
        {
            return Proofs?
                .Select(m => $"Proof of {m.Key.Humanize()}:{m.Value}")
                .Aggregate((m1, m2) => $"{m1}\n{m2}") ?? "";
        }

        public string HashSrc => Proofs?
                .Select(m => $"{m.Key}:{m.Value}")
                .Aggregate((m1, m2) => $"{m1},{m2}") ?? "";

        public void Add(PoDCatalog catalog, string signature)
        {
            if (Proofs == null)
                Proofs = new Dictionary<PoDCatalog, string>();

            if (string.IsNullOrWhiteSpace(signature))
                throw new ArgumentOutOfRangeException("Invalid PoD signature");

            if (Proofs.ContainsKey(catalog))
                throw new InvalidOperationException("PoD catalog already exists");

            Proofs.Add(catalog, signature);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public interface IUniTrade : IBrokerAccount
    {
        UniTrade Trade { get; set; }
        DeliveryStatus Delivery { get; set; }
        UniTradeStatus UTStatus { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class UniTradeRecvBlock : BrokerAccountRecv, IUniTrade
    {
        public UniTrade Trade { get; set; } = null!;
        public DeliveryStatus Delivery { get; set; } = null!;
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
            extraData += $"{Delivery.HashSrc}|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"{Trade}\n";
            result += $"Status: {UTStatus}\n";
            result += Delivery.ToString();
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class UniTradeSendBlock : BrokerAccountSend, IUniTrade
    {
        public UniTrade Trade { get; set; } = null!;
        public DeliveryStatus Delivery { get; set; } = null!;
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
            extraData += $"{Delivery.HashSrc}|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"{Trade}\n";
            result += $"Status: {UTStatus}\n";
            result += Delivery.ToString();
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
