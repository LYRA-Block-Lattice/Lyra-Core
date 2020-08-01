using System;
using System.Collections.Generic;
using Lyra.Core.Blocks;
using Lyra.Core.API;
using System.Threading.Tasks;
using Lyra.Core.Accounts;
using Neo.Cryptography.ECC;
using Lyra.Core.Cryptography;
using System.Linq;
using System.Security.Cryptography;
using Lyra.Shared;

namespace Lyra.Client.CLI
{
    public class CommandProcessor
    {
        public const string COMMAND_HELP = "help";
        public const string COMMAND_STOP = "stop";
        public const string COMMAND_BALANCE = "balance";
        public const string COMMAND_COUNT = "count";
        public const string COMMAND_ACCOUNT_ID = "id";
        public const string COMMAND_PRIVATE_KEY = "key";
        public const string COMMAND_SEND = "send";
        public const string COMMAND_PAY = "pay";
        public const string COMMAND_SELL = "sell";
        public const string COMMAND_STATUS = "status";
        public const string COMMAND_TOKEN = "token";
        public const string COMMAND_GEN_NOTE = "gen";
        public const string COMMAND_PRINT_LAST_BLOCK = "last";
        public const string COMMAND_PRINT_BLOCK = "print";
        public const string COMMAND_SYNC = "sync";
        public const string COMMAND_RESYNC = "resync";
        public const string COMMAND_TRADE_ORDER = "trade";
        public const string COMMAND_TRADE_ORDER_SELL_TEST = "trade-sell-test";
        public const string COMMAND_TRADE_ORDER_BUY_TEST = "trade-buy-test";
        public const string COMMAND_CANCEL_TRADE_ORDER = "cancel";
        public const string COMMAND_PRINT_ACTIVE_TRADE_ORDER_LIST = "orders";
        public const string COMMAND_REDEEM_REWARDS = "redeem";
        public const string COMMAND_VOTEFOR = "votefor";
        public const string COMMAND_SYNCFEE = "syncfee";

        // set wallet's private key
        public const string COMMAND_RESTORE = "restore";

        public const string UNSUPPORTED_COMMAND_MSG = "This command is in development";

        readonly Wallet _wallet;

        public CommandProcessor(Wallet w)
        {
            _wallet = w;
        }

