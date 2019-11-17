using System;
using System.IO;

using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

using Lyra.Core.LiteDB;
using Lyra.Core.Accounts;
using Lyra.Core.Accounts.Node;
using Lyra.Core.MongoDB;

//[assembly: FileVersion("0.0.1")]
//[assembly: AssemblyFileVersion("1.0.2000.0")]

namespace Lyra.Node
{
    class NodeProgram
    {
        static ServiceAccount serviceAccount;

        static IAccountCollection accountCollection;

        static RPCServer _rpc = null;

        static TradeMatchEngine tradeMatchEngine;

        static void Main(string[] args)
        {


            var result = Parser.Default.ParseArguments<Options>(args);

            int mapresult = result.MapResult(
                (Options options) => InitializeNode(options), _ => CommandLineError());

            if (mapresult != 0)
            {
                if (mapresult == -2)
                    Console.WriteLine("Unsupported parameters");
                return;
            }


            Console.WriteLine("Lyra Node is up");
            Console.WriteLine("Version: " + "0.5.3");

            Console.WriteLine("Type 'stop' command to shut down the node");
            string input = null;

            try
            {

                while (input != "stop")
                {
                    if (input == "key")
                    {
                        if (serviceAccount != null)
                            Console.WriteLine(serviceAccount.PrivateKey);
                    }
                    else
                    if (input == "id")
                    {
                        if (serviceAccount != null)
                            Console.WriteLine(serviceAccount.AccountId);
                    }
                    else
                    if (!string.IsNullOrEmpty(input) && input != "stop")
                        Console.WriteLine("Unknown command: " + input);

                    input = Console.ReadLine();
                }
            }
            finally
            {
                if (serviceAccount != null)
                    serviceAccount.Dispose();
                if (accountCollection != null)
                    accountCollection.Dispose();
                if (_rpc != null)
                    _rpc.Dispose();
            }
            Console.WriteLine("Lyra Node is down");
        }

        static int CommandLineError()
        {
            Console.WriteLine("Unknown parameters");
            return -1;
        }


        static int InitializeNode(Options options)
        {


            //if (options.Testnet && options.Seed == Options.SEED_SELF)
            if (options.Seed == Options.SEED_SELF)
            {
                Console.WriteLine("Starting single-node network: " + NodeGlobalParameters.Network_Id);
                NodeGlobalParameters.IsSingleNodeTestnet = true;
                NodeGlobalParameters.Network_Id = options.NetworkId;

                string full_path = null;

                if (options.Database == Options.LITEDB_DATABASE)
                {
                    string lyra_folder = BaseAccount.GetFullFolderName("Lyra-Node-" + NodeGlobalParameters.Network_Id);
                    if (!Directory.Exists(lyra_folder))
                        Directory.CreateDirectory(lyra_folder);

                    full_path = BaseAccount.GetFullPath(lyra_folder);
                    
                    serviceAccount = new ServiceAccount(new LiteAccountDatabase(), NodeGlobalParameters.Network_Id);

                    accountCollection = new LiteAccountCollection(full_path);
                    Console.WriteLine("Database Location: " + full_path);
                }
                else
                if (options.Database == Options.MONGODB_DATABASE)
                {
                    var service_database = new MongoServiceAccountDatabase(options.ConnectionString, NodeGlobalParameters.DEFAULT_DATABASE_NAME, ServiceAccount.SERVICE_ACCOUNT_NAME, NodeGlobalParameters.Network_Id);
                    serviceAccount = new ServiceAccount(service_database, NodeGlobalParameters.Network_Id);

                    accountCollection = new MongoAccountCollection(options.ConnectionString, NodeGlobalParameters.DEFAULT_DATABASE_NAME, NodeGlobalParameters.Network_Id);
                    Console.WriteLine("Database Location: mongodb " + (accountCollection as MongoAccountCollection).Cluster);
                }
                else
                    return -20;

                tradeMatchEngine = new TradeMatchEngine(accountCollection, serviceAccount);
                Console.WriteLine("Node is starting");
                return StartSingleNodeTestnet(full_path);
            }
            return -2;
        }

        static int StartSingleNodeTestnet(string DatabaseLocationPath)
        {
            _rpc = new RPCServer();
            _rpc.Initialize(serviceAccount, accountCollection, tradeMatchEngine);
            serviceAccount.StartSingleNodeTestnet(DatabaseLocationPath);
            return 0;
        }

    }


    class Options
    {
        public const string SEED_SELF = "self";
        //[Value(0, MetaName = "--testnet", HelpText = "Connect to testnet.", Required = false)]
        public const string DEV_NETWORK = "devnet";
        public const string TEST_NETWORK = "testnet";
        public const string MAIN_NETWORK = "mainnet";
        public const string LEX_NETWORK = "lexnet";

        public const string MONGODB_DATABASE = "mongodb";
        public const string LITEDB_DATABASE = "litedb";
        
        //[Option('t', "testnet", HelpText = "Connect to testnet.", Required = false)]
        //[Value(0, MetaName = "--testnet", HelpText = "Connect to testnet.")]
        //public bool Testnet { get; set; }

        [Option('n', "networkid", HelpText = "Network Id.", Required = true)]
        public string NetworkId { get; set; }

        [Option('s', "seed", HelpText = "Seed node address.", Required = true)]
        public string Seed { get; set; }

        [Option('d', "database", HelpText = "Local data storage type", Required = true)]
        public string Database { get; set; }

        [Option('c', "connectionstring", HelpText = "Database connection string", Required = false)]
        public string ConnectionString { get; set; }

        //[Option('s', "auth_account", HelpText = "Authorizer Account Id (optional).")]
        //public string Auth_Account { get; set; }

        [Usage(ApplicationAlias = "dotnet lyranode.dll")]
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
                yield return new Example("Connect to another local testnet node", new Options { NetworkId = TEST_NETWORK, Seed = "127.0.0.1:7901", Database = LITEDB_DATABASE });
                yield return new Example("Create a local single-node development testnet", new Options { NetworkId = DEV_NETWORK, Seed = SEED_SELF, Database = LITEDB_DATABASE });
                yield return new Example("Create a local single-node testnet with mongodb", new Options { NetworkId = DEV_NETWORK, Seed = SEED_SELF, Database = MONGODB_DATABASE, ConnectionString = "mongodb+srv://<dbusername>:<password>@@<clusteraddress>/test?retryWrites=true&w=majority" });
                //yield return new Example("Specify account id to collect authorization fees", 
                //new Options { Testnet = true, Seed = SEED_SELF, Auth_Account= "PiLj1DpQxMPSs6r7RwCBXBzCESoFGnFJN3qV1TcRaiYCaCtgB8kER714mc3pJu2M8JNosQ4o8RB5wenMEbHkHG7P" });

            }
        }

    }

}
