using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Decentralize;
using Lyra.Core.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Newtonsoft.Json;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
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
        private string _network;
        public NodesMonitor(string network)
        {
            _network = network;
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
                    await SendNodesInfoToGroupAsync();
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

        private async Task SendHeight()
        {
            var wc = new WebClient();
            var json = wc.DownloadString(LyraGlobal.SelectNode(_network) + "LyraNode/GetSyncState");
            var bb = JsonConvert.DeserializeObject<GetSyncStateAPIResult>(json);

            await SendGroupMessageAsync($"Current Height: *{bb.NewestBlockUIndex}*");
        }
        private async Task SendNodesInfoToGroupAsync()
        {
            var wc = new WebClient();
            var json = wc.DownloadString(LyraGlobal.SelectNode(_network) + "LyraNode/GetBillboard");
            var bb = JsonConvert.DeserializeObject<BillBoard>(json);
            var sb = new StringBuilder();
            foreach (var node in bb.AllNodes.Values)
            {
                sb.AppendLine($"{node.AccountID}");
                sb.AppendLine($"Staking Balance: {node.Balance}");
                sb.AppendLine($"Last Staking Time: {node.LastStaking}");
                sb.AppendLine();
            }
            await SendGroupMessageAsync(sb.ToString());
        }

        private async void BotOnMessageReceived(object sender, MessageEventArgs messageEventArgs)
        {
            var message = messageEventArgs.Message;

            if (message == null || message.Type != MessageType.Text || !message.Text.StartsWith('/')) return;

            var args = message.Text.Split(' ', '@');
            switch (args.First())
            {
                case "/height":
                    await SendHeight();
                    break;
                case "/nodes":
                    await SendNodesInfoToGroupAsync();
                    break;
                case "/tps":
                    await SendGroupMessageAsync("No Data");
                    break;
                case "/send":
                    if (args.Length != 2)
                        await SendGroupMessageAsync("*Example*\n\n/send [[your wallet address here]]");
                    else
                    {
                        var ret = await SendEvalCoin(args.Skip(1).First());
                        await SendGroupMessageAsync(ret);
                    }
                    break;
                case "/help":
                    const string usage = @"
*User Command*:
/height   - display Lyra BlockChain Height
/nodes    - send status of all nodes
/tps      - send info about TPS
/send     - send testnet Lyra Coin to address
/help     - display this message";
//*Authorizer Node Owner Command*:
///authlist _AccountId_ _SignedMessage_ - List a node to authorizers list
///authdelist _AccountId_ _SignedMessage_ - Delist a node from authorizers list
//*Admim Command*:
///seed _AccountId_ - approve a authorizer node to seed node
///deseed _AccountId_ - disapprove a seed node
//";
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

        private async Task<string> SendEvalCoin(string address)
        {
            try
            {
                var walletKey = Environment.GetEnvironmentVariable("LYRA_BOT_WALLET_KEY");
                if (string.IsNullOrWhiteSpace(walletKey))
                    return "Wallet not found.";

                var wallet = await RefreshBalanceAsync(walletKey);
                var sendResult = await wallet.Send(200, address);
                if (sendResult.ResultCode == Lyra.Core.Blocks.APIResultCodes.Success)
                {
                    var sendResult2 = await wallet.Send(500, address, "Custom.Bitcoin");
                    return $"Succeful sent 200 Lyra.Coin to {address}.";
                }
                else
                {
                    return $"Failed to send coin: {sendResult.ResultCode}";
                }
            }
            catch(Exception ex)
            {
                return $"Fatal error: {ex.Message}";
            }
        }

        private async Task<Wallet> RefreshBalanceAsync(string masterKey)
        {
            // create wallet and update balance
            var memStor = new AccountInMemoryStorage();
            var acctWallet = new ExchangeAccountWallet(memStor, _network);
            acctWallet.AccountName = "tmpAcct";
            await acctWallet.RestoreAccountAsync("", masterKey);
            acctWallet.OpenAccount("", acctWallet.AccountName);

            Console.WriteLine("Sync wallet for " + acctWallet.AccountId);
            var rpcClient = await LyraRestClient.CreateAsync(_network, "Windows", "Lyra Client Cli", "1.0a");
            await acctWallet.Sync(rpcClient);
            return acctWallet;
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

    }
}