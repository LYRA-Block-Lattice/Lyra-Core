using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
//
using Lyra.Client.Lib;
using Lyra.Client.InMemory;
using Lyra.Core.API;
using Lyra.Client.RPC;
using Lyra.Core.Blocks;


namespace Lyra.WalletTest
{
    //public static class ShellHelper
    //{
    //    public static void Bash(this string cmd)
    //    {
    //        var escapedArgs = cmd.Replace("\"", "\\\"");

    //        var process = new Process()
    //        {
    //            StartInfo = new ProcessStartInfo
    //            {
    //                FileName = "/bin/bash",
    //                Arguments = $"-c \"{escapedArgs}\"",
    //                RedirectStandardOutput = false,
    //                UseShellExecute = false,
    //                CreateNoWindow = false,
    //            }
    //        };
    //        process.Start();
    //        //string result = process.StandardOutput.ReadToEnd();
    //        //process.WaitForExit();
    //        //return result;
    //    }
    //}

    [TestClass]
    public class WalletGenericTest
    {
        const string PRIVATE_KEY_1 = "25kksnE589CTHcDeMNbatGBGoCjiMNFzcDCuGULj1vgCMAfxNV";
        const string PRIVATE_KEY_2 = "2QvkckNTBttTt9EwsvWhDCwibcvzSkksx5iBuikh1AzgdYsNov";

        const string NETWORK_ID = "local";

        public WalletGenericTest()
        {
            //ShellHelper.Bash("Users/slava/Projects/Lyra/unittest.command");
           // ShellHelper.Bash("$HOME/Projects/Lyra/unittest.command");
            
        }

        [TestMethod]
        public void TestMethod_Account_1_Restore_Success()
        {
            var inmemory_storage = new AccountInMemoryStorage();
            Wallet wallet = new Wallet(inmemory_storage, NETWORK_ID);
            var result = wallet.RestoreAccount("", PRIVATE_KEY_1);
            // Assert.IsFalse(result.Successful());
            Assert.AreEqual(APIResultCodes.Success, result.ResultCode);
        }

        [TestMethod]
        public void TestMethod_Account_2_Restore_Success()
        {
            var inmemory_storage = new AccountInMemoryStorage();
            Wallet wallet = new Wallet(inmemory_storage, NETWORK_ID);
            var result = wallet.RestoreAccount("", PRIVATE_KEY_2);
            // Assert.IsFalse(result.Successful());
            Assert.AreEqual(APIResultCodes.Success, result.ResultCode);
        }

        [TestMethod]
        public void TestMethod_Account_1_Sync_Success()
        {
            var inmemory_storage = new AccountInMemoryStorage();
            Wallet wallet = new Wallet(inmemory_storage, NETWORK_ID);
            wallet.RestoreAccount("", PRIVATE_KEY_1);

            var node = new RPCClient("TestAccount1");
            var result = wallet.Sync(node).Result;
            Assert.AreEqual(APIResultCodes.Success, result);

            var balances = wallet.GetDisplayBalances();
            Assert.IsNotNull(balances);

            Console.WriteLine(balances);
        }

        [TestMethod]
        public void TestMethod_Account_2_Sync_Success()
        {
            var inmemory_storage = new AccountInMemoryStorage();
            Wallet wallet = new Wallet(inmemory_storage, NETWORK_ID);
            wallet.RestoreAccount("", PRIVATE_KEY_2);

            var node = new RPCClient("TestAccount2");
            var result = wallet.Sync(node).Result;
            Assert.AreEqual(APIResultCodes.Success, result);

        }

        [TestMethod]
        public void TestMethod_Create_LYRA_Token()
        {
            var inmemory_storage = new AccountInMemoryStorage();
            Wallet wallet = new Wallet(inmemory_storage, NETWORK_ID);
            wallet.RestoreAccount("", PRIVATE_KEY_1);

            var node = new RPCClient("TestAccount1");
            var result = wallet.Sync(node).Result;
            Assert.AreEqual(APIResultCodes.Success, result);

            result = wallet.CreateGenesisForCoreToken();
            Assert.AreEqual(APIResultCodes.Success, result);
        }

        [TestMethod]
        public void TestMethod_Create_CUSTOM_Token()
        {
            var inmemory_storage = new AccountInMemoryStorage();
            Wallet wallet = new Wallet(inmemory_storage, NETWORK_ID);
            wallet.RestoreAccount("", PRIVATE_KEY_1);

            var node = new RPCClient("TestAccount1");
            var result = wallet.Sync(node).Result;
            Assert.AreEqual(APIResultCodes.Success, result);
                        
            result = wallet.CreateToken("Custom.USD", "Custom", "", 8, 1000000, true, "Slava", "", "", null).Result.ResultCode;

            Assert.AreEqual(APIResultCodes.Success, result);
        }
    }
}
