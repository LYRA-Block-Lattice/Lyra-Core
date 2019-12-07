using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Lyra.Core.Utils
{
    public static class OrleansSettings
    {
        public static IConfiguration AppSetting { get; }
        static OrleansSettings()
        {
            AppSetting = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("Orleans.json")
                    .Build();
        }
    }
}
