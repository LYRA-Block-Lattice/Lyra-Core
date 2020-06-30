using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Lyra.Core.Blocks;
using Lyra.Core.Accounts;

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
        const string PRIVATE_KEY_1 = "25kksnE589CTHcDeMNbatGBGoCjiMNFzcDCuGULj1vgCMAfxNV"; // merchant
        const string PRIVATE_KEY_2 = "2QvkckNTBttTt9EwsvWhDCwibcvzSkksx5iBuikh1AzgdYsNov"; // customer

        const string NETWORK_ID = "unittest";

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

        // "one-time" test - only works once after network reset  
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

        // "one-time" test - only works once after network reset  
        [TestMethod]
        public void TestMethod_Create_CUSTOM_Token()
        {
            var inmemory_storage = new AccountInMemoryStorage();
            Wallet wallet = new Wallet(inmemory_storage, NETWORK_ID);
            wallet.RestoreAccount("", PRIVATE_KEY_1);

            var node = new RPCClient("TestAccount1");
            var result = wallet.Sync(node).Result;
            Assert.AreEqual(APIResultCodes.Success, result);
                        
            result = wallet.CreateToken("USD", "UnitTest", "", 8, 1000000, true, "Slava", "", "", Core.Blocks.Transactions.ContractTypes.Custom, null).Result.ResultCode;

            Assert.AreEqual(APIResultCodes.Success, result);
        }

        // "one-time" test - only works once after network reset  
        [TestMethod]
        public void TestMethod_Create_REWARDS_Token()
        {
            Wallet wallet = GetWallet(PRIVATE_KEY_1);
            var result = wallet.CreateToken("rewards", "rewards", "", 2, 1000000, false, "Slava", "", "", Core.Blocks.Transactions.ContractTypes.Custom, null).Result.ResultCode;
            Assert.AreEqual(APIResultCodes.Success, result);
        }

        // "one-time" test - only works once after network reset  
        [TestMethod]
        public void TestMethod_Create_DISCOUNT_Token()
        {
            Wallet wallet = GetWallet(PRIVATE_KEY_1);
            var result = wallet.CreateToken("discounts", "discounts", "", 2, 1000000, false, "Slava", "", "", Core.Blocks.Transactions.ContractTypes.Custom, null).Result.ResultCode;
            Assert.AreEqual(APIResultCodes.Success, result);
        }

        public static Wallet GetWallet(string PrivateKey)
        {
            var inmemory_storage = new AccountInMemoryStorage();
            Wallet wallet = new Wallet(inmemory_storage, NETWORK_ID);
            wallet.RestoreAccount("", PrivateKey);

            var node = new RPCClient(PrivateKey);
            var result = wallet.Sync(node).Result;
            if (result == APIResultCodes.Success)
                return wallet;
            throw new Exception("Could not create wallet"); 
        }
    }
}
