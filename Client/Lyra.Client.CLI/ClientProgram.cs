//#define WEB
//#define INMEMORY

using System;
using CommandLine;
using CommandLine.Text;
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

namespace Lyra.Client.CLI

{
    class ClientProgram
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("LYRA Command Line Client");
            Console.WriteLine("Version: " + "0.5.3");

            ParserResult<Options> result = Parser.Default.ParseArguments<Options>(args);

            using (var host = CreateHost())
            {
                host.Start();

                var client = host.Services.GetService<IHostedService>();


                // activate api serivce grain
                // debug test
                var c = client;

                //var gf = host.Services.GetService<IGrainFactory>();
                //var nodeApi = c.Node;

                var wm = new WalletManager();
                int mapresult = result.MapResult((Options options) => wm.RunWallet(options).Result, _ => CommandLineError());

                if (mapresult != 0)
                {
                    if (mapresult == -2)
                        Console.WriteLine("Unsupported parameters");
                }
            }
        }

        static int CommandLineError()
        {
            Console.WriteLine("Unknown parameters");
            return -1;
        }

        private static IHost CreateHost()
        {
            return new HostBuilder()
                .ConfigureServices(services =>
                {
                    //// build config
                    //var Configuration = new ConfigurationBuilder()
                    //    .SetBasePath(Directory.GetCurrentDirectory())
                    //    .AddJsonFile("appsettings.json", false)
                    //    .AddEnvironmentVariables()
                    //    .Build();

                    //services.Configure<OrleansConfig>(Configuration.GetSection("Orleans"));

                    services.Configure<ConsoleLifetimeOptions>(options =>
                    {
                        options.SuppressStatusMessages = true;
                    });
                })
                .ConfigureLogging(builder =>
                {
                    builder.AddConsole();
                })
                .Build();
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
        
        [Option('n', "networkid", HelpText = "Network Id", Required = true)]
        public string NetworkId { get; set; }

        [Option('d', "database", HelpText = "Local data storage type", Required = false)]
        public string Database { get; set; }

        [Option('p', "protocol", HelpText = "Communication protocol with the node", Required = false)]
        public string Protocol { get; set; }

        [Option('n', "node", HelpText = "Node API URL", Required = false)]
        public string Node { get; set; }

        [Option('g', "genwallet", HelpText = "Generate Wallet Only", Required = false)]
        public string GenWalletName { get; set; }

        public IHost Host { get; set; }

        [Usage(ApplicationAlias = "dotnet lyracli.dll")]
        public static IEnumerable<Example> Examples
        {
            get
            {
                //return new List<Example>() {
                //new Example("Connect to mainnet", new Options { Testnet = false }),
                //new Example("Connect to testnet", new Options { Testnet = true }),
                //new Example("Connect to another local testnet node", new Options { Testnet = true, Seed = "127.0.0.1:7901" }),
                //new Example("Create a local single-node testnet", new Options { Testnet = true, Seed = "self" }),
                //new Example("read more lines", new[] { UnParserSettings.WithGroupSwitchesOnly() }, new Options { Testnet = true })
                //};
                //yield return new Example("Connect to mainnet", new Options { Testnet = false });
                //yield return new Example("Connect to testnet", new Options { Testnet = true });
                //                yield return new Example("Connect to another local testnet node", new Options { Testnet = true, Seed = "127.0.0.1:7901" });
                //              yield return new Example("Create a local single-node development testnet", new Options { Testnet = true, Seed = SEED_SELF, NetworkId = DEV_NETWORK });
                yield return new Example("Connect to local devnet node", new Options { NetworkId = LOCAL_NETWORK, Database = LITEDB_DATABASE, Protocol = RPC_PROTOCOL });
                yield return new Example("Connect to public devnet", new Options { NetworkId = DEV_NETWORK, Database = LITEDB_DATABASE, Protocol = WEBAPI_PROTOCOL });
                //yield return new Example("Specify account id to collect authorization fees", 
                //new Options { Testnet = true, Seed = SEED_SELF, Auth_Account= "PiLj1DpQxMPSs6r7RwCBXBzCESoFGnFJN3qV1TcRaiYCaCtgB8kER714mc3pJu2M8JNosQ4o8RB5wenMEbHkHG7P" });

            }
        }

    }

}
