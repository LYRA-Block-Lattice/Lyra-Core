using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Cryptography;
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
        static string testCoin = "Friday/Coin";
        static string lyraCoin = "LYR";
        public static string network_id = "testnet";

        // args: [number] the tps to simulate
        // 
        static async Task Main(string[] args)
        {
            var workingFolder = @"C:\working\Friday";
                      
            var lyraFolder = Wallet.GetFullFolderName(network_id, "wallets");

            Console.WriteLine("Press enter to begin.");
            Console.ReadLine();

            var rpcClient = LyraRestClient.Create(network_id, "Windows", $"{LyraGlobal.PRODUCTNAME} Client Cli", "1.0a");
            //var rpcClient = await LyraRestClient.CreateAsync(network_id, "Windows", "Lyra Client Cli", "1.0a", "http://192.168.3.62:4505/api/Node/");

            // create and save wallets
            var tt = new TransactionTester(rpcClient);
            //var wlts = tt.CreateWallet(1000);
            //var json = JsonConvert.SerializeObject(wlts);
            //File.WriteAllText(workingFolder + @"\wallets.json", json);

            //return;

            // key is account id
            var wallets = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(workingFolder + @"\\wallets.json"));

            //var tt = new TransactionTester(rpcClient);

            var secureStorage = new SecuredFileStore(lyraFolder);
            var masterWallet = Wallet.Open(secureStorage, "My Account", "");

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

            var all = await tt.RefreshBalancesAsync(wallets.Select(a => a.Value).ToArray());
            File.WriteAllText(workingFolder + @"\balances.json", JsonConvert.SerializeObject(all));

            var rich10 = JsonConvert.DeserializeObject<List<WalletBalance>>(File.ReadAllText(workingFolder + @"\balances.json"));
            var realRich10 = rich10.Where(a => a.balance.ContainsKey(lyraCoin) && a.balance.ContainsKey(testCoin))
                .Where(a => a.balance[testCoin] >= 10000).ToDictionary(a => a.privateKey, a => a.balance);

            var rich90 = wallets.Where(a => !realRich10.ContainsKey(a.Value)).Take(90);
            File.WriteAllText(workingFolder + @"\rich90.json", JsonConvert.SerializeObject(rich90));

            //var rich90 = JsonConvert.DeserializeObject<List<KeyValuePair<string, string>>>(File.ReadAllText(workingFolder + @"\\rich90.json"));

            var poors = wallets.Where(a => !rich90.Any(x => x.Key == a.Key));

            var testGroup1 = rich90.Take(50);
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
