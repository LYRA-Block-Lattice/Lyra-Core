using Lyra.Client.Lib;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Exchange;
using LyraWallet.Models;
using LyraWallet.Views;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Xamarin.Forms;

namespace LyraWallet.ViewModels
{
    public class ExchangeViewModel : BaseViewModel
    {
        private string _title;
        private List<string> _tokenList;
        public List<string> TokenList
        {
            get
            {
                return _tokenList;
            }
            set
            {
                var st = SelectedToken;
                SetProperty(ref _tokenList, value);
                SelectedToken = st;
            }
        }

        public ObservableCollection<KeyValuePair<String, String>> SellOrders { get; } = new ObservableCollection<KeyValuePair<String, String>>();
        public ObservableCollection<KeyValuePair<String, String>> BuyOrders { get; } = new ObservableCollection<KeyValuePair<String, String>>();
        // buy/sell token price amount state
        public ObservableCollection<Tuple<string, string, string, string, string, string>> MyOrders { get; } = new ObservableCollection<Tuple<string, string, string, string, string, string>>();

        public string FilterKeyword { get; set; }
        public string TargetTokenBalance { get => _targetTokenBalance; set => SetProperty(ref _targetTokenBalance, value); }
        public string LeXBalance { get => _lexBalance; set => SetProperty(ref _lexBalance, value); }
        public string SelectedToken { get => _selectedToken; set {
                if(_selectedToken != value)
                {
                    SetProperty(ref _selectedToken, value);
                    Task.Run(() => SwitchMarket(value));
                }
            }
        }

        private string _selectedToken;
        private string _targetTokenBalance;
        private string _lexBalance;

        private string _buyPrice;
        private string _buyAmount;
        private string _sellPrice;
        private string _sellAmount;

        public ICommand BuyCommand { get; }
        public ICommand SellCommand { get; }
        public ICommand TokenChangedCommand { get;  }
        public ICommand CancelOrderCommand { get; }
        public string BuyPrice { get => _buyPrice; set => SetProperty(ref _buyPrice, value); }
        public string BuyAmount { get => _buyAmount; set => SetProperty(ref _buyAmount, value); }
        public string SellPrice { get => _sellPrice; set => SetProperty(ref _sellPrice, value); }
        public string SellAmount { get => _sellAmount; set => SetProperty(ref _sellAmount, value); }
        public string Title1 { get => _title; set => SetProperty(ref _title, value); }

        public ExchangeViewPage ThePage;

        private string _exchangeAccountId;

        public ExchangeViewModel()
        {
            Title = "Lyra Exchange";

            App.Container.OnExchangeOrderChanged += async (act, catalog, extInfo) => {
                if (catalog != SelectedToken)  // only show current token's order
                    return;

                switch(act)
                {
                    case "Orders":
                        var orders = JsonConvert.DeserializeObject<Dictionary<string, List<KeyValuePair<Decimal, Decimal>>>>(extInfo);
                        var sellos = orders["SellOrders"];
                        var buyos = orders["BuyOrders"];
                        {
                            SellOrders.Clear();
                            if (sellos.Count < 10)
                            {
                                for (int i = 0; i < 10 - sellos.Count; i++)
                                {
                                    SellOrders.Add(new KeyValuePair<String, String>(" ", " "));   // force alight to bottom
                                }
                            }
                            foreach (var order in sellos)
                            {
                                SellOrders.Add(new KeyValuePair<String, String>(order.Key.ToString(), order.Value.ToString()));
                            }
                            if (sellos.Count > 0)
                            {
                                Device.BeginInvokeOnMainThread(() =>
                                {
                                    ThePage.Scroll(SellOrders.Last());
                                });
                            }
                        }
                        {
                            BuyOrders.Clear();
                            foreach (var order in buyos)
                            {
                                BuyOrders.Add(new KeyValuePair<String, String>(order.Key.ToString(), order.Value.ToString()));
                            }
                            if (buyos.Count < 10)
                            {
                                for (int i = 0; i < 10 - buyos.Count; i++)
                                {
                                    BuyOrders.Add(new KeyValuePair<String, String>(" ", " "));   // force align to bottom
                                }
                            }
                        }
                        break;
                    case "Deal":
                        // at lease some order is (partially) executed
                        await GetMyOrders();
                        await UpdateHoldings();
                        break;
                    default:
                        break;
                }
            };

            MessagingCenter.Subscribe<BalanceViewModel>(
                this, MessengerKeys.BalanceRefreshed, async (sender) =>
                {
                    if(TokenList == null || TokenList.Count == 0)
                        TokenList = await App.Container.GetTokens(FilterKeyword);
                    await UpdateHoldings();
                });

            BuyCommand = new Command(async () =>
            {
                await SubmitOrder(true);
            });

            SellCommand = new Command(async () =>
            {
                await SubmitOrder(false);
            });

            CancelOrderCommand = new Command<string>(async (key) =>
            {
                var oldTitle = Title;
                try
                {
                    Title = "Canceling order...";
                    await App.Container.CancelExchangeOrder(key);
                    await GetMyOrders();
                }
                catch(Exception ex)
                {
                    await ThePage.DisplayAlert("Error Canceling Order", $"Network: {App.Container.CurrentNetwork}\nError Message: {ex.Message}", "OK");
                }
                finally
                {
                    Title = oldTitle;
                }
            });
        }

