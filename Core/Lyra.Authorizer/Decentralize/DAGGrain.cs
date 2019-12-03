using Lyra.Core.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Lyra.Authorizer.Decentralize
{
    public class DAGGrain : Orleans.Grain, IDAGNode
    {
        public Task<GetVersionAPIResult> GetVersion(int apiVersion, string appName, string appVersion)
        {
            throw new NotImplementedException();
        }
    }
}