        public async Task<int> Execute(string command)
        {
            try
            {
                if (string.IsNullOrEmpty(command))
                    return 0;

                switch (command)
                {
                    case COMMAND_STOP:
                        break;
                    case COMMAND_HELP:
                        Console.WriteLine(string.Format(@"{0,10}: List all client commands", COMMAND_HELP));
                        Console.WriteLine(string.Format(@"{0,10}: Display all account balances", COMMAND_BALANCE));
                        Console.WriteLine(string.Format(@"{0,10}: Display the number of transaction blocks in the account", COMMAND_COUNT));
                        Console.WriteLine(string.Format(@"{0,10}: Display Account Id (aka ""wallet address"" or ""public key"")", COMMAND_ACCOUNT_ID));
                        Console.WriteLine(string.Format(@"{0,10}: Display Account Private Key", COMMAND_PRIVATE_KEY));
                        Console.WriteLine(string.Format(@"{0,10}: DPoS: Set Vote for Account Id", COMMAND_VOTEFOR));
                        Console.WriteLine(string.Format(@"{0,10}: Transfer funds to another account", COMMAND_SEND));
                        //Console.WriteLine(string.Format(@"{0,10}: Pay to a merchant", COMMAND_PAY));
                        //Console.WriteLine(string.Format(@"{0,10}: Accept payment from a buyer", COMMAND_SELL));
                        Console.WriteLine(string.Format(@"{0,10}: Display the account status summary", COMMAND_STATUS));
                        //Console.WriteLine(string.Format(@"{0,10}: Place a trade order", COMMAND_TRADE_ORDER));
                        //Console.WriteLine(string.Format(@"{0,10}: Cancel trade order", COMMAND_CANCEL_TRADE_ORDER));
                        Console.WriteLine(string.Format(@"{0,10}: Redeem reward tokens to get a discount token", COMMAND_REDEEM_REWARDS));
                        Console.WriteLine(string.Format(@"{0,10}: Create a new custom digital asset (token)", COMMAND_TOKEN));
                        Console.WriteLine(string.Format(@"{0,10}: Print last transaction block", COMMAND_PRINT_LAST_BLOCK));
                        Console.WriteLine(string.Format(@"{0,10}: Print transaction block", COMMAND_PRINT_BLOCK));
                        Console.WriteLine(string.Format(@"{0,10}: Print the list of active reward orders", COMMAND_PRINT_ACTIVE_TRADE_ORDER_LIST));
                        Console.WriteLine(string.Format(@"{0,10}: Sync up with the node", COMMAND_SYNC));
                        Console.WriteLine(string.Format(@"{0,10}: Sync up authorizer node's fees", COMMAND_SYNCFEE));
                        Console.WriteLine(string.Format(@"{0,10}: Reset and do sync up with the node", COMMAND_RESYNC));
                        Console.WriteLine(string.Format(@"{0,10}: Exit this app", COMMAND_STOP));
                        //Console.WriteLine(string.Format(@"{0,10}: Generate a network genesis block (testnet only)", COMMAND_GEN_NOTE));

                        //Console.WriteLine(string.Format(@"{0,10}: Restore account using private key", COMMAND_RESTORE));
                        //Console.WriteLine(string.Format("{0,15}: Sync up the account with the node to check if there are incoming transactions", COMMAND_SYNC));
                        break;
                    case COMMAND_ACCOUNT_ID:
                        //var ep = Neo.Cryptography.ECC.ECPoint.FromBytes(Base58Encoding.DecodeAccountId(_wallet.AccountId), Neo.Cryptography.ECC.ECCurve.Secp256r1);
                        //Console.WriteLine(ep.ToString());
                        Console.WriteLine(_wallet.AccountId);
                        break;
                    case COMMAND_BALANCE:
                        Console.WriteLine(_wallet.GetDisplayBalances());
                        break;
                    case COMMAND_COUNT:
                        Console.WriteLine(_wallet.GetLocalAccountHeight());
                        break;
                    case COMMAND_STATUS:
                        Console.WriteLine(string.Format("Network Id: {0}", _wallet.NetworkId));
                        Console.WriteLine(string.Format("Account Id: {0}", _wallet.AccountId));
                        Console.WriteLine($"Current voting for Account Id: {_wallet.VoteFor ?? "(Not Set)"}");
                        Console.WriteLine(string.Format("Number of Blocks: {0}", _wallet.GetLocalAccountHeight()));
                        Console.WriteLine(              "Balance: " + _wallet.GetDisplayBalances());
                        Console.WriteLine("Last Status Block: ");
                        Console.WriteLine((await _wallet.GetLastServiceBlockAsync()).Print());
                        break;
                    case COMMAND_PRIVATE_KEY:
                        Console.WriteLine(string.Format(_wallet.PrivateKey));
                        break;
                    case COMMAND_VOTEFOR:
                        Console.WriteLine("Please the account id you want vote for, or enter for not vote: ");
                        string votefor = Console.ReadLine();
                        Console.WriteLine("Your input is: " + (string.IsNullOrEmpty(votefor) ? "(empty)" : votefor));
                        if(Signatures.ValidateAccountId(votefor))
                        {
                            _wallet.VoteFor = votefor;
                            Console.WriteLine($"You will vote for {votefor}. The vote will take effect after next transaction (send/receive etc.).");
                        }
                        else if(string.IsNullOrEmpty(votefor))
                        {
                            _wallet.VoteFor = null;
                            Console.WriteLine($"You will not vote for any account id. This action will take effect after next transaction (send/receive etc.).");
                        }
                        else
                        {
                            Console.WriteLine("Invalid vote for account id.");
                        }
                        break;
                    case COMMAND_TOKEN:
                        await ProcessNewTokenAsync();
                        break;
                    case COMMAND_SEND:
                        await ProcessSendAsync();
                        break;
                    case COMMAND_GEN_NOTE:
                        await _wallet.CreateGenesisForCoreTokenAsync();
                        break;
                    case COMMAND_PRINT_LAST_BLOCK:
                        Console.WriteLine(_wallet.PrintLastBlock());
                        break;
                    case COMMAND_PRINT_BLOCK:
                        Console.WriteLine("Please enter transaction block index: ");
                        string blockindex = Console.ReadLine();
                        Console.WriteLine(_wallet.PrintBlock(blockindex));
                        break;
                    case COMMAND_SYNC:
                        var sync_result = await _wallet.Sync(null);
                        Console.WriteLine("Sync Result: " + sync_result.ToString());
                        break;
                    case COMMAND_RESYNC:
                        var sync_result2 = await _wallet.Sync(null, true);
                        Console.WriteLine("Sync Result: " + sync_result2.ToString());
                        break;
                    case COMMAND_SYNCFEE:
                        var sfeeResult = await _wallet.SyncNodeFees();
                        Console.WriteLine($"Sync Fees Result: {sfeeResult}");
                        break;
                    //case COMMAND_TRADE_ORDER:
                    //    //Console.WriteLine(UNSUPPORTED_COMMAND_MSG);
                    //    ProcessTradeOrder();
                        break;
                    case COMMAND_REDEEM_REWARDS:
                        ProcessRedeemRewardsTradeOrder();
                        break;
                    case COMMAND_PRINT_ACTIVE_TRADE_ORDER_LIST:
                        //Console.WriteLine(UNSUPPORTED_COMMAND_MSG);
                        Console.WriteLine(await _wallet.PrintActiveTradeOrdersAsync());
                        break;
                    //                  case COMMAND_TRADE_ORDER_SELL_TEST:
                    //                      //Console.WriteLine(UNSUPPORTED_COMMAND_MSG);
                    ////                    TradeOrderSellTest();
                    //                      break;
                    //                  case COMMAND_TRADE_ORDER_BUY_TEST:
                    //                      TradeOrderBuyTest();
                    //                      //Console.WriteLine(UNSUPPORTED_COMMAND_MSG);
                    //                      break;
                    case COMMAND_PAY:
                    case COMMAND_SELL:
                    case COMMAND_CANCEL_TRADE_ORDER:
                        Console.WriteLine(UNSUPPORTED_COMMAND_MSG);
                        break;
                    default:
                        Console.WriteLine(string.Format("Unknown command: {0}", command));
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(string.Format("Error: {0}", e.Message));
            }
            return 0;
        }

       
        /*        void TradeOrderSellTest()
                {
                    var orderType = TradeOrderTypes.Sell;
                    var sell_token = "USD";
                    var buy_token = LyraGlobal.LYRA_TICKER_CODE;
                    var max_amount = 5;
                    var price = 10;
                    var result = _wallet.TradeOrder(orderType, sell_token, buy_token, max_amount, 0, price, true, true).Result;
                    Console.WriteLine($"Result code: {result.ResultCode}");
                    Console.WriteLine($"Result Message: {result.ResultMessage}");
                }

                void TradeOrderBuyTest()
                {
                    var orderType = TradeOrderTypes.Buy;
                    var buy_token = "USD";
                    var sell_token = LyraGlobal.LYRA_TICKER_CODE;
                    var max_amount = 5;
                    var price = 10;
                    var result = _wallet.TradeOrder(orderType, sell_token, buy_token, max_amount, 0, price, false, false).Result;
                    Console.WriteLine($"Result code: {result.ResultCode}");
                    Console.WriteLine($"Result Message: {result.ResultMessage}");
                }

                void ProcessTradeOrder()
                {
                    Console.WriteLine("Please enter \"s\" for Sell order or \"b\" for Buy order: ");
                    string ordertype = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(ordertype) || (ordertype != "s" && ordertype != "b"))
                    {
                        Console.WriteLine($"Invalid order type");
                        //Console.Write(string.Format("{0}> ", _wallet.AccountName));
                        return;
                    }

                    TradeOrderTypes orderType = ordertype == "s" ? TradeOrderTypes.Sell : TradeOrderTypes.Buy;

                    decimal max_amount;
                    decimal price;
                    string sell_token;
                    string buy_token;
                    string amountstr;

                    if (orderType == TradeOrderTypes.Sell)
                    {
                        Console.WriteLine("Sell token code: ");
                        sell_token = Console.ReadLine();

                        Console.WriteLine("Sale Amount: ");
                        amountstr = Console.ReadLine();
                        decimal.TryParse(amountstr, out max_amount);

                        Console.WriteLine("Buy Token Code: ");
                        buy_token = Console.ReadLine();

                        Console.WriteLine("Price (how much \"buy\" tokens you would like to get for one token you sell: ");
                        amountstr = Console.ReadLine();
                        decimal.TryParse(amountstr, out price);
                    }
                    else
                    {
                        Console.WriteLine("Buy Token Code: ");
                        buy_token = Console.ReadLine();

                        Console.WriteLine("Buy Amount: ");
                        amountstr = Console.ReadLine();
                        decimal.TryParse(amountstr, out max_amount);

                        Console.WriteLine("Sell token code: ");
                        sell_token = Console.ReadLine();

                        Console.WriteLine("Price (how much tokens you would like to pay for one token you buy: ");
                        amountstr = Console.ReadLine();
                        decimal.TryParse(amountstr, out price);

                    }

                    var result = _wallet.TradeOrder(orderType, sell_token, buy_token, max_amount, 0, price, false, true).Result;
                    if (result.ResultCode != APIResultCodes.Success)
                    {
                        Console.WriteLine($"Failed to add trade order block with error code: {result.ResultCode}");
                        Console.WriteLine("Error Message: " + result.ResultMessage);
                    }
                    else
                    {
                        Console.WriteLine($"Trade Order has been authorized successfully");
                        Console.WriteLine("Balance: " + _wallet.GetDisplayBalances());
                    }
                    //Console.Write(string.Format("{0}> ", _wallet.AccountName));
                }
         */
        void ProcessRedeemRewardsTradeOrder()
        {
            decimal discount_amount;
            string reward_token_code;

            Console.WriteLine("Reward Token Code: ");
            reward_token_code = Console.ReadLine();

            Console.WriteLine("Desired discount $$ Amount: ");
            string amountstr = Console.ReadLine();
            decimal.TryParse(amountstr, out discount_amount);

            var result = _wallet.RedeemRewards(reward_token_code, discount_amount).Result;

            if (result.ResultCode == APIResultCodes.Success)
            {
                Console.WriteLine("Redemption process has been initiated successfully.");
            }
            else
            {
                    Console.WriteLine("Redemption failed:" + result.ResultMessage);
            }
        }
      
        async Task ProcessSendAsync()
        {
            Console.WriteLine("Please enter destination account id: ");
            string destination = Console.ReadLine();
            Console.WriteLine("Please enter amount: ");
            string amountstr = Console.ReadLine();
            decimal.TryParse(amountstr, out decimal amount);

            string ticker = null;
            if (_wallet.NumberOfNonZeroBalances > 1)
            {
                Console.WriteLine(string.Format("Please select token to send, or press Enter for {0}: ", LyraGlobal.OFFICIALTICKERCODE));
                ticker = Console.ReadLine();

            }

            APIResultCodes send_result;
            if (string.IsNullOrEmpty(ticker))
                send_result = (await _wallet.Send(amount, destination)).ResultCode;
            else
                send_result = (await _wallet.Send(amount, destination, ticker)).ResultCode;

            if (send_result != APIResultCodes.Success)
            {
                Console.WriteLine($"Failed to add send transfer block with error code: {send_result}");
            }
            else
            {
                Console.WriteLine($"Send Transfer block has been authorized successfully");
                Console.WriteLine("Balance: " + _wallet.GetDisplayBalances());
            }
            //Console.Write(string.Format("{0}> ", _wallet.AccountName));
        }

        async Task ProcessNewTokenAsync()
        {
            Console.WriteLine("Please enter token name: ");
            string tokenname = Console.ReadLine();

            Console.WriteLine("Please enter domain name (optional): ");
            string domainname = Console.ReadLine();

            Console.WriteLine("Please enter description (optional): ");
            string desc = Console.ReadLine();

            Console.WriteLine("Please enter total supply (0 - 100,000,000,000,000: ");
            string supply = Console.ReadLine();

            Console.WriteLine("Please enter precision (0 - 8): ");
            string precision = Console.ReadLine();

            Console.WriteLine("Is it final supply? (Y/n): ");
            bool isFinalSupply = ReadYesNoAnswer();

            Console.WriteLine("Please enter owner name (optional): ");
            string owner = Console.ReadLine();

            Console.WriteLine("Please enter owner address (optional): ");
            string address = Console.ReadLine();

            string tag_key = "no tag";
            Dictionary<string, string> tags = null;

            while (!string.IsNullOrWhiteSpace(tag_key))
            {
                Console.WriteLine("Please enter tag key (optional): ");
                tag_key = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(tag_key))
                    break;
                Console.WriteLine("Please enter tag value (or press Enter to skip): ");
                string tag_value = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(tag_value))
                    break;
                if (tags == null)
                    tags = new Dictionary<string, string>();
                tags.Add(tag_key, tag_value);
            }

            //if (string.IsNullOrWhiteSpace(domainname))
            //    domainname = "Custom";

            //string ticker = domainname + "." + tokenname;


            var result = _wallet.CreateToken(tokenname, domainname, desc, Convert.ToSByte(precision), Convert.ToDecimal(supply), isFinalSupply, owner, address, null, ContractTypes.Custom, tags).Result;

            if (result.ResultCode != APIResultCodes.Success)
            {
                Console.WriteLine("Token generation failed with code: " + result.ResultCode.ToString());
            }
            else
            {
                Console.WriteLine($"Token generation has been authorized successfully");
                Console.WriteLine("Balance: " + _wallet.GetDisplayBalances());
            }
            //Console.Write(string.Format("{0}> ", _wallet.AccountName));

        }


        public bool ReadYesNoAnswer()
        {
            string answer = Console.ReadLine();
            if (string.IsNullOrEmpty(answer) || answer.ToLower() == "y" || answer.ToLower() == "yes")
                return true;
            return false;
        }



    }
}
