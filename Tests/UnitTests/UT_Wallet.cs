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

namespace UnitTests
{
    [TestClass]
    public class UT_Wallet
    {
        const string PRIVATE_KEY_1 = "25kksnE589CTHcDeMNbatGBGoCjiMNFzcDCuGULj1vgCMAfxNV"; // merchant
        const string PRIVATE_KEY_2 = "2QvkckNTBttTt9EwsvWhDCwibcvzSkksx5iBuikh1AzgdYsNov"; // customer

        const string ADDRESS_ID_1 = "L4hksrWP5pzQ4pdDdUZ4D9GgZoT3iGZiaWNgcTPjSUATyogyJaZk1qYHfKuMnTytqfqEp3fgWQ7NxoQXVZPykqj2ALWejo";
        const string ADDRESS_ID_2 = "LPR9pZeLhB4eHHuQBEDLTVoAJUZUWNbfux2QpSvK6vJbcXsGK6Rz3gN3ynNixcz9yAaA9iLCEJ7c5oQobQpUS66vPtZ2Yq";

        public static Wallet Restore(string privateKey)
        {
            var memStor = new AccountInMemoryStorage();
            try
            {
                Wallet.Create(memStor, "tmpAcct", "", LyraNodeConfig.GetNetworkId(), privateKey);
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
    }
}
