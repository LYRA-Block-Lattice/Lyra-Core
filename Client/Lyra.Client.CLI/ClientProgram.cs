//#define WEB
//#define INMEMORY

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Net.Http;
using Lyra.Core.API;
using Microsoft.Extensions.Configuration;
using System.IO;
using Lyra.Core.Utils;
using McMaster.Extensions.CommandLineUtils;
using System.ComponentModel.DataAnnotations;
using Client.CLI;
using System.Threading;
using AustinHarris.JsonRpc;

namespace Lyra.Client.CLI
{
    public class ClientProgram
    {
        [Option("-n|--networkid", Description = "Network Id")]
        public string NetworkId { get; set; } = "mainnet";

        [Option("-d|--database", Description = "Local data storage type")]
        public string Database { get; set; }

        [Option("-p|--protocol", Description = "Communication protocol with the node")]
        public string Protocol { get; set; }

        [Option("-u|--node", Description = "Node API URL")]
        public string Node { get; set; }

        [Option("-g|--genwallet", Description = "Generate Wallet Only")]
        public string GenWalletName { get; set; }

        [Option("-w|--wallet", Description = "Wallet Name")]
        public string WalletName { get; set; }

        [Option("-s|--rpcserver", Description = "Run JsonRPC Server")]
        public bool RunJsonRPCServer { get; set; }

        [Option("-b|--binding", Description = "JsonRPC Server Binding Address [http://localhost:3373/]")]
        public string ServerBinding { get; set; }

        public CancellationTokenSource cancellation { get; set; }

        public ClientProgram()
        {
            cancellation = new CancellationTokenSource();

            new CtrlC().AddHandler((s) => {
                cancellation.Cancel();
                Environment.Exit(1);
            });
        }
        static async Task<int> Main(string[] args)
        {
            return await new HostBuilder()
                .ConfigureLogging((context, builder) =>
                {
                    builder.AddConsole();
                    SimpleLogger.Factory = new LoggerFactory();
                })
                .ConfigureServices((context, services) =>
                {
                    //services.AddSingleton<IGreeter, Greeter>()
                    //    .AddSingleton<IConsole>(PhysicalConsole.Singleton);
                })
                .RunCommandLineApplicationAsync<ClientProgram>(args);
        }

        private async Task OnExecuteAsync()
        {
            //Console.WriteLine("Personal and Business Banking, Payments, and Digital Asset Management");
            //Console.WriteLine("Banking: Store, transfer, and receive interest on multiple digital assets");
            //Console.WriteLine("Payments: Make or accept instant payments using various currencies, online and in store");
            //Console.WriteLine("Digital Asset Management: Issue your own tokens within seconds");
            //Console.WriteLine("");

            if(RunJsonRPCServer)
            {
                new WalletServer();
                new RPCServer(ServerBinding);

                while(true)
                {
                    var json = Console.ReadLine();
                    if (json == "exit")
                        break;

                    var rep = await JsonRpcProcessor.Process(json);
                    Console.WriteLine(rep);
                }
            }
            else
            {
                Console.WriteLine($"{LyraGlobal.PRODUCTNAME} Command Line Client");
                Console.WriteLine("Version: " + LyraGlobal.NODE_VERSION);

                Console.WriteLine($"\nCurrent networkd ID: {NetworkId}\n");

                var mgr = new WalletManager();
                await mgr.RunWallet(this);
            }
        }
    }
    
    public class Options
    {
        public const string LOCAL_NETWORK = "local";
        public const string DEV_NETWORK = "devnet0";
        public const string DEV_NETWORK_1 = "devnet1";
        public const string TEST_NETWORK = "testnet";
        public const string STAGE_NETWORK = "stagenet";
        public const string MAIN_NETWORK = "mainnet";
        public const string LEX_NETWORK = "lexnet";

        public const string RPC_PROTOCOL = "rpc";
        public const string WEBAPI_PROTOCOL = "webapi";
        public const string P2P_PROTOCOL = "p2p";

        public const string INMEMORY_DATABASE = "inmemory";
        public const string LITEDB_DATABASE = "litedb";
    }
}
