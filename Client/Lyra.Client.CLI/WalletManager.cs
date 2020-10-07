using System;
using System.IO;

using System.Threading;
using System.Threading.Tasks;

using Lyra.Core.Blocks;
using Lyra.Core.Accounts;
using System.Net.Http;
using Lyra.Core.API;
using Microsoft.Extensions.Hosting;
using Lyra.Data.Crypto;
using Lyra.Core.Utils;

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

            Wallet wallet = null;
            string lyra_folder = Wallet.GetFullFolderName(network_id, "wallets");
            IAccountDatabase storage;
            if (INMEMORY)
            {
                storage = new AccountInMemoryStorage();
            }
            else
            {
                storage = new SecuredWalletStore(lyra_folder);
            }

            if (!Directory.Exists(lyra_folder))
                Directory.CreateDirectory(lyra_folder);

            Console.WriteLine("Storage Location: " + lyra_folder);

            if(options.GenWalletName != null)
            {
                Console.WriteLine("Please input a password:");
                var password = Console.ReadLine();

                (var privateKey, var publicKey) = Signatures.GenerateWallet();

                Console.WriteLine($"The new wallet {options.GenWalletName} for {network_id}: ");
                Console.WriteLine(privateKey);
                Console.WriteLine(publicKey);
                var secureFile = new SecuredWalletStore(lyra_folder);
                secureFile.Create(options.GenWalletName, password, network_id, privateKey, publicKey, "");

                return 0;
            }

            CommandProcessor command = new CommandProcessor();
            string walletName = null;
            string walletPassword = null;
            try
            {
                while (!File.Exists($"{lyra_folder}{Path.DirectorySeparatorChar}{walletName}{LyraGlobal.WALLETFILEEXT}"))
                {
                    Console.WriteLine("Press Enter for default account, or enter account name: ");
                    walletName = Console.ReadLine();

                    if (string.IsNullOrEmpty(walletName))
                        walletName = "My Account";

                    string fileName = "";
                    //if (INMEMORY)
                    //{
                    //    fileName = lyra_folder + wallet.AccountName + ".key";

                    //    if (System.IO.File.Exists(fileName))
                    //    {
                    //        string private_key = System.IO.File.ReadAllText(fileName);
                    //        if (wallet.ValidatePrivateKey(private_key))
                    //        {
                    //            var result = wallet.RestoreAccount(lyra_folder, private_key);
                    //            if (!result.Successful())
                    //            {
                    //                Console.WriteLine("Could not restore account from file: " + result.ResultMessage);
                    //                continue;
                    //            }
                    //        }
                    //    }
                    //}


                    if (!File.Exists($"{lyra_folder}{Path.DirectorySeparatorChar}{walletName}{LyraGlobal.WALLETFILEEXT}"))
                    {
                        Console.WriteLine("Local account data not found. Would you like to create a new account? (Y/n): ");
                        if (command.ReadYesNoAnswer())
                        {
                            Console.WriteLine("Please input a password:");
                            walletPassword = Console.ReadLine();

                            (var privateKey, var publicKey) = Signatures.GenerateWallet();

                            try
                            {
                                Wallet.Create(storage, walletName, walletPassword, network_id, privateKey);
                            }
                            catch(Exception ex)
                            {
                                Console.WriteLine(ex.Message);
                                continue;
                            }
                        }
                        else
                        {
                            Console.WriteLine("Please enter private key to restore account: ");
                            string privatekey = Console.ReadLine();

                            Console.WriteLine("Please input a password:");
                            walletPassword = Console.ReadLine();

                            if (!Signatures.ValidatePrivateKey(privatekey))
                                continue;

                            try
                            {
                                Wallet.Create(storage, walletName, walletPassword, network_id, privatekey);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.Message);
                                continue;
                            }                            
                        }

                        //if (INMEMORY)
                        //{
                        //    System.IO.File.WriteAllText(fileName, wallet.PrivateKey);
                        //}
                    }
                    else
                    {
                        Console.WriteLine($"Please input a password to open wallet {walletName}:");
                        walletPassword = Console.ReadLine();                        
                    }
                }

                wallet = Wallet.Open(storage, walletName, walletPassword);

                LyraRestClient rpcClient;
                if (!string.IsNullOrWhiteSpace(options.Node))
                {
                    int port = network_id.Equals("mainnet", StringComparison.InvariantCultureIgnoreCase) ? 5505 : 4505;
                    var apiUrl = $"http://{options.Node}:{port}/api/Node/";
                    rpcClient = LyraRestClient.Create(network_id, "Windows", $"{LyraGlobal.PRODUCTNAME} Client Cli", "1.0a", apiUrl);
                }
                else
                    rpcClient = LyraRestClient.Create(network_id, "Windows", $"{LyraGlobal.PRODUCTNAME} Client Cli", "1.0a");//await LyraRpcClient.CreateAsync(network_id, "Lyra Client Cli", "1.0");

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


                walletName = CommandProcessor.COMMAND_STATUS;

                while (walletName != CommandProcessor.COMMAND_STOP)
                {
                    var result = await command.Execute(wallet, walletName);
                    Console.Write(string.Format("{0}> ", wallet.AccountName));
                    //Console.Write
                    walletName = Console.ReadLine();
                }

                Console.WriteLine($"{LyraGlobal.PRODUCTNAME} Client is shutting down");
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Exception: {0}", ex.Message));
                Console.WriteLine($"{LyraGlobal.PRODUCTNAME} Client is shutting down");
            }

            return 0;
        }
    }
}
