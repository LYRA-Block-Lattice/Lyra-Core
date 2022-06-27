//using Lyra.Core.Blocks;
//using Lyra.Data.API;
//using Lyra.Data.API.WorkFlow;
//using Microsoft.VisualStudio.TestTools.UnitTesting;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace UnitTests.OTC
//{
//    [TestClass]
//    public class UT_RaiseDispute : XOTCTestBase
//    {
//        private static bool done = false;
//        [TestInitialize]
//        public async Task Setup()
//        {
//            if (!done)
//            {
//                await SetupWallets("devnet");

//                await SetupEventsListener();

//                done = true;
//            }
//        }

//        [TestMethod]
//        public async Task TestDisputeRaise()
//        {
//            var order = await CreateOrder();
//            Assert.IsNotNull(order);

//            var trade = await CreateTrade(order);
//            Assert.IsNotNull(trade);

//            await CancelTrade(trade);

//            await CloseOrder(order);
//        }
//    }
//}
