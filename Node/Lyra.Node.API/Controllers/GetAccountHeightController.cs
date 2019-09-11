using System;

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;


using Lyra.Core.API;

namespace Lyra.Node.API.Controllers
{
    [Route("api/[controller]")]
    public class GetAccountHeightController : BaseController
    {
        
        [HttpGet("{AccountId}/{Signature}")]
        public async Task<AccountHeightAPIResult> Get(string AccountId, string Signature)
        {
            try
            {
                if (!ValidateSignature(AccountId, Signature))
                    return new AccountHeightAPIResult() { ResultCode = APIResultCodes.APISignatureValidationFailed };

                var result = await RPC.Client.GetAccountHeight(AccountId, Signature);
                return result;
            }
            catch (Exception e) // Remove the exception message from teh result before going to production!
            {
                return new AccountHeightAPIResult() { ResultCode = APIResultCodes.ExceptionInNodeAPI, ResultMessage = e.Message };
            }

        }

    }
}
