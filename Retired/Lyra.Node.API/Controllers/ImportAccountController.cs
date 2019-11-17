using System;

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;


using Lyra.Core.Blocks.Transactions;

using Lyra.Core.API;


namespace Lyra.Node.API.Controllers
{
    [Route("api/[controller]")]
    public class ImportAccountController : Controller
    {
        [HttpPost]
        [ActionName("ImportAccount")]
        public async Task<AuthorizationAPIResult> ImportAccount([FromBody] ImportAccountBlock block)
        {
            try
            {
                if (!block.VerifySignature(block.AccountID))
                    return new AuthorizationAPIResult() { ResultCode = APIResultCodes.BlockSignatureValidationFailed };

                var result = await RPC.Client.ImportAccount(block);
                return result;
            }
            catch (Exception e) // Remove the exception message from teh result before going to production!
            {
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.ExceptionInNodeAPI, ResultMessage = e.Message }; 
            }
        }

    }
}
