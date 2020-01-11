using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public interface IAuthorizer
    {
        (APIResultCodes, AuthorizationSignature) Authorize<T>(T tblock, bool WithSign = true);
        APIResultCodes Commit<T>(T tblock);
    }
}
