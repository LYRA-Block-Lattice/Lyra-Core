using Lyra.Core.API;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Blocks
{
    public interface IPool
    {
        Dictionary<string, long> Shares { get; set; }
    }
    /// <summary>
    /// 
    /// </summary>
    [BsonIgnoreExtraElements]
    public class PoolDepositBlock : ReceiveTransferBlock, IPool
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.PoolDeposit;
        }

        // AccountId -> Share
        // Initial pool token is 1M
        // make sure sum(share) is always 1M
        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<string, long> Shares { get; set; }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += DictToStr(Shares) + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Shares: {DictToStr(Shares)}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class PoolWithdrawBlock : SendTransferBlock, IPool
    {
        /// <summary>
        /// the hash of requested SendTransferBlock and ReceiveTransferBlock onside pool factory's chain
        /// RelatedTx -> RecvBlock (SourceHash) -> SendTransferBlcok
        /// on pool action to one send/recv combine
        /// </summary>
        public string RelatedTx { get; set; }
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.PoolWithdraw;
        }

        // AccountId -> Share
        // Initial pool token is 1M
        // make sure sum(share) is always 1M
        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<string, long> Shares { get; set; }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += DictToStr(Shares) + "|";
            extraData += RelatedTx + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"RelatedTx: {RelatedTx}\n";
            result += $"Shares: {DictToStr(Shares)}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class PoolSwapInBlock : ReceiveTransferBlock, IPool
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.PoolSwapIn;
        }

        // AccountId -> Share
        // Initial pool token is 1M
        // make sure sum(share) is always 1M
        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<string, long> Shares { get; set; }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += DictToStr(Shares) + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Shares: {DictToStr(Shares)}\n";
            return result;
        }
    }

    [BsonIgnoreExtraElements]
    public class PoolSwapOutBlock : SendTransferBlock, IPool
    {
        public string RelatedTx { get; set; }
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.PoolSwapOut;
        }

        // AccountId -> Share
        // Initial pool token is 1M
        // make sure sum(share) is always 1M
        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<string, long> Shares { get; set; }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += DictToStr(Shares) + "|";
            extraData += RelatedTx + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"RelatedTx: {RelatedTx}\n";
            result += $"Shares: {DictToStr(Shares)}\n";
            return result;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    [BsonIgnoreExtraElements]
    public class PoolGenesisBlock : ReceiveTransferBlock, IOpeningBlock
    {
        public string Token0 { get; set; }
        public string Token1 { get; set; }
        public string RelatedTx { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.PoolGenesis;
        }

        public AccountTypes AccountType { get; set; }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += Token0 + "|";
            extraData += Token1 + "|";
            extraData += RelatedTx + "|";
            extraData += AccountType + "|";
            return extraData;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Token0: {Token0}\n";
            result += $"Token1: {Token1}\n";
            result += $"RelatedTx: {RelatedTx}\n";
            result += $"AccountType: {AccountType}\n";
            return result;
        }
    }

    public class SwapCalculator
    {
        public const decimal LiquidateProviderFee = 0.003m;
        public const decimal ProtocolFee = 0.001m;

        public decimal SwapInAmount { get; private set; }
        public decimal SwapOutAmount { get; private set; }
        public decimal Price { get; private set; }
        public decimal PriceImpact { get; private set; }
        public decimal MinimumReceived { get; private set; }
        public decimal PayToProvider { get; private set; }
        public decimal PayToAuthorizer { get; private set; }

        public string SwapInToken { get; private set; }
        public string SwapOutToken { get; private set; }

        public SwapCalculator(string token0, string token1, TransactionBlock pool, string fromToken, decimal fromAmount, decimal slippage)
        {
            if (token0 == null || token1 == null || pool == null)
                throw new ArgumentException();

            SwapInAmount = fromAmount;
            SwapInToken = fromToken;

            var X = pool.Balances[token0].ToBalanceDecimal();
            var Y = pool.Balances[token1].ToBalanceDecimal();

            decimal fromFeeRito, toFeeRito;
            if (fromToken == LyraGlobal.OFFICIALTICKERCODE)
            {
                fromFeeRito = LiquidateProviderFee + ProtocolFee;
                toFeeRito = 0;
            }
            else
            {
                fromFeeRito = LiquidateProviderFee;
                toFeeRito = ProtocolFee;
            }

            decimal pureTo;
            if (fromToken == token1)
            {
                SwapOutToken = token0;

                pureTo = X - (X * Y / (Y + fromAmount * (1m - fromFeeRito)));
                SwapOutAmount = Math.Round(pureTo * (1 - toFeeRito), 8);

                PriceImpact = Math.Round(SwapOutAmount / X, 8);

                Price = Math.Round(SwapOutAmount / fromAmount, 16);

                MinimumReceived = SwapOutAmount * (1 - slippage);
            }
            else // fromToken == token0
            {
                SwapOutToken = token1;

                pureTo = Y - (X * Y / (X + fromAmount * (1m - fromFeeRito)));
                SwapOutAmount = Math.Round(pureTo * (1 - toFeeRito), 8);

                PriceImpact = Math.Round(SwapOutAmount / Y, 8);

                Price = Math.Round(SwapOutAmount / fromAmount, 16);     // only for display

                MinimumReceived = Math.Round(SwapOutAmount * (1 - slippage));
            }

            PayToProvider = Math.Round(fromAmount * LiquidateProviderFee, 8);
            if (fromToken == LyraGlobal.OFFICIALTICKERCODE)
            {
                PayToAuthorizer = Math.Round(fromAmount * ProtocolFee, 8);
            }
            else
            {
                PayToAuthorizer = Math.Round(pureTo * ProtocolFee, 8);
            }

            //Console.WriteLine($"Price {price} Got {to} X, Price Impact: {chg * 100:n} %");
        }
    }

    //public class SwapCalculator2
    //{
    //    // calculate the fee. 0.2%. half go to liquidate providers, half goto node operators (as fee)
    //    // reduct from swap in token
    //    public string swapInToken { get; private set; }
    //    public string swapOutToken { get; private set; }
    //    public decimal swapInAmount { get; private set; }
    //    public decimal swapOutAmount { get; private set; }
    //    public decimal poolFee { get; private set; }
    //    public decimal protocolFee { get; private set; }

    //    public SwapCalculator(string swapFromToken, decimal originalAmount, PoolGenesisBlock poolGenesis, decimal swapRito)
    //    {
    //        if (swapFromToken == poolGenesis.Token0 && poolGenesis.Token0 == LyraGlobal.OFFICIALTICKERCODE)
    //        {
    //            swapInToken = poolGenesis.Token0;
    //            swapOutToken = poolGenesis.Token1;

    //            // LYR -> other token
    //            swapInAmount = Math.Round(originalAmount * 0.998m, 8);
    //            swapOutAmount = Math.Round(swapInAmount / swapRito, 8);
    //            poolFee = Math.Round(originalAmount * 0.001m, 8);
    //            protocolFee = Math.Round(originalAmount * 0.001m, 8);
    //        }
    //        else if (swapFromToken == poolGenesis.Token0 && poolGenesis.Token0 != LyraGlobal.OFFICIALTICKERCODE)
    //        {
    //            swapInToken = poolGenesis.Token0;
    //            swapOutToken = poolGenesis.Token1;

    //            // other token -> LYR
    //            swapInAmount = originalAmount;
    //            var swapOutTotal = swapInAmount / swapRito;

    //            swapOutAmount = Math.Round(swapOutTotal * 0.998m, 8);
    //            poolFee = Math.Round(swapOutTotal * 0.001m, 8);
    //            protocolFee = Math.Round(swapOutTotal * 0.001m, 8);
    //        }
    //        else if (swapFromToken == poolGenesis.Token1 && poolGenesis.Token1 == LyraGlobal.OFFICIALTICKERCODE)
    //        {
    //            swapInToken = poolGenesis.Token1;
    //            swapOutToken = poolGenesis.Token0;

    //            // LYR -> other token
    //            swapInAmount = Math.Round(originalAmount * 0.998m, 8);
    //            poolFee = Math.Round(originalAmount * 0.001m, 8);
    //            protocolFee = Math.Round(originalAmount * 0.001m, 8);

    //            swapOutAmount = Math.Round(swapInAmount * swapRito, 8);
    //        }
    //        else if (swapFromToken == poolGenesis.Token1 && poolGenesis.Token1 != LyraGlobal.OFFICIALTICKERCODE)
    //        {
    //            swapInToken = poolGenesis.Token1;
    //            swapOutToken = poolGenesis.Token0;

    //            // other token -> LYR
    //            swapInAmount = originalAmount;
    //            var swapOutTotal = swapInAmount * swapRito;

    //            swapOutAmount = Math.Round(swapOutTotal * 0.998m, 8);
    //            poolFee = Math.Round(swapOutTotal * 0.001m, 8);
    //            protocolFee = Math.Round(swapOutTotal * 0.001m, 8);
    //        }
    //    }
    //}
}
