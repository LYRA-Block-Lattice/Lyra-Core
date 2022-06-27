using Lyra.Core.Blocks;
using Lyra.Data.API;
using Lyra.Data.API.WorkFlow;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests.OTC
{
    [TestClass]
    public class UT_CancelTrade : XOTCTestBase
    {
        private static bool done = false;
        public async Task Setup()
        {
                await SetupWallets("devnet");

                await SetupEventsListener();
        }

        [TestMethod]
        public async Task TestCancelling()
        {
            await Setup();

            var order = await CreateOrder();
            Assert.IsNotNull(order);

            var trade = await CreateTrade(order);
            Assert.IsNotNull(trade);

            await CancelTrade(trade);

            await CloseOrder(order);
        }

        [TestMethod]
        public async Task TestDisputeRaise()
        {
            await Setup();

            var order = await CreateOrder();
            Assert.IsNotNull(order);

            var trade = await CreateTrade(order);
            Assert.IsNotNull(trade);

            await CancelTrade(trade);

            await CloseOrder(order);
        }
    }
}
