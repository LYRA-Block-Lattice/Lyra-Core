using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Cryptography;
using Lyra.Exchange;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lyra.Core.Utils;

namespace Lyra.Core.Exchange
{
    public class DealEngine
    {
        private IMongoCollection<ExchangeAccount> _exchangeAccounts;
        private IMongoCollection<ExchangeOrder> _queue;
        private IMongoCollection<ExchangeOrder> _finished;

        private LyraConfig _config;

        public event EventHandler OnNewOrder;

        public DealEngine(
            IMongoCollection<ExchangeAccount> exchangeAccounts,
            IMongoCollection<ExchangeOrder> queue,
            IMongoCollection<ExchangeOrder> finished
            )
        {
            _config = Neo.Settings.Default.LyraNode;
            _exchangeAccounts = exchangeAccounts;
            _queue = queue;
            _finished = finished;
        }
        public async Task<ExchangeAccount> GetExchangeAccount(string accountID, bool refreshBalance = false)
        {
            var findResult = await _exchangeAccounts.FindAsync(a => a.AssociatedToAccountId == accountID);
            var acct = await findResult.FirstOrDefaultAsync();
            if (!refreshBalance)
                return acct;

            if (acct != null)
            {
                // create wallet and update balance
                var memStor = new AccountInMemoryStorage();
                var acctWallet = new ExchangeAccountWallet(memStor, _config.Lyra.NetworkId);
                acctWallet.AccountName = "tmpAcct";
                await acctWallet.RestoreAccountAsync("", acct.PrivateKey);
                acctWallet.OpenAccount("", acctWallet.AccountName);
                for (int i = 0; i < 300; i++)
                {
                    var result = await acctWallet.Sync(null);
                    if (result == APIResultCodes.Success)
                        break;
                }

                {
                    var transb = acctWallet.GetLatestBlock();
                    if (transb != null)
                    {
                        if (acct.Balance == null)
                            acct.Balance = new Dictionary<string, decimal>();
                        else
                            acct.Balance.Clear();
                        foreach (var b in transb.Balances)
                        {
                            acct.Balance.Add(b.Key, b.Value.ToBalanceDecimal());
                        }
                        _exchangeAccounts.ReplaceOne(a => a.AssociatedToAccountId == accountID, acct);
                    }
                }
            }
            return acct;
        }
        public async Task<decimal> GetExchangeAccountBalance(string accountID, string tokenName)
        {
            var findResult = await _exchangeAccounts.FindAsync(a => a.AssociatedToAccountId == accountID);
            var acct = await findResult.FirstOrDefaultAsync();
            if (acct == null)
                return 0;
            if (!acct.Balance.ContainsKey(tokenName))
                return 0;
            return acct.Balance[tokenName];
        }

        public async Task<ExchangeAccount> AddExchangeAccount(string assocaitedAccountId)
        {
            var findResult = await _exchangeAccounts.FindAsync(a => a.AssociatedToAccountId == assocaitedAccountId);
            var findAccount = await findResult.FirstOrDefaultAsync();
            if (findAccount != null)
            {
                return findAccount;
            }

            var walletPrivateKey = Signatures.GenerateWallet().privateKey;
            var walletAccountId = Signatures.GetAccountIdFromPrivateKey(walletPrivateKey);

            var account = new ExchangeAccount()
            {
                AssociatedToAccountId = assocaitedAccountId,
                AccountId = walletAccountId,
                PrivateKey = walletPrivateKey
            };
            await _exchangeAccounts.InsertOneAsync(account);
            return account;
        }

        public async Task<CancelKey> AddOrderAsync(ExchangeAccount acct, TokenTradeOrder order)
        {
            order.CreatedTime = DateTime.Now;
            var item = new ExchangeOrder()
            {
                ExchangeAccountId = acct.Id,
                Order = order,
                CanDeal = true,
                State = DealState.Placed,
                ClientIP = null
            };
            await _queue.InsertOneAsync(item);

            OnNewOrder?.Invoke(this, new EventArgs());

            var key = new CancelKey()
            {
                State = OrderState.Placed,
                Key = item.Id.ToString(),
                Order = order
            };
            return key;
        }

