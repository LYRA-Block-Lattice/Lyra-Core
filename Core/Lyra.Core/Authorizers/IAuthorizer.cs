using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public interface IAuthorizer
    {
        Task<(APIResultCodes, AuthorizationSignature)> AuthorizeAsync<T>(T tblock, bool WithSign = true);
        //APIResultCodes Commit<T>(T tblock);
    }
}
