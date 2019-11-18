using System;

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

using Lyra.Core.Blocks.Transactions;

using Lyra.Core.API;

namespace Lyra.Node.API.Controllers
{
    [Route("api/[controller]")]
    public class ReceiveTransferAndOpenAccountController : Controller
    {
        [HttpPost]
        [ActionName("OpenAccountWithGenesis")]
        public async Task<AuthorizationAPIResult> OpenAccountWithGenesis([FromBody] OpenWithReceiveTransferBlock block)
        {
            try
            {
                if (!block.VerifySignature(block.AccountID))
                    return new AuthorizationAPIResult() { ResultCode = APIResultCodes.BlockSignatureValidationFailed };

                var result = await RPC.Client.ReceiveTransferAndOpenAccount(block);
                return result;
            }
            catch (Exception e) // Remove the exception message from teh result before going to production!
            {
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.ExceptionInNodeAPI, ResultMessage = e.Message }; 
            }
        }

    }
}