        public async Task RemoveOrderAsync(string key)
        {
            var finds = await _queue.FindAsync(a => a.Id == key);
            var order = await finds.FirstOrDefaultAsync();

            if (order != null)
            {
                await _queue.DeleteOneAsync(a => a.Id == order.Id);
                await SendMarket(order.Order.TokenName);
                await ExchangeAccountLiquidation(order.Order.AccountID);
            }
        }

        public async Task<bool> MakeDealAsync()
        {
            // has new order. do trade
            var changedTokens = new List<string>();
            var changedAccount = new List<string>();

            var placed = await GetNewlyPlacedOrdersAsync();
            for (int i = 0; i < placed.Length; i++)
            {
                var curOrder = placed[i];

                if (!changedTokens.Contains(curOrder.Order.TokenName))
                    changedTokens.Add(curOrder.Order.TokenName);

                (bool IsSuccess, decimal balance) mtrans, ctrans;

                var matchedOrders = await LookforExecution(curOrder);
                if (matchedOrders.Count() > 0)
                {
                    foreach (var matchedOrder in matchedOrders)
                    {
                        var tradedAmount = Math.Min(matchedOrder.Order.Amount, curOrder.Order.Amount);

                        // lets sync exchange wallet first to prevent any error from happening
                        //Wallet mwallet, cwallet;
                        //try
                        //{

                        //}

                        // taker profit first
                        if (curOrder.Order.BuySellType == OrderType.Buy)
                        {
                            var tradedPrice = Math.Min(matchedOrder.Order.Price, curOrder.Order.Price);
                            var lyraAmount = tradedAmount * tradedPrice;
                            mtrans = await SendFromExchangeAccountToAnotherAsync(matchedOrder.ExchangeAccountId,
                                curOrder.ExchangeAccountId, matchedOrder.Order.TokenName,
                                tradedAmount);
                            ctrans = await SendFromExchangeAccountToAnotherAsync(curOrder.ExchangeAccountId,
                                matchedOrder.ExchangeAccountId, LyraGlobal.OFFICIALTICKERCODE,
                                lyraAmount);

                            //// transfer back the result to user wallet
                            //var tb1 = await SendFromExchangeAccountBackToUserAsync(curOrder.ExchangeAccountId, matchedOrder.Order.TokenName, tradedAmount);
                            //var tb2 = await SendFromExchangeAccountBackToUserAsync(matchedOrder.ExchangeAccountId, LyraGlobal.LYRA_TICKER_CODE, lyraAmount);
                            //Trace.Assert(tb1.IsSuccess && tb2.IsSuccess);
                        }
                        else   // currentOrder sell
                        {
                            var tradedPrice = Math.Max(matchedOrder.Order.Price, curOrder.Order.Price);
                            var lyraAmount = tradedAmount * tradedPrice;
                            mtrans = await SendFromExchangeAccountToAnotherAsync(matchedOrder.ExchangeAccountId,
                                curOrder.ExchangeAccountId, LyraGlobal.OFFICIALTICKERCODE,
                                lyraAmount);
                            ctrans = await SendFromExchangeAccountToAnotherAsync(curOrder.ExchangeAccountId,
                                matchedOrder.ExchangeAccountId, matchedOrder.Order.TokenName,
                                tradedAmount);

                            //// transfer back to user
                            //var tb1 = await SendFromExchangeAccountBackToUserAsync(curOrder.ExchangeAccountId, LyraGlobal.LYRA_TICKER_CODE, lyraAmount);
                            //var tb2 = await SendFromExchangeAccountBackToUserAsync(matchedOrder.ExchangeAccountId, matchedOrder.Order.TokenName, tradedAmount);
                            //Trace.Assert(tb1.IsSuccess && tb2.IsSuccess);
                        }

                        if (!mtrans.IsSuccess || !ctrans.IsSuccess)
                        {
                            throw new Exception("Exchange Deal Engin Fatal Error");
                        }
                        // three conditions
                        if (matchedOrder.Order.Amount < curOrder.Order.Amount)
                        {
                            //matched -> archive, cur -> partial
                            matchedOrder.State = DealState.Executed;
                            await _queue.DeleteOneAsync(Builders<ExchangeOrder>.Filter.Eq(o => o.Id, matchedOrder.Id));
                            await _finished.InsertOneAsync(matchedOrder);

                            curOrder.State = DealState.PartialExecuted;
                            curOrder.Order.Amount -= matchedOrder.Order.Amount;

                            if (!changedAccount.Contains(matchedOrder.Order.AccountID))
                                changedAccount.Add(matchedOrder.Order.AccountID);
                            if (!changedAccount.Contains(curOrder.Order.AccountID))
                                changedAccount.Add(curOrder.Order.AccountID);

                            continue;
                        }
                        else if (matchedOrder.Order.Amount == curOrder.Order.Amount)
                        {
                            // matched -> archive, cur -> archive
                            matchedOrder.State = DealState.Executed;
                            await _queue.DeleteOneAsync(Builders<ExchangeOrder>.Filter.Eq(o => o.Id, matchedOrder.Id));
                            await _finished.InsertOneAsync(matchedOrder);

                            curOrder.State = DealState.Executed;
                            await _queue.DeleteOneAsync(Builders<ExchangeOrder>.Filter.Eq(o => o.Id, curOrder.Id));
                            await _finished.InsertOneAsync(curOrder);

                            if (!changedAccount.Contains(matchedOrder.Order.AccountID))
                                changedAccount.Add(matchedOrder.Order.AccountID);
                            if (!changedAccount.Contains(curOrder.Order.AccountID))
                                changedAccount.Add(curOrder.Order.AccountID);

                            break;
                        }
                        else // matchedOrder.Order.Amount > curOrder.Order.Amount
                        {
                            // matched -> partial, cur -> archive
                            matchedOrder.State = DealState.PartialExecuted;
                            matchedOrder.Order.Amount -= curOrder.Order.Amount;
                            await _queue.ReplaceOneAsync(Builders<ExchangeOrder>.Filter.Eq(o => o.Id, matchedOrder.Id), matchedOrder);

                            curOrder.State = DealState.Executed;
                            await _queue.DeleteOneAsync(Builders<ExchangeOrder>.Filter.Eq(o => o.Id, curOrder.Id));
                            await _finished.InsertOneAsync(curOrder);

                            if (!changedAccount.Contains(matchedOrder.Order.AccountID))
                                changedAccount.Add(matchedOrder.Order.AccountID);
                            if (!changedAccount.Contains(curOrder.Order.AccountID))
                                changedAccount.Add(curOrder.Order.AccountID);

                            break;
                        }
                    }
                }

                // all matched. update database                        
                // change state from placed to queued, update amount also.
                var update = Builders<ExchangeOrder>.Update.Set(o => o.Order.Amount, curOrder.Order.Amount);
                if (curOrder.State == DealState.Placed)
                    update = update.Set(s => s.State, DealState.Queued);
                if (curOrder.State == DealState.Placed || curOrder.State == DealState.PartialExecuted)
                    await _queue.UpdateOneAsync(Builders<ExchangeOrder>.Filter.Eq(o => o.Id, curOrder.Id), update);
            }
            foreach (var tokenName in changedTokens)
            {
                // the update the client
                await SendMarket(tokenName);
            }
            foreach (var account in changedAccount)
            {
                // client must refresh by itself
                //NotifyService.Notify(account, Core.API.NotifySource.Dex, "Deal", "", "");
                await ExchangeAccountLiquidation(account);
            }
            return true;
        }

