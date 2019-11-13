using System;

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

using Lyra.Core.API;

namespace Lyra.Node.API.Controllers
{
    [Route("api/[controller]")]
    public class GetTokenNamesController : BaseController
    {
        
        [HttpGet("{AccountId}/{Signature}/{keyword}")]
        public async Task<GetTokenNamesAPIResult> Get(string AccountId, string Signature, string keyword)
        {
            try
            {
                if (!ValidateSignature(AccountId, Signature))
                    return new GetTokenNamesAPIResult() { ResultCode = APIResultCodes.APISignatureValidationFailed };

                var result = await RPC.Client.GetTokenNames(AccountId, Signature, keyword);
                return result;
            }
            catch (Exception e) // Remove the exception message from teh result before going to production!
            {
                return new GetTokenNamesAPIResult() { ResultCode = APIResultCodes.ExceptionInNodeAPI, ResultMessage = e.Message };
            }

        }

    }
}
