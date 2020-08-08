using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Cryptography;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

        public async Task MultiThreadedSendAsync(string[] masterKeys, string[] targetAddrs, Dictionary<string, decimal> amounts, bool oneTime = false)
        {
            var multiThreadBatch = masterKeys.Length;
            Console.WriteLine($"Multiple Thread Test for {multiThreadBatch} Send.");

            var dtStart = DateTime.Now;

            var threads = new List<Task>();
            var rand = new Random();

            foreach (var masterKey in masterKeys)
            {
                var tsk = Task.Run(async () =>
                {
                    var fromWallet = await RefreshBalanceAsync(masterKey);
                    var block = fromWallet.GetLatestBlock();
                    if (block == null || block.Balances == null)
                    {
                        Console.WriteLine("No last block!");
                    }
                    else
                    {
                        while (true)
                        {
                            foreach (var wt in targetAddrs)
                            {
                                foreach (var amount in amounts)
                                {
                                    if (block.Balances.ContainsKey(amount.Key) && block.Balances[amount.Key].ToBalanceDecimal() > amount.Value)
                                    {
                                        //var stopwatch = Stopwatch.StartNew();
                                        var result = await fromWallet.Send(amount.Value, wt, amount.Key);
                                        //stopwatch.Stop();
                                        //Console.WriteLine($"Send: {stopwatch.ElapsedMilliseconds} ms. Result: {result.ResultCode}");

                                        if (result.ResultCode != Lyra.Core.Blocks.APIResultCodes.Success)
                                        {
                                            Console.WriteLine($"Error: {result.ResultCode} Quit Thread.");
                                            oneTime = true;
                                            break;
                                        }

                                        await fromWallet.Sync(null);
                                    }
                                    else
                                    {
                                        oneTime = true;
                                        break;
                                    }
                                }
                            }
                            if (oneTime)
                                break;
                        }

                    }
                });
                await Task.Delay(200);
                threads.Add(tsk);
            }

            Task.WaitAll(threads.ToArray());


        }

        private async Task<Wallet> RefreshBalanceAsync(string masterKey)
        {
            throw new NotImplementedException();
            //// create wallet and update balance
            //var memStor = new AccountInMemoryStorage();
            //var acctWallet = new ExchangeAccountWallet(memStor, Program.network_id);
            ////acctWallet.AccountName = "tmpAcct";
            ////acctWallet.RestoreAccount("", masterKey);
            ////acctWallet.OpenAccount("", acctWallet.AccountName);

            //Console.WriteLine("Sync wallet for " + acctWallet.AccountId);
            //var rpcClient = LyraRestClient.Create(Program.network_id, "Windows", $"{LyraGlobal.PRODUCTNAME} Client Cli", "1.0a");
            //await acctWallet.Sync(rpcClient);
            //return acctWallet;
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
                            balance = block.Balances.ToDictionary(p => p.Key, p => p.Value.ToBalanceDecimal())
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
