using Castle.Core.Logging;
using Lyra;
using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Utils;
using Lyra.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;

namespace UnitTests
{
    public static class TestBlockChain
    {
        

        static TestBlockChain()
        {
            Console.WriteLine("initialize DagSystem");



            // Ensure that blockchain is loaded

            var _ = BlockChain.Singleton;
        }

        public static void InitializeMockDagSystem()
        {
        }


    }
}
