using Lyra;
using System;
using System.Collections.Generic;
using System.Text;

namespace UnitTests
{
    public static class TestBlockChain
    {
        public static readonly DagSystem TheDagSystem;

        static TestBlockChain()
        {
            Console.WriteLine("initialize DagSystem");
            TheDagSystem = new DagSystem("");

            // Ensure that blockchain is loaded

            var _ = BlockChain.Singleton;
        }

        public static void InitializeMockDagSystem()
        {
        }
    }
}
