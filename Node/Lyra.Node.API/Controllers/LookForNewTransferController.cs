using System;

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

using Lyra.Core.API;

namespace Lyra.Node.API.Controllers
{
    [Route("api/[controller]")]
    public class LookForNewTransferController : BaseController
    {
        // GET: api/getblock/[AccountId]/[Index]
        [HttpGet("{AccountId}/{Signature}")]
        public async Task<NewTransferAPIResult> Get(string AccountId, string Signature)
        {
            try
            {
                if (!ValidateSignature(AccountId, Signature))
                    return new NewTransferAPIResult() { ResultCode = APIResultCodes.APISignatureValidationFailed };

                var result = await RPC.Client.LookForNewTransfer(AccountId, Signature);
                return result;
            }
            catch (Exception e) // Remove the exception message from teh result before going to production!
            {
                return new NewTransferAPIResult() { ResultCode = APIResultCodes.ExceptionInNodeAPI, ResultMessage = e.Message };
            }

        }

    }
}
