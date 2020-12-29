using Lyra.Core.API;
using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API
{
    public interface ITokenSwapAPI
    {
        Task<PoolAPIResult> CreatePool(string token0, string token1);
        Task<PoolAPIResult> GetPool(string token0, string token1);
    }
}
