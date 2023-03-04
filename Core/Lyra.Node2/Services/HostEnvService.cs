using Lyra.Core.API;
using Lyra.Data.API;
using Lyra.Node2;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using WorkflowCore.Interface;
using ZstdSharp.Unsafe;

namespace Noded.Services
{
    public class HostEnvService : IHostEnv
    {
        private readonly IHubContext<LyraEventHub, ILyraEvent> _hubContext;

        public HostEnvService(IHubContext<LyraEventHub, ILyraEvent> hubContext)
        {
            _hubContext = hubContext;
        }

        public string GetThumbPrint()
        {
            var ksi = Startup.App.ApplicationServices.GetService(typeof(IServer));
#if NET7_0
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

        public IWorkflowHost GetWorkflowHost()
        {
            return _host;
        }

        IWorkflowHost _host;

        public void SetWorkflowHost(IWorkflowHost workflowHost)
        {
            _host = workflowHost;
        }

        public async Task FireEventAsync(EventContainer ec)
        {
            await _hubContext.Clients.All.OnEvent(ec);
        }
    }
}
