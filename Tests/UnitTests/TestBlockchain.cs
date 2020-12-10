using Akka.Actor;
using Lyra;
using Neo.Ledger;
using System;

namespace Neo.UnitTests
{
    public static class TestBlockchain
    {
        public static readonly DagSystem TheNeoSystem;

        static TestBlockchain()
        {
            Console.WriteLine("initialize DagSystem");
            TheNeoSystem = new DagSystem(null, null, null, null);
            TheNeoSystem.TheBlockchain.Tell(new Blockchain.Startup());

            // Ensure that blockchain is loaded

            var _ = Blockchain.Singleton;
        }

        public static void InitializeMockNeoSystem()
        {
        }
    }
}
