using Lyra.Authorizer.Decentralize;
using Lyra.Core.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Newtonsoft.Json;
using org.apache.zookeeper;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;

namespace LyraNodesBot
{
    public class NodesMonitor
    {
        private readonly TelegramBotClient Bot = new TelegramBotClient(System.IO.File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\telegram.txt"));

        private ZooKeeperWatcher _watcher = new ZooKeeperWatcher(LoggerFactory.Create(builder => { builder.AddConsole(); }).CreateLogger("log"));
        //private readonly ZooKeeper ZK = new ZooKeeper(ConfigurationManager.AppSettings["zookeeperConnectString"], 2000,
        //    new ZooKeeperWatcher(LoggerFactory.Create(builder => { builder.AddConsole(); }).CreateLogger("log")));

        private ChatId _groupId = new ChatId(-1001462436848);
        private LyraNodeConfig _config;

        public NodesMonitor()
        {
        }

        public void Start()
        {
            var me = Bot.GetMeAsync().Result;
            Console.Title = me.Username;

            Bot.OnMessage += BotOnMessageReceived;
            Bot.OnMessageEdited += BotOnMessageReceived;
            Bot.OnCallbackQuery += BotOnCallbackQueryReceived;
            Bot.OnInlineQuery += BotOnInlineQueryReceived;
            Bot.OnInlineResultChosen += BotOnChosenInlineResultReceived;
            Bot.OnReceiveError += BotOnReceiveError;

            Bot.StartReceiving(Array.Empty<UpdateType>());
            Console.WriteLine($"Start listening for @{me.Username}");
        }

        public void Stop()
        {
            Bot.StopReceiving();
        }

        public async Task SendGroupMessageAsync(string msg)
        {
            await Bot.SendTextMessageAsync(_groupId, msg, ParseMode.Markdown);
        }

        private async void BotOnMessageReceived(object sender, MessageEventArgs messageEventArgs)
        {
            var message = messageEventArgs.Message;

            if (message == null || message.Type != MessageType.Text) return;

            switch (message.Text.Split(' ', '@').First())
            {
                case "/nodes":
                    await UsingZookeeper(async (zk) =>
                    {
                        var cfg = await zk.getDataAsync("/lyra");
                        var runtimeConfig = JsonConvert.DeserializeObject<ConsensusRuntimeConfig>(Encoding.ASCII.GetString(cfg.Data));
                        
                        var text = $@"*BlockChain Mode*: {runtimeConfig.Mode}
*Seeds Nodes*: {string.Join(',', runtimeConfig.Seeds.ToArray())}
*Current Seed Node*: {runtimeConfig.CurrentSeed}
*Primary Authorizer Nodes*: {runtimeConfig.PrimaryAuthorizerNodes.Aggregate("", (a, b) => { return a.ToString() + "\n    " + b.ToString(); })}
*Backup Authorizer Nodes*: {runtimeConfig.BackupAuthorizerNodes.Aggregate("", (a, b) => { return a.ToString() + "\n    " + b.ToString(); })}
*Voting Nodes*: {runtimeConfig.VotingNodes.Aggregate("", (a, b) => { return a.ToString() + "\n    " + b.ToString(); })}";

                        await SendGroupMessageAsync(text);
                    });
                    break;
                case "/leader":
                case "/tps":
                    await SendGroupMessageAsync("No Data");
                    break;
                default:
                    const string usage = @"
Usage:
/nodes    - send status of all nodes
/leader   - send info about current leader node
/tps      - send info about TPS";

                    await Bot.SendTextMessageAsync(
                        message.Chat.Id,
                        usage,
                        replyMarkup: new ReplyKeyboardRemove());
                    break;
            }
        }

        private async void BotOnCallbackQueryReceived(object sender, CallbackQueryEventArgs callbackQueryEventArgs)
        {
            var callbackQuery = callbackQueryEventArgs.CallbackQuery;

            await Bot.AnswerCallbackQueryAsync(
                callbackQuery.Id,
                $"Received {callbackQuery.Data}");

            await Bot.SendTextMessageAsync(
                callbackQuery.Message.Chat.Id,
                $"Received {callbackQuery.Data}");
        }

        private async void BotOnInlineQueryReceived(object sender, InlineQueryEventArgs inlineQueryEventArgs)
        {
            Console.WriteLine($"Received inline query from: {inlineQueryEventArgs.InlineQuery.From.Id}");

            InlineQueryResultBase[] results = {
                new InlineQueryResultLocation(
                    id: "1",
                    latitude: 40.7058316f,
                    longitude: -74.2581888f,
                    title: "New York")   // displayed result
                    {
                        InputMessageContent = new InputLocationMessageContent(
                            latitude: 40.7058316f,
                            longitude: -74.2581888f)    // message if result is selected
                    },

                new InlineQueryResultLocation(
                    id: "2",
                    latitude: 13.1449577f,
                    longitude: 52.507629f,
                    title: "Berlin") // displayed result
                    {
                        InputMessageContent = new InputLocationMessageContent(
                            latitude: 13.1449577f,
                            longitude: 52.507629f)   // message if result is selected
                    }
            };

            await Bot.AnswerInlineQueryAsync(
                inlineQueryEventArgs.InlineQuery.Id,
                results,
                isPersonal: true,
                cacheTime: 0);
        }

        private void BotOnChosenInlineResultReceived(object sender, ChosenInlineResultEventArgs chosenInlineResultEventArgs)
        {
            Console.WriteLine($"Received inline result: {chosenInlineResultEventArgs.ChosenInlineResult.ResultId}");
        }

        private void BotOnReceiveError(object sender, ReceiveErrorEventArgs receiveErrorEventArgs)
        {
            Console.WriteLine("Received error: {0} — {1}",
                receiveErrorEventArgs.ApiRequestException.ErrorCode,
                receiveErrorEventArgs.ApiRequestException.Message);
        }

        private Task UsingZookeeper(Func<ZooKeeper, Task> zkMethod)
        {
            return ZooKeeper.Using(ConfigurationManager.AppSettings["zookeeperConnectString"], 2000, _watcher, zkMethod);
        }

        class ZooKeeperWatcher : Watcher
        {
            private readonly ILogger logger;
            public ZooKeeperWatcher(ILogger logger)
            {
                this.logger = logger;
            }

            public override Task process(WatchedEvent @event)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug(@event.ToString());
                }
                return Task.CompletedTask;
            }
        }
    }
}