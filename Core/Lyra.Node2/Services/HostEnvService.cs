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
            var ksi = Startup.App.ApplicationServices.GetService(typeof(IServer));
#if NET6_0
            var type = typeof(KestrelServer).Assembly.GetType("Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerImpl");

            PropertyInfo kesprop =
                type.GetProperty("Options", BindingFlags.Public | BindingFlags.Instance);

            MethodInfo getter1 = kesprop.GetGetMethod(nonPublic: true);
            var kso = getter1.Invoke(ksi, null) as KestrelServerOptions;
#else
            var ks = ksi as KestrelServer;
            var kso = ks.Options;
#endif


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
