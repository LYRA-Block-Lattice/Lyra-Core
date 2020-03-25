using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Cryptography;
using Lyra.Core.LiteDB;
using Neo.Wallets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Friday
{
    public class Program
    {
        static string testCoin = "Friday.Coin";
        static string lyraCoin = "Lyra.Coin";
        public static string network_id = "devnet";

        // args: [number] the tps to simulate
        // 
        static async System.Threading.Tasks.Task Main(string[] args)
        {
            var workingFolder = @"C:\working\Friday";
                      
            var lyraFolder = BaseAccount.GetFullFolderName("Lyra-CLI-" + network_id);

            Console.WriteLine("Press enter to begin.");
            Console.ReadLine();

            // create and save wallets
            //var tt = new TransactionTester();
            //var wlts = tt.CreateWallet(1000);
            //var json = JsonConvert.SerializeObject(wlts);
            //File.WriteAllText(workingFolder + @"\wallets.json", json);

            // key is account id
            var wallets = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(workingFolder + @"\\wallets.json"));

            //var rpcClient = await LyraRestClient.CreateAsync(network_id, "Windows", "Lyra Client Cli", "1.0a", "https://192.168.3.62:4505/api/LyraNode/");
            var rpcClient = await LyraRestClient.CreateAsync(network_id, "Windows", "Lyra Client Cli", "1.0a");
            var tt = new TransactionTester(rpcClient);

            var masterWallet = new Wallet(new LiteAccountDatabase(), network_id);
            masterWallet.AccountName = "My Account";
            masterWallet.OpenAccount(lyraFolder, masterWallet.AccountName);
            await masterWallet.Sync(rpcClient);

            _ = Task.Run(async () =>
              {
                  while (true)
                  {
                      var state = await rpcClient.GetSyncState();
                      await Task.Delay(10000);
                      var state2 = await rpcClient.GetSyncState();

                      var tps = state2.Status.totalBlockCount - state.Status.totalBlockCount;

                      Console.WriteLine($"\n============> TPS: {tps} / 10\n");
                  }
              });

            //var all = await tt.RefreshBalancesAsync(wallets.Select(a => new KeyPair(Base58Encoding.DecodePrivateKey(a.Value))).ToArray());
            //File.WriteAllText(workingFolder + @"\balances.json", JsonConvert.SerializeObject(all));

            var rich10 = JsonConvert.DeserializeObject<List<WalletBalance>>(File.ReadAllText(workingFolder + @"\balances.json"));
            var realRich10 = rich10.Where(a => a.balance.ContainsKey(lyraCoin) && a.balance.ContainsKey(testCoin))
                .Where(a => a.balance[testCoin] >= 10000).ToDictionary(a => a.privateKey, a => a.balance);

            //var rich90 = wallets.Where(a => !realRich10.ContainsKey(a.Value)).Take(90);
            var rich90 = JsonConvert.DeserializeObject<List<KeyValuePair<string, string>>>(File.ReadAllText(workingFolder + @"\\rich90.json"));
            //File.WriteAllText(workingFolder + @"\rich90.json", JsonConvert.SerializeObject(rich90));

            var poors = wallets.Where(a => !rich90.Any(x => x.Key == a.Key));

            var testGroup1 = rich90.Take(350);
            await tt.MultiThreadedSendAsync(new [] { masterWallet.PrivateKey }, testGroup1.Select(a => a.Key).ToArray(), new Dictionary<string, decimal> { { lyraCoin, 5000 } }, true);

            Console.WriteLine("Coin distribute OK. Press Enter to continue...");
            Console.ReadLine();
            
            await tt.MultiThreadedSendAsync(testGroup1.Select(a => a.Value).ToArray(), poors.Select(a => a.Key).ToArray(), new Dictionary<string, decimal> { { lyraCoin, 1 } });

            Console.ReadLine();

            //foreach(var b in masterWallet.GetLatestBlock().Balances)
            //{
            //    Console.WriteLine($"{b.Key}: {b.Value}");
            //}
            //Console.WriteLine("Hello Lyra!");

            //var top10 = wallets.Take(10).ToDictionary(a => a.Key, a => a.Value);

            //await tt.SingleThreadedSendAsync(10, masterWallet, top10.Keys.ToArray(), new Dictionary<string, decimal> {
            //    { lyraCoin, 10000 }, {testCoin, 1000000}
            //});

            //var top100 = wallets.Skip(10).Take(100).ToDictionary(a => a.Key, a => a.Value);
            //await tt.MultiThreadedSendAsync(10, top10.Select(a => new KeyPair(Base58Encoding.DecodePrivateKey(a.Value))).ToArray(),
            //    top100.Values.ToArray(), new Dictionary<string, decimal> {
            //        { lyraCoin, 100 }, {testCoin, 10000} }
            //    );
        }
    }
}
