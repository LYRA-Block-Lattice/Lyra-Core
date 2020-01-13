using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Cryptography;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Friday
{
    public class WalletBalance
    {
        public string privateKey { get; set; }
        public string pulicKey { get; set; }
        public Dictionary<string, decimal> balance { get; set; }
    }
    /// <summary>
    /// the master wallet is "My Account", which has 100M Lyra.Coin and 1000B Friday.Coin
    /// so we create 1000 wallet named [Friday1-Friday1000], transfer 1000 Friday.Coin and 10 Lyra.Coin to it.
    /// </summary>
    public class TransactionTester
    {
        LyraRestClient _client;
        public TransactionTester(LyraRestClient client)
        {
            _client = client;
        }
        public Dictionary<string, string> CreateWallet(int count)
        {
            var dict = new Dictionary<string, string>();
            for(int i = 1; i <= count; i++)
            {
                var nw = Signatures.GenerateWallet();
                dict.Add(nw.AccountId, nw.privateKey);                
            }
            return dict;
        }

        public async Task SingleThreadedSendAsync(int singleThreadBatch, Wallet masterWallet, string[] targetAddrs, Dictionary<string, decimal> amounts)
        {
            Console.WriteLine($"Single Thread Test for {singleThreadBatch} Send.");
            var dtStart = DateTime.Now;
            for (int i = 0; i < singleThreadBatch; i++)
            {
                var wt = targetAddrs[i];
                foreach(var amount in amounts)
                {
                    var result = await masterWallet.Send(amount.Value, wt, amount.Key);
                    Console.WriteLine($"Trans {i}: {result.ResultCode}");
                }
            }
            var dtEnd = DateTime.Now;
            Console.WriteLine($"Single thread, {singleThreadBatch} Send, Avg: {(dtEnd - dtStart).TotalSeconds / singleThreadBatch}");
        }

        public async Task MultiThreadedSendAsync(string[] masterKeys, string[] targetAddrs, Dictionary<string, decimal> amounts)
        {
            var multiThreadBatch = masterKeys.Length;
            Console.WriteLine($"Multiple Thread Test for {multiThreadBatch} Send.");
            var dtStart = DateTime.Now;

            var fromWallets = new List<Wallet>();
            foreach(var masterKey in masterKeys)
            {
                fromWallets.Add(await RefreshBalanceAsync(masterKey));
            }

            Console.WriteLine($"Sync balance takes {(DateTime.Now - dtStart).TotalSeconds} seconds");

            dtStart = DateTime.Now;
            var threads = new List<Task>();

            for(int i = 0; i < fromWallets.Count; i++)
            {
                var fromWallet = fromWallets[i];
                var start = i * 10;
                var tsk = Task.Run(async () =>
                {
                    for (int j = start; j < start + 10; j++)
                    {
                        var wt = targetAddrs[j];
                        foreach (var amount in amounts)
                        {
                            var result = await fromWallet.Send(amount.Value, wt, amount.Key);
                            Console.WriteLine($"Trans {i}: {result.ResultCode}");
                        }
                    }
                });
                threads.Add(tsk);
            }

            Task.WaitAll(threads.ToArray());

            var dtEnd = DateTime.Now;
            Console.WriteLine($"Multiple thread, {multiThreadBatch} Send, Avg: {(dtEnd - dtStart).TotalSeconds / multiThreadBatch}");
        }

        private async Task<Wallet> RefreshBalanceAsync(string masterKey)
        {
            // create wallet and update balance
            var memStor = new AccountInMemoryStorage();
            var acctWallet = new ExchangeAccountWallet(memStor, "testnet");
            acctWallet.AccountName = "tmpAcct";
            await acctWallet.RestoreAccountAsync("", masterKey);
            acctWallet.OpenAccount("", acctWallet.AccountName);

            Console.WriteLine("Sync wallet for " + acctWallet.AccountId);
            await acctWallet.Sync(_client);
            return acctWallet;
        }

        public async Task<List<WalletBalance>> RefreshBalancesAsync(string[] masterKeys)
        {
            var threads = new List<Task>();
            var blances = new List<WalletBalance>();

            foreach (var mk in masterKeys)
            {
                var tsk = Task.Run(async () =>
                {
                    var wallet = await RefreshBalanceAsync(mk);
                    var block = wallet.GetLatestBlock();
                    if(block != null)
                        blances.Add(new WalletBalance
                        {
                            privateKey = wallet.PrivateKey,
                            pulicKey = wallet.AccountId,
                            balance = block.Balances
                        });
                });
                threads.Add(tsk);
                await Task.Delay(100);
            }

            Task.WaitAll(threads.ToArray());

            return blances;
        }
    }
}
