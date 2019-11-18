using Newtonsoft.Json;
using Lyra.Core.Blocks.Transactions;
using Lyra.Core.Protos;

namespace Lyra.Core.Blocks
{
    public enum TradeOrderTypes: ushort
    {
        Sell = 1,
        Buy = 2
    }

    public enum TradeOrderListTypes: ushort
    {
        All = 0,
        SellOnly = 1,
        BuyOnly = 2,
    }

    public class TradeOrderBlock : SendTransferBlock
    {
        // Sell or Buy?
        public TradeOrderTypes OrderType { get; set; }

        // That's what I sell
        public string SellTokenCode { get; set; }

        // That's what I want to get in exchange
        public string BuyTokenCode { get; set; }

        // How much I want to sell or buy
        public decimal TradeAmount { get; set; }

        // Don't want to sell or buy less than this 
        public decimal MinTradeAmount { get; set; }

        // Sell Price -
        // For sell order: how many buy tokens are needed to buy one sell token
        // For buy order: how many sell tokens are needed to buy one buy token
        public decimal Price { get; set; }

        // How many matching orders to fullfull -
        // applicable to non-fungible tokens when each token is unique and must be generated per order.
        // if set to 1, the order must be concluded after the first trade even if the amount of trade is less than the MaxSellAmount of the order.
        // if set to N > 1, the total of N trades can be executed against the order.
        // if set to 0, the number of trades that can be executed until the order is fulfilled is unlimited
        public int MaxQuantity { get; set; }

        // If set to true, this order owner commits to pay fees bor both traders so the second trader will pay zero fee
        // it's mostly needed to support automated trading such as discount/reward tokens,
        // so the merchant could cover customer's fees as teh customer simply might no have any gas to pay the fees.
        public bool CoverAnotherTradersFee { get; set; }

        // If set to true, this order owner requests another trader to cover the fee.
        public bool AnotherTraderWillCoverFee { get; set; }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += OrderType.ToString() + "|";
            extraData += SellTokenCode + "|";
            extraData += BuyTokenCode + "|";
            extraData += JsonConvert.SerializeObject(TradeAmount) + "|";
            extraData += JsonConvert.SerializeObject(MinTradeAmount) + "|";
            extraData += JsonConvert.SerializeObject(Price) + "|";
            extraData += MaxQuantity.ToString() + "|";
            extraData += CoverAnotherTradersFee.ToString() + "|";
            extraData += AnotherTraderWillCoverFee.ToString() + "|";
            return extraData;
        }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.TradeOrder;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"OrderType: {OrderType.ToString()}\n";
            result += $"SellTokenCode: {SellTokenCode}\n";
            result += $"BuyTokenCode: {BuyTokenCode}\n";
            result += $"TradeAmount: {JsonConvert.SerializeObject(TradeAmount)}\n";
            result += $"MinTradeAmount: {JsonConvert.SerializeObject(MinTradeAmount)}\n";
            result += $"Price: {JsonConvert.SerializeObject(Price)}\n";
            result += $"MaxQuantity: {MaxQuantity.ToString()}\n";
            result += $"CoverAnotherTradersFee: {CoverAnotherTradersFee.ToString()}\n";
            result += $"AnotherTraderWillCoverFee: {AnotherTraderWillCoverFee.ToString()}\n";
            return result;
        }


    }

    //// This block will adjust the amount of active order so the order owner can
    //// 1) get funds to process the execute trade and
    //// 2) keep the updated order in the original queue spot
    //public class UpdateTradeOrderBlock : ReceiveTransferBlock
    //{
    //    // The hash of the original trade order.
    //    public string TradeOrderId { get; set; }

    //    public long NewOrderAmount { get; set; }

    //    public override BlockTypes GetBlockType()
    //    {
    //        return BlockTypes.UpdateTradeOrder;
    //    }

    //    public override string GetExtraData()
    //    {
    //        string extraData = base.GetExtraData();
    //        extraData += TradeOrderId;
    //        return extraData;
    //    }
    //}

    public class TradeBlock : SendTransferBlock
    {
        // The hash of the seller's or buyer's trade order we are working with 
        public string TradeOrderId { get; set; }

        // That's what I sell
        public string SellTokenCode { get; set; }

        // That's what I get in exchange
        public string BuyTokenCode { get; set; }

        // How much I send
        public decimal SellAmount { get; set; }

        // How much I get
        public decimal BuyAmount { get; set; }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += TradeOrderId + "|";
            extraData += SellTokenCode + "|";
            extraData += BuyTokenCode + "|";
            extraData += JsonConvert.SerializeObject(SellAmount) + "|";
            extraData += JsonConvert.SerializeObject(BuyAmount) + "|";
            return extraData;
        }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.Trade;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"TradeOrderId: {TradeOrderId}\n";
            result += $"SellTokenCode: {SellTokenCode}\n";
            result += $"BuyTokenCode: {BuyTokenCode}\n";
            result += $"SellAmount: {JsonConvert.SerializeObject(SellAmount)}\n";
            result += $"BuyAmount: {JsonConvert.SerializeObject(BuyAmount)}\n";
            return result;
        }
    }

    // 
    public class ExecuteTradeOrderBlock : SendTransferBlock
    {
        // The hash of the original order block
        public string TradeOrderId { get; set; }

        // The hash of the trade block
        public string TradeId { get; set; }

        // That's what I send as a result of the trade
        public string SellTokenCode { get; set; }

        // That's what I want to get in exchange
        //public string BuyTokenCode { get; set; }

        // How much I send as a result of the trade
        public decimal SellAmount { get; set; }

        // 
        //public long BuyAmount { get; set; }

        public override string Print()
        {
            string result = base.Print();
            result += $"TradeOrderId: {TradeOrderId}\n";
            result += $"TradeId: {TradeId}\n";
            result += $"SellTokenCode: {SellTokenCode}\n";
            result += $"SellAmount: {JsonConvert.SerializeObject(SellAmount)}\n";
            return result;
        }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += TradeOrderId + "|";
            extraData += TradeId + "|";
            extraData += SellTokenCode + "|";
            extraData += JsonConvert.SerializeObject(SellAmount) + "|";
            return extraData;
        }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.ExecuteTradeOrder;
        }

        public override TransactionInfoEx GetTransaction(TransactionBlock previousBlock)
        {
            var transaction = new TransactionInfoEx();
            transaction.Amount = SellAmount;
            transaction.TokenCode = SellTokenCode;
            transaction.FeeCode = this.FeeCode;
            transaction.FeeAmount = this.Fee;

            if (transaction.FeeCode == transaction.TokenCode)
                transaction.TotalBalanceChange = transaction.Amount + transaction.FeeAmount;
            else
                transaction.TotalBalanceChange = transaction.Amount;
            return transaction;
        }

    }

    // Cancels the trade order, either fully or partially (after it was partually executed).
    // Returns remaining unsent funds locked by the trade order.
    public class CancelTradeOrderBlock: ReceiveTransferBlock
    {
        // The hash of the original order block
        public string TradeOrderId { get; set; }
    }



    // Example of merchant's sell order:
    // SellTokenId = "Discount Tokens"
    // BuyTokenId = "Reward Token"
    // MaxSellAmount = 50
    // MinSellAmount = 5
    // SellPrice = 10
    // MaxQuantity = 1

    // Example of customer's buy order:
    // SellTokenId = "Reward Tokens"
    // BuyTokenId = "Discount Token"
    // MaxSellAmount = 200
    // MinSellAmount = 200
    // SellPrice = 0.1
    // MaxQuantity = 1




}
