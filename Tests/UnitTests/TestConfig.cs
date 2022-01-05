using Lyra.Core.Decentralize;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;

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
            _bf.Init(_af);
        }
    }
}
