using System;

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

using Lyra.Core.API;


namespace Lyra.Node.API.Controllers
{
    [Route("api/[controller]")]
    public class GetSyncHeightController : Controller
    {
        // GET: api/GetSyncHeight
        [HttpGet]
        public async Task<AccountHeightAPIResult> Get()
        {
            try
            {
                var result = await RPC.Client.GetSyncHeight();
                return result;
            }
            catch (Exception e) // Remove the exception message from teh result before going to production!
            {
                return new AccountHeightAPIResult() { ResultCode = APIResultCodes.ExceptionInNodeAPI, ResultMessage = e.Message };
            }

        }


    }
}
