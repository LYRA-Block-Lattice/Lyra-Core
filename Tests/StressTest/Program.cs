using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;

using Lyra.Core.Blocks;
using Lyra.Core.Accounts;
using Lyra.Core.Utils;
using System.Configuration;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Threading.Tasks;

namespace StressTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using (var host = CreateHost())
            {
                host.Start();
                var logger = new SimpleLogger("stresstest").Logger;
                try
                {
                    var log_message = "StressTest starting...";
                    Console.WriteLine(log_message);
                    logger.LogInformation(log_message);

                    var client = host.Services.GetService<IHostedService>();

                    InMemWallet sender_wallet = null;

                    var recipient_wallet = new InMemWallet();
                    recipient_wallet.GenerateWallet();
                    var recipient_account_id = recipient_wallet.AccountId;
                    var recipient_private_key = recipient_wallet.PrivateKey;

                    log_message = "recipient_account_id: " + recipient_account_id;
                    Console.WriteLine(log_message);
                    logger.LogInformation(log_message);

                    log_message = "recipient_private_key: " + recipient_private_key;
                    Console.WriteLine(log_message);
                    logger.LogInformation(log_message);

                    for (int i = 0; i < Convert.ToInt32(Config.SendCount); i++)
                    {
                        try
                        {
                            if (sender_wallet == null)
                                sender_wallet = await RestoreSenderWalletAsync();

                            var send_result = await sender_wallet.Wallet.SendAsync((decimal)0.000001, recipient_account_id);
                            if (send_result.ResultCode != APIResultCodes.Success)
                            {
                                throw new Exception(send_result.ResultCode.ToString());
                            }
                            else
                            {
                                log_message = $"Send Success {i}";
                                Console.WriteLine(log_message);
                                logger.LogInformation(log_message);
                            }
                        }
                        catch (Exception e)
                        {

                            log_message = $"Send Error: {e.Message}";
                            Console.WriteLine(log_message);
                            logger.LogError(log_message);

                            sender_wallet = null;
                            Thread.Sleep(1000);
                        }
                        Thread.Sleep(500);
                    }

                    for (int i = 0; i < Convert.ToInt32(Config.SendCount); i++)
                    {
                        var receive_wallet_result = await SyncReceiveWalletAsync(recipient_private_key, logger);
                        if (receive_wallet_result == APIResultCodes.Success)
                        {
                            log_message = $"Receive Success";
                            Console.WriteLine(log_message);
                            logger.LogInformation(log_message);
                            break;
                        }
                        else
                        {
                            log_message = $"Receive Error: {receive_wallet_result}";
                            Console.WriteLine(log_message);
                            logger.LogError(log_message);

                            Thread.Sleep(1000);
                        }
                    }

                    log_message = "StressTest ended!";
                    Console.WriteLine(log_message);
                    logger.LogInformation(log_message);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    logger.LogError(e.Message);
                }
            }

        }

        private static async Task<APIResultCodes> SyncReceiveWalletAsync(string recipient_private_key, ILogger logger)
        {
            var result = new InMemWallet();
            var restore_result_code = await result.RestoreAsync(Config.NetworkId, Config.NodeURL, recipient_private_key);
            if (restore_result_code == APIResultCodes.Success)
            {
                var balance = result.Wallet.BaseBalance;
                var log_message = $"Receive wallet balance: {balance}";
                Console.WriteLine(log_message);
                logger.LogInformation(log_message);
            }
            return restore_result_code;
        }

        private static async Task<InMemWallet> RestoreSenderWalletAsync()
        {
            var result = new InMemWallet();
            var restore_result_code = await result.RestoreAsync(Config.NetworkId, Config.NodeURL, Config.SenderPrivateKey);
            if (restore_result_code != APIResultCodes.Success)
            {
                throw new Exception("Could not restore sender_wallet");
            }
            return result;
        }

        private static IHost CreateHost()
        {
            return new HostBuilder()
                .ConfigureServices(services =>
                {
                    var Configuration = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json", false)
                        .AddEnvironmentVariables()
                        .Build();

                    Config.LoadConfig(Configuration);

                })
                .ConfigureLogging(builder =>
                {
                    builder.AddConsole();
                    var logName = Config.LogPath + $"stresstest-{DateTime.Now.Date.ToString("yyyy'-'MM'-'dd")}.txt";//Directory.GetCurrentDirectory(); //$"C:/inetpub/logs/LogFiles/";
                    var loggerFactory = new LoggerFactory();
                    loggerFactory.AddFile(logName);
                    SimpleLogger.Factory = loggerFactory;
                })
                .Build();
        }
    }
}
