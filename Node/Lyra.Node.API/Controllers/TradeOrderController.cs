using System;

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

using Lyra.Core.Blocks;
using Lyra.Core.API;

namespace Lyra.Node.API.Controllers
{
    [Route("api/[controller]")]

    public class TradeOrderController : Controller
    {
        [HttpPost]
        [ActionName("TradeOrder")]
        public async Task<AuthorizationAPIResult> TradeOrder([FromBody] TradeOrderBlock block)
        {
            try
            {
                if (!block.VerifySignature(block.AccountID))
                    return new AuthorizationAPIResult() { ResultCode = APIResultCodes.BlockSignatureValidationFailed };

                var result = await RPC.Client.TradeOrder(block);
                return result;
            }
            catch (Exception e) // Remove the exception message from the result before going to production!
            {
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.ExceptionInNodeAPI, ResultMessage = e.Message }; 
            }
        }

    }
}
