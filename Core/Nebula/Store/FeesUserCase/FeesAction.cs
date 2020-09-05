using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Store.FeesUserCase
{
    public class FeesAction
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
