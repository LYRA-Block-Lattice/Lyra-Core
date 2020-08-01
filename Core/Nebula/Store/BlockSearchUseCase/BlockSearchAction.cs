using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Store.BlockSearchUseCase
{
    public class BlockSearchAction
    {
        public string hash { get; }

        public BlockSearchAction(string hashToSearch)
        {
            hash = hashToSearch;
        }
    }
}
