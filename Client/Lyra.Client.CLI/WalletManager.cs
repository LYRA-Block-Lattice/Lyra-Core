using System;
using System.IO;

using System.Threading;
using System.Threading.Tasks;

using Lyra.Core.Blocks;
using Lyra.Core.Accounts;

using Lyra.Core.LiteDB;
using System.Net.Http;
using Lyra.Core.API;
using Microsoft.Extensions.Hosting;
using Lyra.Core.Cryptography;

namespace Lyra.Client.CLI
{
    public class WalletManager
    {
        Boolean timer_busy1;

        Timer timer1;

        public async Task<int> RunWallet(Options options)
        {
            Console.WriteLine("Personal and Business Banking, Payments, and Digital Asset Management");
            Console.WriteLine("");
            Console.WriteLine("Banking: Store, transfer, and receive interest on multiple digital assets");
            Console.WriteLine("Payments: Make or accept instant payments using various currencies, online and in store");
            Console.WriteLine("Digital Asset Management: Issue your own tokens within seconds");
            Console.WriteLine("");

            string network_id = options.NetworkId;
            bool INMEMORY = options.Database == Options.INMEMORY_DATABASE;
            bool WEB = options.Protocol == Options.WEBAPI_PROTOCOL;

            Wallet wallet;
            if (INMEMORY)
            {
                var inmemory_storage = new AccountInMemoryStorage();
                wallet = new Wallet(inmemory_storage, network_id);
            }
            else
            {
                wallet = new Wallet(new LiteAccountDatabase(), network_id);
            }

            string lyra_folder = BaseAccount.GetFullFolderName(network_id, "wallets");
            if (!Directory.Exists(lyra_folder))
                Directory.CreateDirectory(lyra_folder);

            Console.WriteLine("Storage Location: " + lyra_folder);

            if(options.GenWalletName != null)
            {
                wallet.AccountName = options.GenWalletName;
                wallet.CreateAccount(lyra_folder, wallet.AccountName, AccountTypes.Standard);
                var ep = Neo.Cryptography.ECC.ECPoint.FromBytes(Base58Encoding.DecodeAccountId(wallet.AccountId), Neo.Cryptography.ECC.ECCurve.Secp256r1);
                Console.WriteLine($"The new wallet {wallet.AccountName} for {network_id}: ");
                Console.WriteLine(ep.ToString());
                Console.WriteLine(wallet.AccountId);
                return 0;
            }

            CommandProcessor command = new CommandProcessor(wallet);
            string input = null;
            try
            {
                while (!wallet.AccountExistsLocally(lyra_folder, input))
                {
                    Console.WriteLine("Press Enter for default account, or enter account name: ");
                    input = Console.ReadLine();

                    if (string.IsNullOrEmpty(input))
                        input = "My Account";

                    wallet.AccountName = input;

                    string fileName = "";
                    if (INMEMORY)
                    {
                        fileName = lyra_folder + wallet.AccountName + ".key";

                        if (System.IO.File.Exists(fileName))
                        {
                            string private_key = System.IO.File.ReadAllText(fileName);
                            if (wallet.ValidatePrivateKey(private_key))
                            {
                                var result = wallet.RestoreAccount(lyra_folder, private_key);
                                if (!result.Successful())
                                {
                                    Console.WriteLine("Could not restore account from file: " + result.ResultMessage);
                                    continue;
                                }
                            }
                        }
                    }


                    if (!wallet.AccountExistsLocally(lyra_folder, wallet.AccountName))
                    {
                        Console.WriteLine("Local account data not found. Would you like to create a new account? (Y/n): ");
                        if (command.ReadYesNoAnswer())
                        {
                            wallet.CreateAccount(lyra_folder, wallet.AccountName, AccountTypes.Standard);
                        }
                        else
                        {
                            Console.WriteLine("Please enter private key to restore account: ");
                            string privatekey = Console.ReadLine();

                            if (!wallet.ValidatePrivateKey(privatekey))
                                continue;

                            var result = wallet.RestoreAccount(lyra_folder, privatekey);
                            if (!result.Successful())
                            {
                                Console.WriteLine("Could not restore account from file: " + result.ResultMessage);
                                continue;
                            }
                        }
                        if (INMEMORY)
                        {
                            System.IO.File.WriteAllText(fileName, wallet.PrivateKey);
                        }

                    }
                    else
                        wallet.OpenAccount(lyra_folder, wallet.AccountName);
                }

                LyraRestClient rpcClient;
                if (!string.IsNullOrWhiteSpace(options.Node))
                {
                    var apiUrl = $"https://{options.Node}:4505/api/Node/";
                    rpcClient = await LyraRestClient.CreateAsync(network_id, "Windows", $"{LyraGlobal.PRODUCTNAME} Client Cli", "1.0a", apiUrl);
                }
                else
                    rpcClient = await LyraRestClient.CreateAsync(network_id, "Windows", $"{LyraGlobal.PRODUCTNAME} Client Cli", "1.0a");//await LyraRpcClient.CreateAsync(network_id, "Lyra Client Cli", "1.0");

                Console.WriteLine("Type 'help' to see the list of available commands");
                Console.WriteLine("");

                await wallet.Sync(rpcClient);

                //timer1 = new Timer(async _ =>
                //{
                //    if (timer_busy1)
                //        return;
                //    try
                //    {
                //        timer_busy1 = true;
                //        var sync_result = await wallet.Sync(rpcClient);
                //    }
                //    finally
                //    {
                //        timer_busy1 = false;
                //    }
                //},
                //null, 2000, 30000);


                input = CommandProcessor.COMMAND_STATUS;

                while (input != CommandProcessor.COMMAND_STOP)
                {
                    var result = await command.Execute(input);
                    Console.Write(string.Format("{0}> ", wallet.AccountName));
                    //Console.Write
                    input = Console.ReadLine();
                }

                Console.WriteLine($"{LyraGlobal.PRODUCTNAME} Client is shutting down");
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Exception: {0}", ex.Message));
                Console.WriteLine($"{LyraGlobal.PRODUCTNAME} Client is shutting down");
            }
            finally
            {
                if (wallet != null)
                    wallet.Dispose();
            }

            return 0;
        }
    }
}
