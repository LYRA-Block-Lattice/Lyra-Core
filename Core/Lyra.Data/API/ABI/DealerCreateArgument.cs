using Lyra.Data.API.WorkFlow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API.ABI
{
    public class DealerCreateArgument
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string DealerAccountId { get; set; }
        public string ServiceUrl { get; set; }
        public ClientMode Mode { get; set; }
    }
}
