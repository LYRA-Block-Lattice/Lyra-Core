using System;

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

using Lyra.Core.Blocks.Transactions;

using Lyra.Core.API;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Lyra.Node.API.Controllers
{
    [Route("api/[controller]")]
    public class SendTransferController : Controller
    {
        [HttpPost]
        [ActionName("SendTransfer")]
        public async Task<AuthorizationAPIResult> SendTransfer([FromBody] SendTransferBlock block)
        {
            try
            {
                if (!block.VerifySignature(block.AccountID))
                    return new AuthorizationAPIResult() { ResultCode = APIResultCodes.BlockSignatureValidationFailed };

                var result = await RPC.Client.SendTransfer(block);
                return result;
            }
            catch (Exception e) // Remove the exception message from teh result before going to production!
            {
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.ExceptionInNodeAPI, ResultMessage = e.Message }; 
            }
        }

    }
}