        private async Task ExchangeAccountLiquidation(string associatedAccountId)
        {
            var ordersFind = await _queue.FindAsync(a => a.Order.AccountID == associatedAccountId);
            if (await ordersFind.AnyAsync())
                return;

            var fromResult = await _exchangeAccounts.FindAsync(a => a.AssociatedToAccountId == associatedAccountId);
            var fromAcct = await fromResult.FirstOrDefaultAsync();
            if (fromAcct != null)
            {
                var fromWallet = await GetExchangeAccountWallet(fromAcct.PrivateKey);

                {
                    var transb = fromWallet.GetLatestBlock();
                    if (transb != null)
                    {
                        int sendCount = 0;
                        foreach (var kvp in transb.Balances)
                        {
                            if (kvp.Value > 0 && kvp.Key != LyraGlobal.OFFICIALTICKERCODE)
                            {
                                var ret = await fromWallet.Send(kvp.Value.ToBalanceDecimal(), associatedAccountId, kvp.Key, true);
                                Trace.Assert(ret.ResultCode == APIResultCodes.Success);
                                sendCount++;
                            }
                        }

                        sendCount++;

                        if (transb.Balances[LyraGlobal.OFFICIALTICKERCODE] - (sendCount * ExchangingBlock.FEE).ToBalanceLong() > 0)
                        {
                            var ret2 = await fromWallet.Send(transb.Balances[LyraGlobal.OFFICIALTICKERCODE] - (sendCount * ExchangingBlock.FEE).ToBalanceLong(), associatedAccountId, LyraGlobal.OFFICIALTICKERCODE, true);
                            Trace.Assert(ret2.ResultCode == APIResultCodes.Success);
                        }
                    }
                }
            }
        }

