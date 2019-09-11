using System;

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

using Lyra.Core.API;
using Lyra.Core.Blocks;

namespace Lyra.Node.API.Controllers
{
    [Route("api/[controller]")]
    public class GetActiveTradeOrdersController : BaseController
    {
        [HttpGet("{AccountId}/{SellTokenCode}/{BuyTokenCode}/{OrderType}/{Signature}")]
        public async Task<ActiveTradeOrdersAPIResult> Get(string AccountId, string SellTokenCode, string BuyTokenCode, string OrderType, string Signature)
        {
            try
            {
                if (!ValidateSignature(AccountId, Signature))
                    return new ActiveTradeOrdersAPIResult() { ResultCode = APIResultCodes.APISignatureValidationFailed };

                if (string.IsNullOrEmpty(SellTokenCode) || string.IsNullOrEmpty(BuyTokenCode))
                    return new ActiveTradeOrdersAPIResult() { ResultCode = APIResultCodes.InvalidParameterFormat, ResultMessage = "Parameter cannot be null; use * to specify ALL values." };

                TradeOrderListTypes order_type = (TradeOrderListTypes)Enum.Parse(typeof(TradeOrderListTypes), OrderType);
                var result = await RPC.Client.GetActiveTradeOrders(AccountId, SellTokenCode, BuyTokenCode, order_type, Signature);
                return result;
            }
            catch (Exception e) // Remove the exception message from teh result before going to production!
            {
                return new ActiveTradeOrdersAPIResult() { ResultCode = APIResultCodes.ExceptionInNodeAPI, ResultMessage = e.Message };
            }

        }

    }
}
