using Lyra.Core.API;
using Lyra.Data.Blocks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Blocks
{

    public interface IFiatWallet : IBrokerAccount
    {
        // link to the genesis block. zero supply genesis 
        public string GenesisHash { get; set; }
        // external symbol, 'USDT'
        public string ExtSymbol { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class FiatReceiveBlock : BrokerAccountRecv, IFiatWallet
    {
        public string GenesisHash { get; set; }
        public string ExtSymbol { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.FiatRecvToken;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as FiatReceiveBlock;

            return base.AuthCompare(ob) &&
                GenesisHash == ob.GenesisHash &&
                ExtSymbol == ob.ExtSymbol
                ;
        }

        public decimal GetAmount()
        {
            if (Balances.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
                return Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();
            else
                return 0;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += GenesisHash + "|";
            extraData += ExtSymbol + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"GenesisHash: {GenesisHash}\n";
            result += $"ExtSymbol: {ExtSymbol}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class FiatSendBlock : BrokerAccountSend, IFiatWallet
    {
        public string GenesisHash { get; set; }
        public string ExtSymbol { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.FiatSendToken;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as FiatSendBlock;

            return base.AuthCompare(ob) &&
                GenesisHash == ob.GenesisHash &&
                ExtSymbol == ob.ExtSymbol
                ;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += GenesisHash + "|";
            extraData += ExtSymbol + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"GenesisHash: {GenesisHash}\n";
            result += $"ExtSymbol: {ExtSymbol}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class FiatWalletGenesis : FiatReceiveBlock, IOpeningBlock
    {
        public AccountTypes AccountType { get; set; }

        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.FiatWalletGenesis;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as FiatWalletGenesis;

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
