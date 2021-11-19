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

    public interface IDexWallet : IBrokerAccount
    {
        // internal symbol, 'LUSDT'
        public string IntSymbol { get; set; }
        // external symbol, 'USDT'
        public string ExtSymbol { get; set; }
        // external provider, like 'TRC20', 'ERC20'
        public string ExtProvider { get; set; }
        // external address, like 'Trxss.e...', '0x35535...'
        public string ExtAddress { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class DexReceiveBlock : BrokerAccountRecv, IDexWallet
    {
        public string IntSymbol { get; set; }
        public string ExtSymbol { get; set; }
        public string ExtProvider { get; set; }
        public string ExtAddress { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.DexRecvToken;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as DexReceiveBlock;

            return base.AuthCompare(ob) &&
                IntSymbol == ob.IntSymbol &&
                ExtSymbol == ob.ExtSymbol &&
                ExtProvider == ob.ExtProvider &&
                ExtAddress == ob.ExtAddress
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
            extraData += IntSymbol + "|";
            extraData += ExtSymbol + "|";
            extraData += ExtProvider + "|";
            extraData += ExtAddress + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"IntSymbol: {IntSymbol}\n";
            result += $"ExtSymbol: {ExtSymbol}\n";
            result += $"ExtProvider: {ExtProvider}\n";
            result += $"ExtAddress: {ExtAddress}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class DexSendBlock : BrokerAccountSend, IDexWallet
    {
        public string IntSymbol { get; set; }
        public string ExtSymbol { get; set; }
        public string ExtProvider { get; set; }
        public string ExtAddress { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.DexSendToken;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as DexSendBlock;

            return base.AuthCompare(ob) &&
                IntSymbol == ob.IntSymbol &&
                ExtSymbol == ob.ExtSymbol &&
                ExtProvider == ob.ExtProvider &&
                ExtAddress == ob.ExtAddress
                ;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += IntSymbol + "|";
            extraData += ExtSymbol + "|";
            extraData += ExtProvider + "|";
            extraData += ExtAddress + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"IntSymbol: {IntSymbol}\n";
            result += $"ExtSymbol: {ExtSymbol}\n";
            result += $"ExtProvider: {ExtProvider}\n";
            result += $"ExtAddress: {ExtAddress}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class DexWalletGenesis : DexReceiveBlock, IOpeningBlock
    {
        public AccountTypes AccountType { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.DexWalletGenesis;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as DexWalletGenesis;

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

    [BsonIgnoreExtraElements]
    public class DexTokenMintBlock : DexReceiveBlock
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.DexTokenMint;
        }

        public override bool AuthCompare(Block other)
        {
            var ob = other as DexWalletGenesis;

            return base.AuthCompare(ob)
                ;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            return result;
        }
    }
}