        private async Task<(bool IsSuccess, decimal balance)> SendFromExchangeAccountToAnotherAsync(string fromId, string toId, string tokenName, decimal amount)
        {
            var fromResult = await _exchangeAccounts.FindAsync(a => a.Id == fromId);
            var fromAcct = await fromResult.FirstOrDefaultAsync();

            var toResult = await _exchangeAccounts.FindAsync(a => a.Id == toId);
            var toAcct = await toResult.FirstOrDefaultAsync();

            var fromWallet = await GetExchangeAccountWallet(fromAcct.PrivateKey);

            var transb = fromWallet.GetLatestBlock();
            if (transb != null && transb.Balances[tokenName].ToBalanceDecimal() >= amount)
            {
                var bLast = transb.Balances[tokenName].ToBalanceDecimal() - amount;
                var ret = await fromWallet.Send(amount, toAcct.AccountId, tokenName, true);
                return (ret.ResultCode == APIResultCodes.Success, bLast);
            }
            else
            {
                return (false, 0);
            }
        }

        private async Task<(bool IsSuccess, decimal balance)> SendFromExchangeAccountBackToUserAsync(string fromId, string tokenName, decimal amount)
        {
            var fromResult = await _exchangeAccounts.FindAsync(a => a.Id == fromId);
            var fromAcct = await fromResult.FirstOrDefaultAsync();

            var fromWallet = await GetExchangeAccountWallet(fromAcct.PrivateKey);
            var transb = fromWallet.GetLatestBlock();
            if (transb != null && transb.Balances[tokenName].ToBalanceDecimal() >= amount)
            {
                var bLast = transb.Balances[tokenName].ToBalanceDecimal() - amount;
                var ret = await fromWallet.Send(amount, fromAcct.AssociatedToAccountId, tokenName, true);
                return (ret.ResultCode == APIResultCodes.Success, bLast);
            }
            else
            {
                return (false, 0);
            }
        }

        private async Task<Wallet> GetExchangeAccountWallet(string privateKey)
        {
            // create wallet and update balance
            var memStor = new AccountInMemoryStorage();

            var fromWallet = new Wallet(memStor, _config.Lyra.NetworkId);
            fromWallet.AccountName = "tmpAcct";
            await fromWallet.RestoreAccountAsync("", privateKey);
            fromWallet.OpenAccount("", fromWallet.AccountName);
            APIResultCodes result = APIResultCodes.UnknownError;
            for (int i = 0; i < 300; i++)
            {
                result = await fromWallet.Sync(null);
                if (result == APIResultCodes.Success)
                    break;
            }
            Trace.Assert(result == APIResultCodes.Success);
            return fromWallet;
        }

