﻿using Lyra.Data.API.WorkFlow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API.ABI
{
    public class DealerCreateArgument
    {
        public string Name { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string DealerAccountId { get; set; } = null!;
        public string ServiceUrl { get; set; } = null!;
        public ClientMode Mode { get; set; }
    }
}
