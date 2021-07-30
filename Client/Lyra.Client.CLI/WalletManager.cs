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
                GenerateWallet(lyra_folder, network_id, options.GenWalletName, options.WalletPassword);
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
            string walletName = options.WalletName;
            string walletPassword = null;
            try
            {
                while (walletName == null || !File.Exists($"{lyra_folder}{Path.DirectorySeparatorChar}{walletName}{LyraGlobal.WALLETFILEEXT}"))
                {
                    walletName = Prompt.GetString($"Open wallet or creat a new wallet. Name:", "My Account");

                    if (!File.Exists($"{lyra_folder}{Path.DirectorySeparatorChar}{walletName}{LyraGlobal.WALLETFILEEXT}"))
                    {
                        if (Prompt.GetYesNo("Local account data not found. Would you like to create a new wallet?", defaultAnswer: true))
                        {
                            var password = Prompt.GetPassword("Please specify a strong password for your wallet:",
                                promptColor: ConsoleColor.Red,
                                promptBgColor: ConsoleColor.Black);

                            var password2 = Prompt.GetPassword("Repeat your password:",
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
                                Console.WriteLine("Wallet created.");
                            }
                            catch(Exception ex)
                            {
                                Console.WriteLine(ex.Message);
                                continue;
                            }
                        }
                        else
                        {
                            string privatekey = Prompt.GetString($"Please enter private key to restore account:");

                            var password = Prompt.GetPassword("Please specify a strong password for your wallet:",
                                promptColor: ConsoleColor.Red,
                                promptBgColor: ConsoleColor.Black);

                            var password2 = Prompt.GetPassword("Repeat your password:",
                                promptColor: ConsoleColor.Red,
                                promptBgColor: ConsoleColor.Black);

                            if (password != password2)
                            {
                                Console.WriteLine("Passwords not match.");
                                continue;
                            }
                            walletPassword = password;

                            if (!Signatures.ValidatePrivateKey(privatekey))
                            {
                                Console.WriteLine("Private key is not valid.");
                                continue;
                            }

                            try
                            {
                                Wallet.Create(storage, walletName, walletPassword, network_id, privatekey);
                                Console.WriteLine("Wallet restored.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.Message);
                                continue;
                            }                            
                        }
                    }
                }

                Wallet wallet;
                try
                {
                    walletPassword ??= Prompt.GetPassword($"Please input the password to open wallet {walletName}:",
                        promptColor: ConsoleColor.Red,
                        promptBgColor: ConsoleColor.Black);

                    wallet = Wallet.Open(storage, walletName, walletPassword);
                    Console.WriteLine("Wallet opened.");
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

                try
                {
                    Console.WriteLine("Try syncing wallet with Lyra blockchain...");
                    await wallet.Sync(rpcClient, options.cancellation.Token);
                    Console.WriteLine("Wallet is synced.");
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

                Console.WriteLine("\nType 'help' to see the list of available commands");
                Console.WriteLine("");

                var cmdInput = CommandProcessor.COMMAND_STATUS;

                while (!options.cancellation.IsCancellationRequested && cmdInput != CommandProcessor.COMMAND_STOP)
                {
                    var result = await command.Execute(wallet, cmdInput, options.cancellation.Token);
                    Console.Write(string.Format("\n{0}> ", wallet.AccountName));
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

        private void GenerateWallet(string path, string networkId, string walletName, string password)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            var secureFile = new SecuredWalletStore(path);

            if (secureFile.Exists(walletName))
            {
                Console.WriteLine($"Wallet named {walletName} already exists.");
                return;
            }

            Console.WriteLine($"Creating wallet for {networkId}.");

            var walletPass = password;
            if(string.IsNullOrEmpty(password))
            {
                var password1 = Prompt.GetPassword("Please specify a strong password for your wallet:",
    promptColor: ConsoleColor.Red,
    promptBgColor: ConsoleColor.Black);

                var password2 = Prompt.GetPassword("Repeat your password:",
                    promptColor: ConsoleColor.Red,
                    promptBgColor: ConsoleColor.Black);

                if (password1 != password2)
                {
                    Console.WriteLine("Passwords not match.");
                    return;
                }

                walletPass = password1;
            }


            (var privateKey, var publicKey) = Signatures.GenerateWallet();

            Console.WriteLine($"The new wallet {walletName} for {networkId} was created.");
            //Console.WriteLine($"Private Key: {privateKey}");
            Console.WriteLine($"Account ID: {publicKey}");

            secureFile.Create(walletName, walletPass, networkId, privateKey, publicKey, "");
            Console.WriteLine($"Wallet saved to: {path}{walletName}");
        }
    }
}
