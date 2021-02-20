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
using McMaster.Extensions.CommandLineUtils;
using NeoSmart.SecureStore;

namespace Lyra.Client.CLI
{
    public class WalletManager
    {
        public async Task<int> RunWallet(ClientProgram options)
        {
            string network_id = options.NetworkId;

            bool INMEMORY = options.Database == Options.INMEMORY_DATABASE;
            string lyra_folder = Wallet.GetFullFolderName(network_id, "wallets");

            if (options.GenWalletName != null)
            {
                GenerateWallet(lyra_folder, network_id, options.GenWalletName);
                return 0;
            }

            IAccountDatabase storage;
            if (INMEMORY)
            {
                storage = new AccountInMemoryStorage();
            }
            else
            {
                if (!Directory.Exists(lyra_folder))
                    Directory.CreateDirectory(lyra_folder);

                storage = new SecuredWalletStore(lyra_folder);
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

                    //string fileName = "";
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
                        if (Prompt.GetYesNo("Local account data not found. Would you like to create a new account? (Y/n): ", defaultAnswer: true))
                        {
                            var password = Prompt.GetPassword("Please specify a strong password for your wallet: ",
                                promptColor: ConsoleColor.Red,
                                promptBgColor: ConsoleColor.Black);

                            var password2 = Prompt.GetPassword("Repeat your password: ",
                                promptColor: ConsoleColor.Red,
                                promptBgColor: ConsoleColor.Black);

                            if (password != password2)
                            {
                                Console.WriteLine("Passwords not match.");
                                continue;
                            }

                            walletPassword = password;

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

                            var password = Prompt.GetPassword("Please specify a strong password for your wallet: ",
                                promptColor: ConsoleColor.Red,
                                promptBgColor: ConsoleColor.Black);

                            var password2 = Prompt.GetPassword("Repeat your password: ",
                                promptColor: ConsoleColor.Red,
                                promptBgColor: ConsoleColor.Black);

                            if (password != password2)
                            {
                                Console.WriteLine("Passwords not match.");
                                continue;
                            }
                            walletPassword = password;

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
                        walletPassword = Prompt.GetPassword($"Please input the password to open wallet {walletName}: ",
                            promptColor: ConsoleColor.Red,
                            promptBgColor: ConsoleColor.Black);               
                    }
                }

                Wallet wallet;
                try
                {
                    wallet = Wallet.Open(storage, walletName, walletPassword);
                }
                catch(TamperedCipherTextException)
                {
                    Console.WriteLine("Wrong password.");
                    return 1;
                }

                LyraRestClient rpcClient;
                if (!string.IsNullOrWhiteSpace(options.Node))
                {
                    int port = network_id.Equals("mainnet", StringComparison.InvariantCultureIgnoreCase) ? 5504 : 4504;
                    var apiUrl = $"https://{options.Node}:{port}/api/Node/";
                    rpcClient = LyraRestClient.Create(network_id, "Windows", $"{LyraGlobal.PRODUCTNAME} Client Cli", "1.0a", apiUrl);
                }
                else
                    rpcClient = LyraRestClient.Create(network_id, "Windows", $"{LyraGlobal.PRODUCTNAME} Client Cli", "1.0a");//await LyraRpcClient.CreateAsync(network_id, "Lyra Client Cli", "1.0");

                Console.WriteLine("Type 'help' to see the list of available commands");
                Console.WriteLine("");

                try
                {
                    await wallet.Sync(rpcClient);
                }
                catch(Exception)
                {
                    Console.WriteLine("Startup sync failed. You may need to run sync command manually.");
                }

                var lastServiceBlock = await wallet.GetLastServiceBlockAsync();
                Console.WriteLine($"Last Service Block Received {lastServiceBlock.Height}");
                Console.WriteLine(string.Format("Transfer Fee: {0} ", lastServiceBlock.TransferFee));
                Console.WriteLine(string.Format("Token Generation Fee: {0} ", lastServiceBlock.TokenGenerationFee));
                Console.WriteLine(string.Format("Trade Fee: {0} ", lastServiceBlock.TradeFee));
                Console.Write(string.Format("{0}> ", wallet.AccountName));

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


                var cmdInput = CommandProcessor.COMMAND_STATUS;

                while (cmdInput != CommandProcessor.COMMAND_STOP)
                {
                    var result = await command.Execute(wallet, cmdInput);
                    Console.Write(string.Format("{0}> ", wallet.AccountName));
                    //Console.Write
                    cmdInput = Console.ReadLine();
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

        private void GenerateWallet(string path, string networkId, string walletName)
        {
            var secureFile = new SecuredWalletStore(path);

            if (secureFile.Exists(walletName))
            {
                Console.WriteLine($"Wallet named {walletName} already exists.");
                return;
            }

            Console.WriteLine($"Creating wallet for {networkId}.");

            var password = Prompt.GetPassword("Please specify a strong password for your wallet: ",
                promptColor: ConsoleColor.Red,
                promptBgColor: ConsoleColor.Black);

            var password2 = Prompt.GetPassword("Repeat your password: ",
                promptColor: ConsoleColor.Red,
                promptBgColor: ConsoleColor.Black);

            if (password != password2)
            {
                Console.WriteLine("Passwords not match.");
                return;
            }

            (var privateKey, var publicKey) = Signatures.GenerateWallet();

            Console.WriteLine($"The new wallet {walletName} for {networkId} was created.");
            //Console.WriteLine($"Private Key: {privateKey}");
            Console.WriteLine($"Account ID: {publicKey}");

            secureFile.Create(walletName, password, networkId, privateKey, publicKey, "");
            Console.WriteLine($"Wallet saved to: {path}");
        }
    }
}