        private async Task SwitchMarket(string token)
        {
            await UpdateHoldings();
            await GetMyOrders();
            await App.Container.RequestMarket(token);
        }

        private async Task GetMyOrders()
        {
            try
            {
                var myorders = await App.Container.GetOrdersForAccount(App.Container.AccountID);
                MyOrders.Clear();
                foreach (var key in myorders.Where(a => a.Order.TokenName == SelectedToken))
                {
                    MyOrders.Add(new Tuple<string, string, string, string, string, string>(key.Order?.BuySellType.ToString(),
                        key?.Order.TokenName, key.Order?.Price.ToString(), key.Order?.Amount.ToString(), key.State.ToString(), key.Id));
                }
            }
            catch(Exception ex)
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    ThePage.DisplayAlert("Error Getting Order", $"Network: {App.Container.CurrentNetwork}\nError Message: {ex.Message}", "OK");
                });
            }
        }
        private async Task SubmitOrder(bool IsBuy)
        {
            if (string.IsNullOrWhiteSpace(SelectedToken))
                return;

            try
            {
                Title = "Submiting order...";
                // first check exchange account
                if(_exchangeAccountId == null)
                {
                    _exchangeAccountId = await App.Container.GetExchangeAccountId();
                }

                if (_exchangeAccountId == null)
                {
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        ThePage.DisplayAlert("Error Submiting Order", $"Network: {App.Container.CurrentNetwork}\nError Message: Can't create exchange account.", "OK");
                    });
                    return;
                }

                TokenTradeOrder order = new TokenTradeOrder()
                {
                    CreatedTime = DateTime.Now,
                    AccountID = App.Container.AccountID,
                    NetworkID = App.Container.CurrentNetwork,
                    BuySellType = IsBuy ? OrderType.Buy : OrderType.Sell,
                    TokenName = SelectedToken,
                    Price = Decimal.Parse(IsBuy ? BuyPrice : SellPrice),
                    Amount = decimal.Parse(IsBuy ? BuyAmount : SellAmount)
                };

                Dictionary<string, decimal> fundsToTransfer = new Dictionary<string, decimal>();
                if (!IsBuy)
                {
                    fundsToTransfer.Add(order.TokenName, order.Amount);
                }
                var lyraTotal = IsBuy ? order.Price * order.Amount + 2 * ExchangingBlock.FEE : 2 * ExchangingBlock.FEE;     // fee is high
                fundsToTransfer.Add(LyraGlobal.LYRA_TICKER_CODE, lyraTotal);
                
                try
                {
                    foreach(var kvp in fundsToTransfer)
                    {
                        if(kvp.Value > 0)
                        {
                            await App.Container.Transfer(kvp.Key, _exchangeAccountId, kvp.Value, true);
                        }
                    }                        
                }
                catch(Exception ex)
                {
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        ThePage.DisplayAlert("Error Submiting Order", $"Network: {App.Container.CurrentNetwork}\nError Message: Can't transfer funds to exchange account: {ex.Message}", "OK");
                    });
                    return;
                }

                var signer = new SignaturesClient();
                await order.SignAsync(signer, App.Container.PrivateKey);
                var key = await App.Container.SubmitExchangeOrderAsync(order);
                if(key.State == OrderState.Placed)
                {
                    // this is fake. just make a illusion.
                    MyOrders.Add(new Tuple<string, string, string, string, string, string>(key.Order?.BuySellType.ToString(),
                            key?.Order.TokenName, key.Order?.Price.ToString(), key.Order?.Amount.ToString(), key.State.ToString(), key.Key));
                    await GetMyOrders();
                }
            }
            catch (Exception)
            {

            }
            finally
            {
                Title = "Exchange Ready.";
            }
        }
        private async Task UpdateHoldings()
        {
            if(App.Container.Balances == null)
            {
                LeXBalance = $"Lyra.LeX: 0";
                TargetTokenBalance = $"{SelectedToken}: 0";
            }
            else
            {
                var exchBalance = await App.Container.GetExchangeBalance();
                string exchLyraBstr = "0";
                if (exchBalance != null && exchBalance.ContainsKey(LyraGlobal.LYRA_TICKER_CODE))
                    exchLyraBstr = exchBalance[LyraGlobal.LYRA_TICKER_CODE].ToString();
                LeXBalance = $"Lyra.LeX: {App.Container.Balances["Lyra.LeX"]} ({exchLyraBstr})";
                if(SelectedToken == null)
                {
                    TargetTokenBalance = "";
                }
                else
                {
                    string exchBstr = "0";
                    if (exchBalance != null && exchBalance.ContainsKey(SelectedToken))
                        exchBstr = exchBalance[SelectedToken].ToString();

                    if (App.Container.Balances.ContainsKey(SelectedToken))
                    {
                        TargetTokenBalance = $"{SelectedToken}: {App.Container.Balances[SelectedToken]} ({exchBstr})";
                    }
                    else
                    {
                        TargetTokenBalance = $"{SelectedToken}: 0 ({exchBstr})";
                    }
                }
            }
        }
    }
}
