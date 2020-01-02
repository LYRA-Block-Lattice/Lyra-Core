using Lyra.Core.Decentralize;
using Lyra.Core.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Newtonsoft.Json;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

         private ChatId _groupId = new ChatId(-1001462436848);
        private LyraNodeConfig _config;

        private ConsensusRuntimeConfig _runtimeCfg;

        public NodesMonitor()
        {
        }

        public async Task StartAsync()
        {
            var me = await Bot.GetMeAsync();
            Console.Title = me.Username;

            Bot.OnMessage += BotOnMessageReceived;
            Bot.OnMessageEdited += BotOnMessageReceived;
            Bot.OnCallbackQuery += BotOnCallbackQueryReceived;
            Bot.OnInlineQuery += BotOnInlineQueryReceived;
            Bot.OnInlineResultChosen += BotOnChosenInlineResultReceived;
            Bot.OnReceiveError += BotOnReceiveError;

            Bot.StartReceiving(Array.Empty<UpdateType>());
            Console.WriteLine($"Start listening for @{me.Username}");

            _runtimeCfg = await RefreshConfigFromZK();
        }

        public void Stop()
        {
            Bot.StopReceiving();
        }

        public async Task SendGroupMessageAsync(string msg)
        {
            var retryCount = 5;
            while(retryCount-- > 0)
            {
                try
                {
                    await Bot.SendTextMessageAsync(_groupId, msg, ParseMode.Markdown);
                    break;
                }
                catch(Exception)
                {
                    await Task.Delay(2000);
                }
            }            
        }

        public async Task OnGossipMessageAsync(SourceSignedMessage msg)
        {
            var m = msg as ChatMsg;
            if (m == null)
                return;

            switch(m.MsgType)
            {
                case ChatMessageType.NodeUp:
                    // add node to zk runtime config
                    _runtimeCfg = await RefreshConfigFromZK();
                    if (_runtimeCfg != null && !_runtimeCfg.GetAllNodes().Any(a => a.Address == m.From))
                    {
                        _runtimeCfg.VotingNodes.Add(new AuthorizerNode
                        {
                            Address = m.From,
                            AccountID = m.Text,
                            StakingAmount = 10000   // TODO: get real balance
                        });
                        //await UsingZookeeper(async (zk) =>
                        //{
                        //    var json = JsonConvert.SerializeObject(_runtimeCfg);
                        //    var cfg = await zk.setDataAsync("/lyra", Encoding.ASCII.GetBytes(json));
                        //});
                        //await SendGroupMessageAsync($"*💖 Good News Everyone! 💖*\n\nA new node is up and running: {m.From}");
                    }
                    break;
                case ChatMessageType.AuthorizerPrePrepare:
                case ChatMessageType.AuthorizerPrepare:
                //case ChatMessageType.AuthorizerCommit:
                //    var typStr = string.Join(" ", Regex.Split(m.Type.ToString(), @"(?<!^)(?=[A-Z])"));
                //    var text = $"*From*: {m.From}\n*Event*: {typStr}\n*Block Number*: {m.BlockUIndex}";
                //    await SendGroupMessageAsync(text);
                //    break;
                default:
                    var typStr2 = string.Join(" ", Regex.Split(m.MsgType.ToString(), @"(?<!^)(?=[A-Z])"));
                    var text2 = $"*From*: {m.From}\n*Event*: {typStr2}\n*Text*: {m.Text}";
                    await SendGroupMessageAsync(text2);
                    break;
            }
        }

        private async void BotOnMessageReceived(object sender, MessageEventArgs messageEventArgs)
        {
            var message = messageEventArgs.Message;

            if (message == null || message.Type != MessageType.Text || !message.Text.StartsWith('/')) return;

            switch (message.Text.Split(' ', '@').First())
            {
//                case "/nodes":
//                    await UsingZookeeper(async (zk) =>
//                    {
//                        var cfg = await zk.getDataAsync("/lyra");
//                        _runtimeCfg = JsonConvert.DeserializeObject<ConsensusRuntimeConfig>(Encoding.ASCII.GetString(cfg.Data));
                        
//                        var text = $@"*BlockChain Mode*: {_runtimeCfg.Mode}
//*Seeds Nodes*: {string.Join(',', _runtimeCfg.Seeds.ToArray())}
//*Current Seed Node*: {_runtimeCfg.CurrentSeed}
//*Primary Authorizer Nodes*: {_runtimeCfg.PrimaryAuthorizerNodes.Aggregate("", (a, b) => { return a.ToString() + "\n    " + b.ToString(); })}
//*Backup Authorizer Nodes*: {_runtimeCfg.BackupAuthorizerNodes.Aggregate("", (a, b) => { return a.ToString() + "\n    " + b.ToString(); })}
//*Voting Nodes*: {_runtimeCfg.VotingNodes.Aggregate("", (a, b) => { return a.ToString() + "\n    " + b.ToString(); })}";

//                        await SendGroupMessageAsync(text);
//                    });
//                    break;
                case "/tps":
                    await SendGroupMessageAsync("No Data");
                    break;
                case "/help":
                    const string usage = @"
*User Command*:
/nodes    - send status of all nodes
/tps      - send info about TPS
/help     - display this message
*Authorizer Node Owner Command*:
/authlist _AccountId_ _SignedMessage_ - List a node to authorizers list
/authdelist _AccountId_ _SignedMessage_ - Delist a node from authorizers list
*Admim Command*:
/seed _AccountId_ - approve a authorizer node to seed node
/deseed _AccountId_ - disapprove a seed node
";
                    await SendGroupMessageAsync(usage);
                    break;
                case "/authlist":
                case "/authdelist":
                    await SendGroupMessageAsync("Under Construction");
                    break;
                case "/seed":
                case "/deseed":
                    if(message.From.Id == 397968968)      // @jfkwn
                    {
                        await SendGroupMessageAsync("Code todo");
                    }
                    else
                    {
                        await SendGroupMessageAsync("Only admins can do this");
                    }
                    break;
                default:
                    await SendGroupMessageAsync("Unknown command. Please reference to: /help");
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

        private async Task<ConsensusRuntimeConfig> RefreshConfigFromZK()
        {
            ConsensusRuntimeConfig result = null;
            try
            {
                //await UsingZookeeper(async (zk) =>
                //{
                //    // get Lyra network configurations from /lyra
                //    // {"mode":"permissioned","seeds":["node1","node2"]}
                //    var cfg = await zk.getDataAsync("/lyra");
                //    result = JsonConvert.DeserializeObject<ConsensusRuntimeConfig>(Encoding.ASCII.GetString(cfg.Data));
                //});
            }
            catch (Exception)
            {
                result = null;
            }
            return result;
        }

    }
}