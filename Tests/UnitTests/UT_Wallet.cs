using FluentAssertions;
using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Data.Crypto;
using Lyra.Core.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using Lyra.Data.Utils;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class UT_Wallet
    {
        const string PRIVATE_KEY_1 = "2gbESTeBHsgt8um1aNN2dC9jajEDk3CoEupwmN6TRJQckyRbHa"; // merchant
        const string PRIVATE_KEY_2 = "KufWHKVUxqCjBVunJXqqpPBkajdxb4mKLbYZYFnxDNXhUsnCT"; // customer

        public const string ADDRESS_ID_1 = "LUTkgGP9tb4iAFAFXv7i83N4GreEUakWbaDrUbUFnKHpPp46n9KF1QzCtvUwZRBCQz6yqerkWvvGXtCTkz4knzeKRmqid";
        const string ADDRESS_ID_2 = "LUTDSh9xEn21ZDjgGQ9g9g1zd9JxhY2rEoqH9kh8E3EwHk76jP6x24iYaT64HG3zEznZqptK88Y6nM1zz9NbxRWt45XdBx";

        public static Wallet Restore(string privateKey)
        {
            var memStor = new AccountInMemoryStorage();
            try
            {
                Wallet.Create(memStor, "tmpAcct", "", TestConfig.networkId, privateKey);
                return Wallet.Open(memStor, "tmpAcct", "");
            }
            catch (Exception)
            {
                return null;
            }
        }

        [TestMethod]
        public void WalletCreateNewTest()
        {
            var keypair = Signatures.GenerateWallet();
            keypair.AccountId.Should().StartWith(LyraGlobal.ADDRESSPREFIX.ToString());
        }

        [TestMethod]
        public void WalletRestoreTest()
        {
            var wallet1 = Restore("");
            wallet1.Should().BeNull();

            var wallet2 = Restore(PRIVATE_KEY_1);
            wallet2.AccountId.Should().Be(ADDRESS_ID_1);
        }

        //[TestMethod]
        //public async Task TestUnsettlementAsync()
        //{
        //    var lcx = LyraRestClient.Create("mainnet", Environment.OSVersion.ToString(), "Nebula", "1.4");
        //    var txs = await lcx.SearchTransactionsAsync("LRsirJCFKsA9F5KmUeGQuRxu98fA8xpaHyLqvp5pvXfSVBYGividVK6mV4YZwFUBFU8WhvqWdXk4BWe1bye7PYiprRGQBD", DateTime.UtcNow.AddDays(-2).Ticks, DateTime.UtcNow.Ticks, 10);
        //    if(txs.Successful())
        //    {

        //    }
        //}
    }
}
