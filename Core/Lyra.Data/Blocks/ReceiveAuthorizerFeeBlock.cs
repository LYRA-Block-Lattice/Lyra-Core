﻿
using Lyra.Core.API;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace Lyra.Core.Blocks
{
    [BsonIgnoreExtraElements]
    [Obsolete]
    public class ReceiveAuthorizerFeeBlock : ReceiveTransferBlock
    {        
        public long ServiceBlockStartHeight { get; set; }
        public long ServiceBlockEndHeight { get; set; }
        public decimal AuthorizerFee { get; set; }

        public override bool AuthCompare(Block other)
        {
            var ob = other as ReceiveAuthorizerFeeBlock;

            return base.AuthCompare(ob) &&
                ServiceBlockStartHeight == ob.ServiceBlockStartHeight &&
                ServiceBlockEndHeight == ob.ServiceBlockEndHeight &&
                AuthorizerFee == ob.AuthorizerFee
                ;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += AuthorizerFee.ToString("0.########") + "|";
            extraData += $"{ServiceBlockStartHeight}|";
            extraData += $"{ServiceBlockEndHeight}|";
            return extraData;
        }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.ReceiveAuthorizerFee;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"ServiceBlock Start Height: {ServiceBlockStartHeight}\n";
            result += $"ServiceBlock End Height: {ServiceBlockEndHeight}\n";
            result += $"Authorizer Fee: {AuthorizerFee}\n";
            return result;
        }
    }
}
