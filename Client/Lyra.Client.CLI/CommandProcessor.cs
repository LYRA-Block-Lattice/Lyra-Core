using System;
using System.Collections.Generic;
using Lyra.Core.Blocks;
using Lyra.Core.API;
using System.Threading.Tasks;
using Lyra.Core.Accounts;
using Neo.Cryptography.ECC;
using Lyra.Data.Crypto;
using System.Linq;
using System.Security.Cryptography;
using Lyra.Shared;
using System.Data;

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
        public const string COMMAND_SYNC_SHORT = "s";
        public const string COMMAND_TRADE_ORDER = "trade";
        public const string COMMAND_TRADE_ORDER_SELL_TEST = "trade-sell-test";
        public const string COMMAND_TRADE_ORDER_BUY_TEST = "trade-buy-test";
        public const string COMMAND_CANCEL_TRADE_ORDER = "cancel";
        public const string COMMAND_PRINT_ACTIVE_TRADE_ORDER_LIST = "orders";
        public const string COMMAND_REDEEM_REWARDS = "redeem";
        public const string COMMAND_VOTEFOR = "votefor";
        public const string COMMAND_SYNCFEE = "syncfee";
        public const string COMMAND_IMPORT_ACCOUNT = "import";

        // Generate new NFT genesis
        public const string COMMAND_CREATE_NFT = "createnft";

        // Issues an new instance if NFT does not exists.
        public const string COMMAND_ISSUE_NFT = "issuenft";

        // Makes NFT transfer if NFT exists;
        public const string COMMAND_SEND_NFT = "sendnft";

        // Shows all NFT instances owned by the account
        public const string COMMAND_SHOW_NFT = "nft";

        // set wallet's private key
        public const string COMMAND_RESTORE = "restore";

        // create liquidate pool
        public const string COMMAND_CREATE_POOL = "createpool";

        public const string COMMAND_HISTORY = "history";
        public const string COMMAND_HISTORY_SHORT = "h";

        public const string UNSUPPORTED_COMMAND_MSG = "This command is in development";

        Wallet _wallet;

        public CommandProcessor()
        {

        }

        public async Task<int> Execute(Wallet wallet, string command)
        {
            _wallet = wallet;
            try
            {
                if (string.IsNullOrEmpty(command))
                    return 0;

                switch (command)
                {
                    case COMMAND_STOP:
                        break;
                    case COMMAND_HELP:
                        Console.WriteLine(string.Format(@"{0,10}: Show the list of all wallet commands", COMMAND_HELP));
                        Console.WriteLine(string.Format(@"{0,10}: Show all account balances", COMMAND_BALANCE));
                        Console.WriteLine(string.Format(@"{0,10}: Show the number of transaction blocks in the account", COMMAND_COUNT));
                        Console.WriteLine(string.Format(@"{0,10}: Show Account Id (aka ""wallet address"" or ""public key"")", COMMAND_ACCOUNT_ID));
                        Console.WriteLine(string.Format(@"{0,10}: Show Account Private Key", COMMAND_PRIVATE_KEY));
                        Console.WriteLine(string.Format(@"{0,10}: DPoS: Set Vote for Account Id", COMMAND_VOTEFOR));
                        Console.WriteLine(string.Format(@"{0,10}: Transfer funds to another account", COMMAND_SEND));
                        Console.WriteLine(string.Format(@"{0,10}: Transfer collectible NFT to another account", COMMAND_SEND_NFT));
                        //Console.WriteLine(string.Format(@"{0,10}: Pay to a merchant", COMMAND_PAY));
                        //Console.WriteLine(string.Format(@"{0,10}: Accept payment from a buyer", COMMAND_SELL));
                        Console.WriteLine(string.Format(@"{0,10}: Show the account status summary", COMMAND_STATUS));
                        //Console.WriteLine(string.Format(@"{0,10}: Place a trade order", COMMAND_TRADE_ORDER));
                        //Console.WriteLine(string.Format(@"{0,10}: Cancel trade order", COMMAND_CANCEL_TRADE_ORDER));
                        Console.WriteLine(string.Format(@"{0,10}: Show transaction history", COMMAND_HISTORY));
                        Console.WriteLine(string.Format(@"{0,10}: Redeem reward tokens to get a discount token", COMMAND_REDEEM_REWARDS));
                        Console.WriteLine(string.Format(@"{0,10}: Import account into current wallet account", COMMAND_IMPORT_ACCOUNT));
                        Console.WriteLine(string.Format(@"{0,10}: Create a new fungible token", COMMAND_TOKEN));
                        Console.WriteLine(string.Format(@"{0,10}: Create a new collectible NFT (non-fungible token)", COMMAND_CREATE_NFT));
                        Console.WriteLine(string.Format(@"{0,10}: Issue a new collectible NFT instance", COMMAND_ISSUE_NFT));
                        Console.WriteLine(string.Format(@"{0,10}: Show all NFT instances owned by the account", COMMAND_SHOW_NFT));
                        Console.WriteLine(string.Format(@"{0,10}: Show last transaction block", COMMAND_PRINT_LAST_BLOCK));
                        Console.WriteLine(string.Format(@"{0,10}: Show transaction block with specified index", COMMAND_PRINT_BLOCK));
                        Console.WriteLine(string.Format(@"{0,10}: Show the list of active reward orders", COMMAND_PRINT_ACTIVE_TRADE_ORDER_LIST));
                        Console.WriteLine(string.Format(@"{0,10}: Sync up with the node (receive transfers)", COMMAND_SYNC));
                        Console.WriteLine(string.Format(@"{0,10}: Sync up authorizer node's fees", COMMAND_SYNCFEE));
                        //Console.WriteLine(string.Format(@"{0,10}: Reset and do sync up with the node", COMMAND_RESYNC));
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
                        Console.WriteLine(await _wallet.GetDisplayBalancesAsync());
                        break;
                    case COMMAND_COUNT:
                        Console.WriteLine(_wallet.GetLocalAccountHeight());
                        break;
                    case COMMAND_STATUS:
                        Console.WriteLine(string.Format("Network Id: {0}", _wallet.NetworkId));
                        Console.WriteLine(string.Format("Account Id: {0}", _wallet.AccountId));
                        Console.WriteLine($"Current voting for Account Id: {_wallet.VoteFor ?? "(Not Set)"}");
                        Console.WriteLine(string.Format("Number of Blocks: {0}", _wallet.GetLocalAccountHeight()));
                        Console.WriteLine(              "Balance: " + await _wallet.GetDisplayBalancesAsync());
                        //Console.WriteLine("Last Status Block: ");
                        //Console.WriteLine((await _wallet.GetLastServiceBlockAsync()).Print());
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
                            _wallet.VoteFor = "";
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
                    case COMMAND_CREATE_NFT:
                        await ProcessNewNFTAsync();
                        break;
                    case COMMAND_ISSUE_NFT:
                        await ProcessSendNFTAsync(true);
                        break;
                    case COMMAND_SEND_NFT:
                        await ProcessSendNFTAsync(false);
                        break;
                    case COMMAND_SHOW_NFT:
                        Console.WriteLine(await GetDisplayNFTAsync());
                        break;
                    case COMMAND_SEND:
                        await ProcessSendAsync();
                        break;
                    //case COMMAND_GEN_NOTE:
                    //    await _wallet.CreateGenesisForCoreTokenAsync();
                    //    break;
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
                    //case COMMAND_RESYNC:
                    //    var sync_result2 = await _wallet.Sync(null);
                    //    Console.WriteLine("Sync Result: " + sync_result2.ToString());
                    //    break;
                    case COMMAND_SYNCFEE:
                        var sfeeResult = await _wallet.SyncNodeFees();
                        Console.WriteLine($"Sync Fees Result: {sfeeResult}");
                        break;
                    //case COMMAND_TRADE_ORDER:
                    //    //Console.WriteLine(UNSUPPORTED_COMMAND_MSG);
                    //    ProcessTradeOrder();
                   //     break;
                    case COMMAND_REDEEM_REWARDS:
                        ProcessRedeemRewardsTradeOrder();
                        break;
                    case COMMAND_IMPORT_ACCOUNT:
                        Console.WriteLine("Please enter private key of the account to import: ");
                        string imported_private_key = Console.ReadLine();
                        var import_result = await _wallet.ImportAccount(imported_private_key);
                        Console.WriteLine("Import Result: " + import_result.ResultCode.ToString());
                        break;
                    case COMMAND_CREATE_POOL:
                        Console.WriteLine("Create liquidate pool for two tokens. The first token is:");
                        var token0 = Console.ReadLine();
                        Console.WriteLine("The second token is:");
                        var token1 = Console.ReadLine();

                        var lp = await _wallet.GetLiquidatePoolAsync(token0, token1);
                        if(lp == null)
                        {
                            Console.WriteLine($"No liquidate pool for {token0} and {token1}. Would you like create a pool for it? It cost 1000 LYR to create a pool.");
                            if(ReadYesNoAnswer())
                            {
                                var tags = new Dictionary<string, string>();
                                tags.Add("token0", token0);
                                tags.Add("token1", token1);
                                var amounts = new Dictionary<string, decimal>();
                                amounts.Add(LyraGlobal.OFFICIALTICKERCODE, 1000m);
                                var poolCreateResult = await _wallet.SendEx(lp.PoolFactoryAccountId, amounts, tags);
                                if(poolCreateResult.ResultCode == APIResultCodes.Success)
                                {
                                    Console.WriteLine("Liquidate pool creating in progress...");

                                    PoolInfoAPIResult lpNew = null;
                                    while(true)
                                    {
                                        lpNew = await _wallet.GetLiquidatePoolAsync(token0, token1);
                                        if (lpNew.Successful())
                                            break;
                                        else
                                        {
                                            Console.Write(".");
                                            await Task.Delay(2000);
                                        }
                                    }
                                    Console.WriteLine("Liquidate pool created successfully.");
                                    Console.WriteLine($"How many {token0} will you add to the pool:");
                                    var token0Amount = decimal.Parse(Console.ReadLine());
                                    Console.WriteLine($"How many {token1} will you add to the pool:");
                                    var token1Amount = decimal.Parse(Console.ReadLine());

                                    var amountsDeposit = new Dictionary<string, decimal>();
                                    amountsDeposit.Add(token0, token0Amount);
                                    amountsDeposit.Add(token1, token1Amount);
                                    var poolDepositResult = await _wallet.SendEx(lp.PoolAccountId, amountsDeposit, tags);

                                    if(poolDepositResult.Successful())
                                    {
                                        var poolResult2 = await _wallet.GetLiquidatePoolAsync(token0, token1);
                                        if(poolResult2.Successful())
                                        {
                                            var poolBlock = poolResult2.GetBlock() as PoolBlock;
                                            Console.WriteLine($"Your deposition is successed. Your share on the liquidate pool is {poolBlock.Shares[_wallet.AccountId].ToBalanceDecimal() * 100} %");
                                        }                                        
                                    }
                                }
                            }
                        }
                        break;
                    case COMMAND_HISTORY:
                    case COMMAND_HISTORY_SHORT:
                        await ProcessHistoryAsync();
                        break;
                    case COMMAND_PRINT_ACTIVE_TRADE_ORDER_LIST:
                        //Console.WriteLine(UNSUPPORTED_COMMAND_MSG);
                        //Console.WriteLine(await _wallet.PrintActiveTradeOrdersAsync());
                        Console.WriteLine(UNSUPPORTED_COMMAND_MSG);
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
            if (_wallet.GetNumberOfNonZeroBalances() < 1)
            {
                Console.WriteLine($"Insufficient balance");
                return;
            }

            Console.WriteLine("Please enter destination account id: ");
            string destination = Console.ReadLine();
            Console.WriteLine("Please enter amount: ");
            string amountstr = Console.ReadLine();
            decimal.TryParse(amountstr, out decimal amount);

            string ticker = null;
            if (_wallet.GetNumberOfNonZeroBalances() > 1)
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
                Console.WriteLine("Balance: " + await _wallet.GetDisplayBalancesAsync());
            }
            //Console.Write(string.Format("{0}> ", _wallet.AccountName));
        }

        // If NFT with SerialNumber already exists and belongs to the account, it will send this NFT to DestinationAccount;
        // If NFT with SerialNumber does not exists, it will attempt to issue a new instance of NFT and send it to DestinationAccount
        async Task ProcessSendNFTAsync(bool IssueNewNFTInstance)
        {
            if (_wallet.GetNumberOfNonZeroBalances() < 2)
            {
                Console.WriteLine($"Insufficient balance");
                return;
            }

            Console.WriteLine("Please enter destination account id: ");
            string destination = Console.ReadLine();
            Console.WriteLine("Please enter NFT name: ");
            string ticker = Console.ReadLine();
            Console.WriteLine("Please enter serial number: ");
            string serial_number = Console.ReadLine();

            APIResultCodes result;

            if (IssueNewNFTInstance)
                result = (await _wallet.IssueNFT(destination, ticker, serial_number)).ResultCode;
            else
                result = (await _wallet.SendNFT(destination, ticker, serial_number)).ResultCode;

            if (result != APIResultCodes.Success)
            {
                Console.WriteLine($"Failed to send NFT with error code: {result}");
            }
            else
            {
                Console.WriteLine($"NFT instance has been sent successfully");
                //Console.WriteLine("Balance: " + await _wallet.GetDisplayBalancesAsync());
            }
        }

        async Task ProcessNewTokenAsync()
        {
            if (_wallet.GetNumberOfNonZeroBalances() < 1)
            {
                Console.WriteLine($"Insufficient balance");
                return;
            }

            Console.WriteLine("Please enter token name ( minimum 2 characters ): ");
            string tokenname = Console.ReadLine();

            Console.WriteLine("Please enter domain name ( minimum 6 characters ): ");
            string domainname = Console.ReadLine();

            Console.WriteLine("Please enter description (optional): ");
            string desc = Console.ReadLine();

            Console.WriteLine("Please enter total supply ( maximum 90,000,000,000 ): ");
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
                Console.WriteLine("Balance: " + await _wallet.GetDisplayBalancesAsync());
            }
            //Console.Write(string.Format("{0}> ", _wallet.AccountName));

        }

        async Task ProcessNewNFTAsync()
        {
            Console.WriteLine("Please enter token name ( minimum 3 characters ): ");
            string tokenname = Console.ReadLine();

            Console.WriteLine("Please enter domain name ( minimum 6 characters ): ");
            string domainname = Console.ReadLine();

            Console.WriteLine("Please enter description (optional): ");
            string desc = Console.ReadLine();

            Console.WriteLine("Please enter total supply ( maximum 90,000,000,000 ): ");
            string supply = Console.ReadLine();

            Console.WriteLine("Is it final supply? (Y/n): ");
            bool isFinalSupply = ReadYesNoAnswer();

            Console.WriteLine("Please enter owner name (optional): ");
            string owner = Console.ReadLine();

            Console.WriteLine("Please enter owner address (optional): ");
            string address = Console.ReadLine();

            Console.WriteLine("Please enter icon image URL (optional): ");
            string icon = Console.ReadLine();

            Console.WriteLine("Please enter image URL (optional): ");
            string image = Console.ReadLine();

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

            var result = _wallet.CreateNFT(tokenname, domainname, desc, Convert.ToDecimal(supply), isFinalSupply, owner, address, icon, image, tags).Result;

            if (result.ResultCode != APIResultCodes.Success)
            {
                Console.WriteLine("NFT generation failed with code: " + result.ResultCode.ToString());
            }
            else
            {
                Console.WriteLine($"NFT generation has been authorized successfully");
                Console.WriteLine("Balance: " + await _wallet.GetDisplayBalancesAsync());
            }
        }

        async Task ProcessHistoryAsync()
        {
            Console.WriteLine("Please enter starting index or press Enter to show last transactions: ");
            string index = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(index))
                Console.WriteLine(await GetDisplayTransactionHistoryAsync());
            else
            {
                long start_index = Convert.ToInt64(index);
                Console.WriteLine(await GetDisplayTransactionHistoryAsync(start_index));
            }
        }

        // Shows all NFT instances owned by the account
        public async Task<string> GetDisplayNFTAsync()
        {
            string res = "No NFT found";

            var nft_list = await _wallet.GetNonFungibleTokensAsync();
            if (nft_list == null)
                return res;

            res = string.Empty;

            foreach (var nft in nft_list)
            {
                res += await _wallet.GetDisplayNFTInstanceAsync(nft);
                res += $"\n";
            }

            return res;
        }

            // Displays 50 last transactions starting from start_height
        public async Task<string> GetDisplayTransactionHistoryAsync(long start_height = 0)
        {
            const int MAX_TRANSACTIONS_TO_DISPLAY = 50;
            string res = "No transactions found";
            TransactionBlock lastBlock = _wallet.GetLatestBlock();
            if (lastBlock == null)
                return res;

            if (start_height < 0 || start_height > lastBlock.Height)
                start_height = 0;

            long end_height = 0;

            if (start_height == 0) // just 20 last transactions
            {
                end_height = lastBlock.Height;
                start_height = end_height - MAX_TRANSACTIONS_TO_DISPLAY + 1;
                if (start_height < 1)
                    start_height = 1;
            }
            else
            {
                end_height = start_height + MAX_TRANSACTIONS_TO_DISPLAY - 1;
                if (end_height > lastBlock.Height)
                    end_height = lastBlock.Height;
            }
             
            TransactionBlock block = null;
            TransactionBlock previous_block = null;

            const string INDEX_LABEL = "#";
            const string DATETIME_LABEL = "Date       Time ";
            const string TRANSACTION_LABEL = "Transaction";
            const string AMOUNT_LABEL = "Amount";
            const string TOKEN_LABEL = "Token";
            const string BALANCE_LABEL = "Balance";
            const string FEE_LABEL = "Fee";
            const string HASH_LABEL = "Hash";

            int index_width = INDEX_LABEL.Length;
            int datetime_width = DATETIME_LABEL.Length;
            int transaction_width = TRANSACTION_LABEL.Length;
            int amount_width = AMOUNT_LABEL.Length;
            int token_width = TOKEN_LABEL.Length;
            int balance_width = BALANCE_LABEL.Length;
            int fee_width = FEE_LABEL.Length;
            int hash_width = HASH_LABEL.Length;

            var blocks = new Dictionary<long, TransactionBlock>();
            
            if (start_height > 1) // prepare the first previous block 
            {
                block = await _wallet.GetBlockByIndex(start_height - 1);
                blocks.Add(start_height - 1, block);
            }

            // find the max width of each variable field so we could format them nicely
            for (long i = start_height; i <= end_height; i++)
            {
                previous_block = block;
                block = await _wallet.GetBlockByIndex(i);
                if (block == null)
                    continue;
                blocks.Add(i, block);
                if (i.ToString().Length > index_width)
                    index_width = i.ToString().Length;

                if (GetTransactionName(block).Length > transaction_width)
                    transaction_width = GetTransactionName(block).Length;

                var transaction = block.GetTransaction(previous_block);
                if (transaction != null)
                {
                    if (String.Format("{0:n}", transaction.Amount).Length > amount_width)
                        amount_width = String.Format("{0:n}", transaction.Amount).Length;

                    if (transaction.TokenCode.Length > token_width)
                        token_width = transaction.TokenCode.Length;

                    if (block.Balances.Count > 0 && block.Balances.ContainsKey(transaction.TokenCode))
                    {
                        if (String.Format("{0:n}", block.Balances[transaction.TokenCode].ToBalanceDecimal()).Length > balance_width)
                            balance_width = String.Format("{0:n}", block.Balances[transaction.TokenCode].ToBalanceDecimal()).Length;
                    }

                    if (String.Format("{0:n}", block.Fee).Length > fee_width)
                        fee_width = String.Format("{0:n}", block.Fee).Length;
                }
            }

            res = $"\n";
            //res += $"Index Date       Time  Transaction           Amount                         Token               Balance           Fee    Hash";
            res += string.Format($"{{0,{-index_width}}} ", INDEX_LABEL);
            res += string.Format($"{{0,{datetime_width}}} ", DATETIME_LABEL);
            res += string.Format($"{{0,{-transaction_width}}} ", TRANSACTION_LABEL);
            res += string.Format($"{{0,{-amount_width}}} ", AMOUNT_LABEL);
            res += string.Format($"{{0,{-token_width}}} ", TOKEN_LABEL);
            res += string.Format($"{{0,{-balance_width}}} ", BALANCE_LABEL);
            res += string.Format($"{{0,{-fee_width}}} ", FEE_LABEL);
            res += string.Format($"{{0,{-hash_width}}} ", HASH_LABEL);
            res += $"\n";


            if (start_height > 1)
                block = blocks[start_height - 1];
            else
                block = null;

            for (long i = start_height; i <= end_height; i++)
            {
                string index = string.Format($"{{0,{index_width}}}", i);
                res += $"{index} ";

                previous_block = block;
                if (!blocks.TryGetValue(i, out block))
                {
                    res += "Could not get the block";
                    res += $"\n";
                    continue;
                }

                res += $"{block.TimeStamp.ToString("MM'/'dd'/'yyyy' 'HH':'mm")} ";

                res += string.Format($"{{0,{-transaction_width}}} ", GetTransactionName(block)); 

                var transaction = block.GetTransaction(previous_block);
                //string amount = string.Format("{0,14:##########0.00}", 0m);
                //if (transaction != null)
                //    amount = string.Format("{0,14:##########0.00}", transaction.Amount);

                //string amount = String.Format($"{{0,{amount_width}}}", 0m);
                //if (transaction != null)
                //    amount = string.Format($"{{0,{amount_width}}}", transaction.Amount);
                //res += $"{amount} ";

                string amount = String.Format("{0:n}", 0m);
                if (transaction != null)
                    amount = string.Format("{0:n}", transaction.Amount);
                res += string.Format($"{{0,{amount_width}}} ", amount);

                string token = String.Format($"{{0,{token_width}}}", string.Empty);
                if (transaction != null)
                    token = String.Format($"{{0,{-token_width}}}", transaction.TokenCode);
                res += $"{token} ";

                //string balance = string.Format($"{{0,{balance_width}}}", 0m);
                //if (transaction != null && block.Balances.Count > 0)
                //    balance = string.Format($"{{0,{balance_width}}}", block.Balances[transaction.TokenCode].ToBalanceDecimal());
                //res += $"{balance} ";

                //string balance = string.Format("{0,14:##########0.00}", 0m);
                //if (transaction != null && block.Balances.Count > 0)
                //    balance = string.Format("{0,14:##########0.00}", block.Balances[transaction.TokenCode].ToBalanceDecimal());
                //res += $"{balance} ";

                string balance = String.Format("{0:n}", 0m);
                if (transaction != null && block.Balances.Count > 0)
                    balance = string.Format("{0:n}", block.Balances[transaction.TokenCode].ToBalanceDecimal());
                res += string.Format($"{{0,{balance_width}}} ", balance);

                //string fee = string.Format($"{{0,{fee_width}}}", 0m);
                //if (transaction != null)
                //    fee = string.Format($"{{0,{fee_width}}}", transaction.FeeAmount);
                //res += $"{fee} ";

                //string fee = string.Format("{0,8:####0.00}", 0m);
                //if (transaction != null)
                //    fee = string.Format("{0,8:####0.00}", transaction.FeeAmount);
                //res += $"{fee} ";

                string fee = String.Format("{0:n}", 0m);
                if (transaction != null)
                    fee = String.Format("{0:n}", block.Fee);
                res += string.Format($"{{0,{fee_width}}} ", fee);

                res += $"{block.Hash}";

                res += $"\n";

            }
            return res;
        }

        private string GetTransactionName(TransactionBlock block)
        {
            string res = string.Empty;
            switch (block.BlockType)
            {
                case BlockTypes.OpenAccountWithReceiveTransfer:
                case BlockTypes.OpenAccountWithReceiveFee:
                case BlockTypes.ReceiveTransfer:
                    res += $"+ Received";
                    break;
                case BlockTypes.OpenAccountWithImport:
                case BlockTypes.ImportAccount:
                    res += $"+ Import";
                    break;
                case BlockTypes.LyraTokenGenesis:
                case BlockTypes.TokenGenesis:
                    res += $"+ Genesis";
                    break;
                case BlockTypes.ReceiveAuthorizerFee:
                case BlockTypes.ReceiveMultipleFee:
                    res += $"+ Auth Fees";
                    break;
                case BlockTypes.SendTransfer:
                    res += $"- Sent";
                    break;
                case BlockTypes.ExecuteTradeOrder:
                    res += $"- Exec Trade";
                    break;
                case BlockTypes.TradeOrder:
                    res += $"- Trade Order";
                    break;
                case BlockTypes.Trade:
                    res += $"- Trade";
                    break;
                default:
                    break;
            }
            return res;
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
