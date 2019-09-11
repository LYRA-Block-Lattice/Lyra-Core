using System;

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

using Lyra.Core.API;

namespace Lyra.Node.API.Controllers
{
    [Route("api/[controller]")]
    public class GetTokenGenesisBlockController : BaseController
    {
        
        [HttpGet("{AccountId}/{TokenTicker}/{Signature}")]
        public async Task<BlockAPIResult> Get(string AccountId, string TokenTicker, string Signature)
        {
            try
            {
                if (!ValidateSignature(AccountId, Signature))
                    return new BlockAPIResult() { ResultCode = APIResultCodes.APISignatureValidationFailed };

                var result = await RPC.Client.GetTokenGenesisBlock(AccountId, TokenTicker, Signature);
                return result;
            }
            catch (Exception e) // Remove the exception message from teh result before going to production!
            {
                return new BlockAPIResult() { ResultCode = APIResultCodes.ExceptionInNodeAPI, ResultMessage = e.Message };
            }

        }

    }
}
