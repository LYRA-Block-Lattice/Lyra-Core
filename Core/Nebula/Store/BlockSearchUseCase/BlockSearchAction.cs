using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Store.BlockSearchUseCase
{
    public class BlockSearchAction
    {
        public string hash { get; }
        public long height { get; }

        public BlockSearchAction(string hashToSearch, long heightToSearch)
        {
            hash = hashToSearch;
            height = heightToSearch;
        }
    }
}
