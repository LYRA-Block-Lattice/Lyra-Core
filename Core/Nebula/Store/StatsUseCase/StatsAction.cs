using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Store.StatsUseCase
{
    public class StatsAction
    {

    }

    public class AccountFeesAction
    {
        public string addr { get; }

        public AccountFeesAction(string addrToSearch)
        {
            addr = addrToSearch;
        }
    }
}
