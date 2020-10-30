using Lyra.Core.API;
using Lyra.Node2;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Noded.Services
{
    public class HostEnvService : IHostEnv
    {
        public string GetThumbPrint()
        {
            var ks = Startup.App.ApplicationServices.GetService(typeof(IServer)) as KestrelServer;
            var kso = ks.Options;

            PropertyInfo prop =
                typeof(KestrelServerOptions).GetProperty("DefaultCertificate", BindingFlags.NonPublic | BindingFlags.Instance);

            MethodInfo getter = prop.GetGetMethod(nonPublic: true);
            var cert = getter.Invoke(kso, null) as X509Certificate2;
            if (cert != null)
                return cert.Thumbprint;
            else
                return null;
        }
    }
}
