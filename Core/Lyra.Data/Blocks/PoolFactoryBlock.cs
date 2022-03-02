using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Blocks
{
    public enum ProfitingType { Node, Oracle, Merchant, Yield, Orgnization }
    // user send the specified amount fee to pool factory
    // pool factory will generate a new pool account
    // user send funds to the pool to create it
    [BsonIgnoreExtraElements]
    public class PoolFactoryBlock : ReceiveTransferBlock, IOpeningBlock
    {
        public const string FactoryAccount = "LPFA82ZDTo4cyoeY3EGozTpbWWzUXAtHCm33cMDcXyPzuV2HQf1X2Z9xVAins9kGJdBY12iGAzBPuMZvvW6x4ktLXa1MKQ";
        
        public const decimal PoolCreateFee = 1000m;
        public const decimal StakingAccountCreateFee = 10m;
        public const decimal ProfitingAccountCreateFee = 100m;
        public const decimal DexWalletCreateFee = 10m;
        public const decimal DaoCreateFee = 10000m;

        public AccountTypes AccountType { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.PoolFactory;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += FactoryAccount + "|";

            //HACK: temporary fix. should roll out db upgrade in future.
            extraData += AccountTypes.Pool + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"FactoryAccount: {FactoryAccount}\n";
            result += $"AccountType: {AccountType}\n";
            return result;
        }
    }
}
