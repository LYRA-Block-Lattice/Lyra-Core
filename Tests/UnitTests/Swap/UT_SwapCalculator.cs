using Lyra.Core.Blocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using Lyra.Core.API;

namespace UnitTests.Swap
{
    [TestClass]
    public class UT_SwapCalculator
    {
        [TestMethod]
        public void Calculate()
        {
            var X = 10000m; // LYR
            var Y = 20000m;  // dai

            var fakeBlock = new SendTransferBlock();
            fakeBlock.Balances = new Dictionary<string, long>();
            fakeBlock.Balances.Add("LYR", X.ToBalanceLong());
            fakeBlock.Balances.Add("DAI", Y.ToBalanceLong());

            var u = 1000m;       // dai
            var pureTo = X - (X * Y / (Y + u * 0.997m));
            var to = Math.Round(pureTo * 0.999m, 8);
            var chg = Math.Round(to / X, 6);
            var price = Math.Round(to / u, 10);
            Console.WriteLine($"Price {price} Got {to} X, Price Impact: {chg * 100:n} %");

            var cal1 = new SwapCalculator("LYR", "DAI", fakeBlock, "DAI", u, 0);
            Assert.AreEqual(to, cal1.SwapOutAmount);
            Assert.AreEqual("LYR", cal1.SwapOutToken);

            var u2 = 100000;       // eth

            var pureTo2 = Y - (X * Y / (X + u2 * 0.996m));
            var to2 = Math.Round(pureTo2, 8);

            var chg2 = Math.Round(to2 / Y, 6);

            var price2 = Math.Round(to2 / u2, 10);

            var cal2 = new SwapCalculator("LYR", "DAI", fakeBlock, "LYR", u2, 0);
            Assert.AreEqual(to2, cal2.SwapOutAmount);
            Assert.AreEqual("DAI", cal2.SwapOutToken);

            Console.WriteLine($"Price {price2} Got {to2} X, Price Impact: {chg2 * 100:n} %");
        }
    }
}
