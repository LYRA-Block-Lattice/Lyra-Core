using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Utils
{
    public class SimpleLogger
    {
        public static SimpleLogger Instance;
        ILogger<SimpleLogger> _logger;
        public SimpleLogger(ILogger<SimpleLogger> logger)
        {
            Logger = logger;
            Instance = this;
        }

        public ILogger<SimpleLogger> Logger { get => _logger; private set => _logger = value; }
    }
}
