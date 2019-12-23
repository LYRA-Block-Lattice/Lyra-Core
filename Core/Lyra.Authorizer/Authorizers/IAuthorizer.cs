using Lyra.Core.Blocks;
using Orleans;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Authorizer.Authorizers
{
    public interface IAuthorizer : IGrainWithGuidKey
    {
        Task<APIResultCodes> Authorize<T>(T tblock);
        Task<APIResultCodes> Commit<T>(T tblock);
    }
}
