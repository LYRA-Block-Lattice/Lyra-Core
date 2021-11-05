using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class TransactionAuthorizer : BaseAuthorizer
    {
        protected override Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {


            return base.AuthorizeImplAsync(sys, tblock);
        }
    }
}
