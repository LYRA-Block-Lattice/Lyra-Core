using System;

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

using Lyra.Core.API;

namespace Lyra.Node.API.Controllers
{
    [Route("api/[controller]")]
    public class LookForNewTradeController : BaseController
    {
        // GET: api/lookfornewtrade/[AccountId]
        [HttpGet("{AccountId}/{BuyTokenCode}/{SellTokenCode}/{Signature}")]
        public async Task<TradeAPIResult> Get(string AccountId, string BuyTokenCode, string SellTokenCode, string Signature)
        {
            try
            {
                if (!ValidateSignature(AccountId, Signature))
                    return new TradeAPIResult() { ResultCode = APIResultCodes.APISignatureValidationFailed };

                if (string.IsNullOrEmpty(SellTokenCode) || string.IsNullOrEmpty(BuyTokenCode))
                    return new TradeAPIResult() { ResultCode = APIResultCodes.InvalidParameterFormat, ResultMessage = "Parameter cannot be null; use * to specify ALL values." };

                var result = await RPC.Client.LookForNewTrade(AccountId, BuyTokenCode, SellTokenCode, Signature);
                return result;
            }
            catch (Exception e) // Remove the exception message from teh result before going to production!
            {
                return new TradeAPIResult() { ResultCode = APIResultCodes.ExceptionInNodeAPI, ResultMessage = e.Message };
            }

        }

    }
}
