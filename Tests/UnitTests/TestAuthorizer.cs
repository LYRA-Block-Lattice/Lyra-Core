using Akka.TestKit;
using Castle.Core.Logging;
using Lyra;
using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Cryptography;
using Lyra.Core.Utils;
using Lyra.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;

namespace UnitTests
{
    public class TestAuthorizer
    {
        public DagSystem TheDagSystem;
        public TestProbe fakeP2P;
        public Mock<IAccountCollectionAsync> mockStore;
        public Wallet posWallet;

        public TestAuthorizer(TestProbe testProbe)
        {
            Console.WriteLine("initialize DagSystem");

            fakeP2P = testProbe;
            mockStore = new Mock<IAccountCollectionAsync>();
            var keypair = Signatures.GenerateWallet();
            posWallet = UT_Wallet.Restore(keypair.privateKey);

            TheDagSystem = new DagSystem("xtest", mockStore.Object, posWallet, fakeP2P);
        }
    }
}
