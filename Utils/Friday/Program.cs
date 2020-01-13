using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.LiteDB;
using Newtonsoft.Json;
using System;
using System.IO;

namespace Friday
{
    class Program
    {
        // args: [number] the tps to simulate
        // 
        static async System.Threading.Tasks.Task Main(string[] args)
        {
            var workingFolder = @"C:\working\Friday";
            var network_id = "testnet";
            var lyraFolder = BaseAccount.GetFullFolderName("Lyra-CLI-" + network_id);

            // create and save wallets
            //var tt = new TransactionTester();
            //var wlts = tt.CreateWallet(1000);
            //var json = JsonConvert.SerializeObject(wlts);
            //File.WriteAllText(workingFolder + @"\wallets.json", json);

            var rpcClient = await LyraRestClient.CreateAsync(network_id, "Windows", "Lyra Client Cli", "1.0a");

            var masterWallet = new Wallet(new LiteAccountDatabase(), network_id);
            masterWallet.AccountName = "My Account";
            masterWallet.OpenAccount(BaseAccount.GetFullPath(lyraFolder), masterWallet.AccountName);

            await masterWallet.Sync(rpcClient);

            foreach(var b in masterWallet.GetLatestBlock().Balances)
            {
                Console.WriteLine($"{b.Key}: {b.Value}");
            }
            Console.WriteLine("Hello World!");
        }
    }
}
