using System;

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

using Lyra.Core.Blocks;
using Lyra.Core.API;

namespace Lyra.Node.API.Controllers
{
    [Route("api/[controller]")]

    public class ExecuteTradeOrderController : Controller
    {
        [HttpPost]
        [ActionName("ExecuteTradeOrder")]
        public async Task<AuthorizationAPIResult> ExecuteTradeOrder([FromBody] ExecuteTradeOrderBlock block)
        {
            try
            {
                if (!block.VerifySignature(block.AccountID))
                    return new AuthorizationAPIResult() { ResultCode = APIResultCodes.BlockSignatureValidationFailed };

                var result = await RPC.Client.ExecuteTradeOrder(block);
                return result;
            }
            catch (Exception e) // Remove the exception message from teh result before going to production!
            {
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.ExceptionInNodeAPI, ResultMessage = e.Message }; 
            }
        }

    }
}
