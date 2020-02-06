using Lyra.Core.API;
using Lyra.Core.Decentralize;
using System;
using System.Collections.Generic;
using System.Text;

namespace LyraNodesBot
{
    public static class Extensions
    {
        public static IEnumerable<string> FailReasons(this PosNode node)
        {
            if (node.Balance < LyraGlobal.MinimalAuthorizerBalance)
                yield return "Low Balance";
            if (DateTime.Now - node.LastStaking > TimeSpan.FromMinutes(12))
                yield return "Inactive";
        }
    }
}
