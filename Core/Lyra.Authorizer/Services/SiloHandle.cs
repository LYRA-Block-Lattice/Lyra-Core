using Orleans.Hosting;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Authorizer.Services
{
    public class SiloHandle
    {
        public static ISiloBuilder TheSilo { get; set; }
    }
}
