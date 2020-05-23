using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Cryptography;
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
using Lyra.Shared;
using Neo;
using System.Net.Http;

namespace LyraNodesBot
{
    public class NodesMonitor
    {
        private readonly TelegramBotClient Bot = new TelegramBotClient(System.IO.File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\telegram.txt"));

        private ChatId _groupId = new ChatId(-1001462436848);
        private string _network;
        private string apiHost = "seed2.testnet.wizdag.com";
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

        public async Task SendGroupMessageAsync(ChatId chatid, string msg)
        {
            var retryCount = 5;
            while(retryCount-- > 0)
            {
                try
                {
                    await Bot.SendTextMessageAsync(chatid, msg, ParseMode.Markdown);
                    break;
                }
                catch(Exception)
                {
                    await Task.Delay(2000);
                }
            }            
        }

        public async Task OnGossipMessageAsync(ChatId chatid, SourceSignedMessage msg)
        {
            var m = msg as ChatMsg;
            if (m == null)
                return;

            switch(m.MsgType)
            {
                case ChatMessageType.NodeUp:
                    await SendNodesInfoToGroupAsync(chatid);
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
                    await SendGroupMessageAsync(chatid, text2);
                    break;
            }
        }

        private async Task SendHeight(ChatId chatid)
        {
            var wc = new WebClient();
            var json = wc.DownloadString($"https://{apiHost}:4505/api/Node/GetSyncState");
            var bb = JsonConvert.DeserializeObject<GetSyncStateAPIResult>(json);

            //await SendGroupMessageAsync(chatid, $"Current Height: *{bb.NewestBlockUIndex}*");
        }
        private async Task SendNodesInfoToGroupAsync(ChatId chatid)
        {
            var wc = new WebClient();
            var json = wc.DownloadString($"https://{apiHost}:4505/api/Node/GetBillboard");
            var bb = JsonConvert.DeserializeObject<BillBoard>(json);
            var sb = new StringBuilder();

            sb.AppendLine($"*Consensus Algorithm (PBFT) Settings*");
            sb.AppendLine($"Total Needed Minimal Node Number: {ProtocolSettings.Default.ConsensusTotalNumber}");
            sb.AppendLine($"Consensus Win Number: {ProtocolSettings.Default.ConsensusWinNumber}");
            sb.AppendLine($"Maxmimum Tolerant Node Number: {ProtocolSettings.Default.ConsensusNumber}");
            sb.AppendLine($"Current Running Node Count: {bb.AllNodes.Count}");
            sb.AppendLine($"Current Nodes can do Authorizing: {bb.AllNodes.Count(a => a.Value.AbleToAuthorize)}");
            var cando = "unknown"; // bb.CanDoConsensus ? "Yes" : "No";
            sb.AppendLine($"Consensus Can be Made Now: {cando}");

            sb.AppendLine("\n*Primary Authorizers*\n");

            sb.AppendLine("`" + bb.PrimaryAuthorizers
                .Select((a, i) => $"{i}. {a.Shorten()} [{GetBalance(bb, a)}]")
                .Aggregate((c, d) => c + "\n" + d) + "`");

            sb.AppendLine("\n*Backup Authorizers*\n");

            if(bb.BackupAuthorizers.Length > 0)
            {
                sb.AppendLine("`" + bb.BackupAuthorizers
                    .Select((a, i) => $"{i}. {a.Shorten()} [{GetBalance(bb, a)}]")
                    .Aggregate((c, d) => c + "\n" + d) + "`");
            }
            else
            {
                sb.AppendLine("None");
            }

            sb.AppendLine("\n*Other Nodes*\n");

            var voting = bb.AllNodes.Keys.Where(a => !bb.PrimaryAuthorizers.Contains(a) && !bb.BackupAuthorizers.Contains(a));
            if(voting.Any())
            {
                sb.AppendLine("`" + voting
                    .Select((a, i) => $"{i}. {a.Shorten()} [{GetBalance(bb, a)}]")
                    .Aggregate((c, d) => c + "\n" + d) + "`");
            }
            else
            {
                sb.AppendLine("None");
            }

            await SendGroupMessageAsync(chatid, sb.ToString());
        }

        private string GetBalance(BillBoard bb, string accountId)
        {
            if (bb.PrimaryAuthorizers.Take(3).Contains(accountId))
                return "seed";

            var balance = bb.AllNodes[accountId].Balance;
            return $"{balance} Lyra";
        }

        private async void BotOnMessageReceived(object sender, MessageEventArgs messageEventArgs)
        {
            var message = messageEventArgs.Message;

            if (message == null || message.Type != MessageType.Text || !message.Text.StartsWith('/')) return;

            var args = message.Text.Split(' ', '@');
            switch (args.First())
            {
                case "/height":
                    await SendHeight(message.Chat.Id);
                    break;
                case "/nodes":
                    try
                    {
                        await SendNodesInfoToGroupAsync(message.Chat.Id);
                    }
                    catch(Exception ex)
                    {
                        await SendGroupMessageAsync(message.Chat.Id, $"Error: {ex.Message}");
                    }
                    break;
                case "/tps":
                    await SendGroupMessageAsync(message.Chat.Id, "No Data");
                    break;
                case "/send":
                    if (args.Length == 2 && Signatures.ValidateAccountId(args[1]))
                    {
                        var ret = await SendEvalCoin(args.Skip(1).First());
                        await SendGroupMessageAsync(message.Chat.Id, ret);
                    }
                    else
                    {
                        await SendGroupMessageAsync(message.Chat.Id, "*Example*\n\n/send [[your wallet address here]]");
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
                    await SendGroupMessageAsync(message.Chat.Id, usage);
                    break;
                case "/authlist":
                case "/authdelist":
                    await SendGroupMessageAsync(message.Chat.Id, "Under Construction");
                    break;
                case "/seed":
                case "/deseed":
                    if(message.From.Id == 397968968)      // @jfkwn
                    {
                        await SendGroupMessageAsync(message.Chat.Id, "Code todo");
                    }
                    else
                    {
                        await SendGroupMessageAsync(message.Chat.Id, "Only admins can do this");
                    }
                    break;
                default:
                    await SendGroupMessageAsync(message.Chat.Id, "Unknown command. Please reference to: /help");
                    break;
            }
        }

        private async Task<string> SendTpsAsync()
        {
            var url = "https://seed2.testnet.wizdag.com:4505/api/Node/GetTransStats";
            var wc = new HttpClient();
            var json = await wc.GetStringAsync(url);
            return json;
        }
        private async Task<string> SendEvalCoin(string address)
        {
            try
            {
                var walletKey = Environment.GetEnvironmentVariable("WIZDAG_BOT_WALLET_KEY");
                if (string.IsNullOrWhiteSpace(walletKey))
                    return "Wallet not found.";

                var wallet = await RefreshBalanceAsync(walletKey);
                var sendResult = await wallet.Send(1000, address);
                if (sendResult.ResultCode == Lyra.Core.Blocks.APIResultCodes.Success)
                {
                    var sendResult2 = await wallet.Send(500, address, "Custom.Bitcoin");
                    return $"Succeful sent 1000 Lyra.Coin to {address}.";
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
            var rpcClient = await LyraRestClient.CreateAsync(_network, "Windows", "Lyra Client Cli", "1.0a", "https://seed2.testnet.wizdag.com:4505/api/Node/");
            await acctWallet.Sync(rpcClient, true);
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