
namespace Lyra.Exchange
{
    public enum OrderState { Placed, Executed, PartialExecuted, BadOrder }
    public class CancelKey
    {
        public OrderState State { get; set; }
        public string Key { get; set; }
    }
}