
namespace Lyra.Data.API
{
    public enum OrderState { Placed, Executed, PartialExecuted, InsufficientFunds, BadOrder }
    public class CancelKey
    {
        public OrderState State { get; set; }
        public string Key { get; set; }
        public TokenTradeOrder Order {get; set;}
    }
}