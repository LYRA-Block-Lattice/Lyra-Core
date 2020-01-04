using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Utils
{
    public class SimpleLogger
    {
        public SimpleLogger(string catagory)
        {
            var loggerFactory = new LoggerFactory();

            Logger = loggerFactory.CreateLogger(catagory);
        }

        public ILogger Logger { get; private set; }
    }
}
