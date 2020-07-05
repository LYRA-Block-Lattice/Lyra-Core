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
        public static readonly DagSystem TheDagSystem;

        static TestBlockChain()
        {
            Console.WriteLine("initialize DagSystem");

            SimpleLogger.Factory = new NullLoggerFactory();

            var mockStore = new Mock<IAccountCollectionAsync>();
            var posWallet = Restore("25kksnE589CTHcDeMNbatGBGoCjiMNFzcDCuGULj1vgCMAfxNV");

            TheDagSystem = new DagSystem("xtest", mockStore.Object, posWallet);

            // Ensure that blockchain is loaded

            var _ = BlockChain.Singleton;
        }

        public static void InitializeMockDagSystem()
        {
        }

        public static Wallet Restore(string privateKey)
        {
            var memStor = new AccountInMemoryStorage();
            var acctWallet = new ExchangeAccountWallet(memStor, LyraNodeConfig.GetNetworkId());
            acctWallet.AccountName = "tmpAcct";
            var result = acctWallet.RestoreAccount("", privateKey);
            if (result.ResultCode == Lyra.Core.Blocks.APIResultCodes.Success)
            {
                acctWallet.OpenAccount("", acctWallet.AccountName);
                return acctWallet;
            }
            else
            {
                return null;
            }
        }
    }
}
