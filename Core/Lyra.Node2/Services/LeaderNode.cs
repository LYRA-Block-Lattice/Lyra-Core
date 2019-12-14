using org.apache.zookeeper.recipes.leader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Lyra.Node2.Services
{
    public class LeaderNode
    {
        
    }

    public class LeaderElected : LeaderElectionAware
    {
        public Task onElectionEvent(ElectionEventType eventType)
        {
            throw new NotImplementedException();
        }
    }
}
