﻿using Lyra.Core.Blocks;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Data.Blocks
{
    public interface IBrokerAccount
    {
        public DateTime TimeStamp { get; set; }
        public string AccountID { get; set; }
        public string Name { get; set; }
        public string OwnerAccountId { get; set; }
        /// <summary>
        /// Transaction Initiator's SendTransferBlock Hash.
        /// </summary>
        public string RelatedTx { get; set; }
    }

    [BsonIgnoreExtraElements]
    public abstract class BrokerAccountRecv : ReceiveTransferBlock, IBrokerAccount
    {
        // user specified string, less thant 32 char
        public string Name { get; set; } = null!;
        public string OwnerAccountId { get; set; } = null!;
        public string RelatedTx { get; set; } = null!;

        public override bool AuthCompare(Block? other)
        {
            var ob = other as BrokerAccountRecv;
            return base.AuthCompare(ob) &&
                Name == ob?.Name &&
                OwnerAccountId == ob.OwnerAccountId &&
                RelatedTx == ob.RelatedTx;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            var plainTextBytes = Encoding.UTF8.GetBytes(Name??"");
            var nameEnc = Convert.ToBase64String(plainTextBytes);   // to avoid attack
            extraData += nameEnc + "|";
            extraData += OwnerAccountId + "|";
            if (RelatedTx != null)
                extraData += RelatedTx + "|";       // for compatible
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Name: {Name}\n";
            result += $"OwnerAccountId: {OwnerAccountId}\n";
            result += $"RelatedTx: {RelatedTx}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public abstract class BrokerAccountSend : SendTransferBlock, IBrokerAccount
    {
        public string OwnerAccountId { get; set; } = null!;
        public string RelatedTx { get; set; } = null!;

        // user specified string, less thant 32 char
        public string Name { get; set; } = null!;

        public override bool AuthCompare(Block? other)
        {
            var ob = other as BrokerAccountSend;
            return base.AuthCompare(ob) &&
                Name == ob.Name &&
                OwnerAccountId == ob.OwnerAccountId &&
                RelatedTx == ob.RelatedTx;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            var plainTextBytes = Encoding.UTF8.GetBytes(Name??"");
            var nameEnc = Convert.ToBase64String(plainTextBytes);   // to avoid attack
            extraData += nameEnc + "|";
            extraData += OwnerAccountId + "|";
            if (RelatedTx != null)
                extraData += RelatedTx + "|";       // for compatible
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Name: {Name}\n";
            result += $"OwnerAccountId: {OwnerAccountId}\n";
            result += $"RelatedTx: {RelatedTx}\n";
            return result;
        }
    }
}
