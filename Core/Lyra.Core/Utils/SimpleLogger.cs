using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Utils
{
    public class SimpleLogger
    {
        public static ILoggerFactory Factory { get; set; }
        public SimpleLogger(string catagory)
        {
            Logger = Factory.CreateLogger(catagory);
        }

        public ILogger Logger { get; private set; }
    }
}