        public async Task<ExchangeOrder[]> GetNewlyPlacedOrdersAsync()
        {
            var finds = await _queue.FindAsync(a => a.State == DealState.Placed);
            var fl = await finds.ToListAsync();
            return fl.OrderBy(a => a.Order.CreatedTime).ToArray();
        }

        public async Task<ExchangeOrder[]> GetQueuedOrdersAsync()
        {
            var finds = await _queue.FindAsync(a => a.CanDeal);
            var fl = await finds.ToListAsync();
            return fl.OrderBy(a => a.Order.CreatedTime).ToArray();
        }

        public async Task<IOrderedEnumerable<ExchangeOrder>> LookforExecution(ExchangeOrder order)
        {
            //var builder = Builders<ExchangeOrder>.Filter;
            //var filter = builder.Eq("Order.TokenName", order.Order.TokenName)
            //    & builder.Ne("State", DealState.Placed);

            //if (order.Order.BuySellType == OrderType.Buy)
            //{
            //    filter &= builder.Eq("Order.BuySellType", OrderType.Sell);
            //    filter &= builder.Lte("Order.Price", order.Order.Price);
            //}
            //else
            //{
            //    filter &= builder.Eq("Order.BuySellType", OrderType.Buy);
            //    filter &= builder.Gte("Order.Price", order.Order.Price);
            //}

            IAsyncCursor<ExchangeOrder> found;
            if (order.Order.BuySellType == OrderType.Buy)
            {
                found = await _queue.FindAsync(a => a.Order.TokenName == order.Order.TokenName
                                && a.State != DealState.Placed
                                && a.Order.BuySellType == OrderType.Sell
                                && a.Order.Price <= order.Order.Price);
            }
            else
            {
                found = await _queue.FindAsync(a => a.Order.TokenName == order.Order.TokenName
                && a.State != DealState.Placed
                && a.Order.BuySellType == OrderType.Buy
                && a.Order.Price >= order.Order.Price);
            }

            var matches0 = await found.ToListAsync();

            if (order.Order.BuySellType == OrderType.Buy)
            {
                var matches = matches0.OrderBy(a => a.Order.Price);
                return matches;
            }
            else
            {
                var matches = matches0.OrderByDescending(a => a.Order.Price);
                return matches;
            }
        }

        public async Task<List<ExchangeOrder>> GetActiveOrders(string tokenName)
        {
            return await _queue.Find(a => a.CanDeal && a.Order.TokenName == tokenName).ToListAsync();
        }

        public async Task SendMarket(string tokenName)
        {
            var excOrders = (await GetActiveOrders(tokenName)).OrderByDescending(a => a.Order.Price);
            var sellOrders = excOrders.Where(a => a.Order.BuySellType == OrderType.Sell)
                .GroupBy(a => a.Order.Price)
                .Select(a => new KeyValuePair<Decimal, Decimal>(a.Key, a.Sum(x => x.Order.Amount))).ToList();

            var buyOrders = excOrders.Where(a => a.Order.BuySellType == OrderType.Buy)
                .GroupBy(a => a.Order.Price)
                .Select(a => new KeyValuePair<Decimal, Decimal>(a.Key, a.Sum(x => x.Order.Amount))).ToList();

            var orders = new Dictionary<string, List<KeyValuePair<Decimal, Decimal>>>();
            orders.Add("SellOrders", sellOrders);
            orders.Add("BuyOrders", buyOrders);

            //NotifyService.Notify("", Core.API.NotifySource.Dex, "Orders", tokenName, JsonConvert.SerializeObject(orders));
        }

        public async Task<List<ExchangeOrder>> GetOrdersForAccount(string accountId)
        {
            return await _queue.Find(a => a.Order.AccountID == accountId).ToListAsync();
        }
    }
}
