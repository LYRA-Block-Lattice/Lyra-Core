using Lyra.Core.API;
using Lyra.Core.Decentralize;
using Lyra.Core.WorkFlow;
using Lyra.Data.API;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using WorkflowCore.Interface;

namespace UnitTests
{
    [TestClass]
    public class TestConfig
    {
        public static string networkId = "devnet";

        [AssemblyInitialize()]
        public static void MyTestInitialize(TestContext testContext)
        {
            var _af = new AuthorizersFactory();
            _af.Init();
            var _bf = new BrokerFactory();
            _bf.Init(_af, null);
        }
    }

    public class TestEnv : IHostEnv
    {
        IWorkflowHost _host;
        public string GetThumbPrint()
        {
            throw new NotImplementedException();
        }

        public IWorkflowHost GetWorkflowHost()
        {
            return _host;
        }

        public void SetWorkflowHost(IWorkflowHost workflowHost)
        {
            _host = workflowHost;
        }

        public async Task FireEventAsync(EventContainer ec)
        {
            throw new NotImplementedException();
            //await _hubContext.Clients.All.OnEvent(ec);
        }
    }
}
