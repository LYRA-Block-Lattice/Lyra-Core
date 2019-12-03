using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.API
{
    public interface IDAGNode : Orleans.IGrainWithIntegerKey
    {
        Task<GetVersionAPIResult> GetVersion(int apiVersion, string appName, string appVersion);
    }
}
