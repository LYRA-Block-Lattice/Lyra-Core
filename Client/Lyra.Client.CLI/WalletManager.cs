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
using Lyra.Client.Lib;
using Lyra.Core.Cryptography;

namespace Lyra.Client.CLI
{
    public class WalletManager
    {
        Boolean timer_busy1;

        Timer timer1;

        public async Task<int> RunWallet(DAGClientHostedService client, Options options)
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

            var signr = new SignaturesClient();
            Wallet wallet;
            if (INMEMORY)
            {
                var inmemory_storage = new AccountInMemoryStorage();
                wallet = new Wallet(signr, inmemory_storage, network_id);
            }
            else
            {
                wallet = new Wallet(signr, new LiteAccountDatabase(), network_id);
            }

            string lyra_folder = BaseAccount.GetFullFolderName("Lyra-CLI-" + network_id);
            if (!Directory.Exists(lyra_folder))
                Directory.CreateDirectory(lyra_folder);

            string full_path = BaseAccount.GetFullPath(lyra_folder);

            Console.WriteLine("Storage Location: " + full_path);

            CommandProcessor command = new CommandProcessor(wallet);
            string input = null;
            try
            {
                while (!wallet.AccountExistsLocally(full_path, input))
                {
                    Console.WriteLine("Press Enter for default account, or enter account name: ");
                    input = Console.ReadLine();

                    if (string.IsNullOrEmpty(input))
                        input = "My Account";

                    wallet.AccountName = input;

                    string fileName = "";
                    if (INMEMORY)
                    {
                        fileName = full_path + wallet.AccountName + ".key";

                        if (System.IO.File.Exists(fileName))
                        {
                            string private_key = System.IO.File.ReadAllText(fileName);
                            if (wallet.ValidatePrivateKey(private_key))
                            {
                                var result = await wallet.RestoreAccountAsync(full_path, private_key);
                                if (!result.Successful())
                                {
                                    Console.WriteLine("Could not restore account from file: " + result.ResultMessage);
                                    continue;
                                }
                            }
                        }
                    }


                    if (!wallet.AccountExistsLocally(full_path, wallet.AccountName))
                    {
                        Console.WriteLine("Local account data not found. Would you like to create a new account? (Y/n): ");
                        if (command.ReadYesNoAnswer())
                        {
                            await wallet.CreateAccountAsync(full_path, wallet.AccountName, AccountTypes.Standard);
                        }
                        else
                        {
                            Console.WriteLine("Please enter private key to restore account: ");
                            string privatekey = Console.ReadLine();

                            if (!wallet.ValidatePrivateKey(privatekey))
                                continue;

                            var result = await wallet.RestoreAccountAsync(full_path, privatekey);
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
                        wallet.OpenAccount(full_path, wallet.AccountName);
                }

                //INodeAPI rpcClient;
                //if (client == null)
                var rpcClient = await LyraRestClient.CreateAsync(network_id, "Windows", "Lyra Client Cli", "1.0a");//await LyraRpcClient.CreateAsync(network_id, "Lyra Client Cli", "1.0");
                //else
                //    rpcClient = new DAGAPIClient(client);

                //if (WEB)
                //{
                //    string node_address;
                //    if (!string.IsNullOrWhiteSpace(options.Node))
                //        node_address = options.Node;
                //    else
                //        node_address = SelectNode(network_id);
                //    rpcClient = new WebAPIClient(node_address);
                //}
                //else
                //{
                //    rpcClient = new RPCClient(wallet.AccountId);
                //}

                //var sync_result = await wallet.Sync(rpcClient);
                //Console.WriteLine("Sync Result: " + sync_result.ToString());
                //wallet.Launch(rpcClient);

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

                Console.WriteLine("Lyra Client is shutting down");
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Exception: {0}", ex.Message));
                Console.WriteLine("Lyra Client is shutting down");
            }
            finally
            {
                if (wallet != null)
                    wallet.Dispose();
            }

            return 0;
        }

        string SelectNode(string network_id)
        {
            //INodeAPI rpcClient = new WebAPIClient("http://localhost:5002/api/");
            //INodeAPI rpcClient = new WebAPIClient("https://lyranode.ngrok.io/api/");

            switch (network_id)
            {
                case Options.LOCAL_NETWORK:
                    return "http://localhost:5002/api/";
                case Options.DEV_NETWORK:
                    return "https://node.lyra.live/api/";
                case Options.DEV_NETWORK_1:
                    return "https://node1.lyra.live/api/";
                case Options.TEST_NETWORK:
                    return "";
                case Options.MAIN_NETWORK:
                    return "";
                case Options.STAGE_NETWORK:
                    return "";
                default:
                    return "";
            }
        }
    }
}
