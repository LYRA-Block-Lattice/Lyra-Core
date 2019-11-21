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
        public ObservableCollection<Tuple<string, string, string, string, string>> MyOrders { get; } = new ObservableCollection<Tuple<string, string, string, string, string>>();

        public string FilterKeyword { get; set; }
        public string TargetTokenBalance { get => _targetTokenBalance; set => SetProperty(ref _targetTokenBalance, value); }
        public string LeXBalance { get => _lexBalance; set => SetProperty(ref _lexBalance, value); }
        public string SelectedToken { get => _selectedToken; set {
                SetProperty(ref _selectedToken, value);
                UpdateHoldings();
                Task.Run(async () => await App.Container.RequestMarket(value));            
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
        public string BuyPrice { get => _buyPrice; set => SetProperty(ref _buyPrice, value); }
        public string BuyAmount { get => _buyAmount; set => SetProperty(ref _buyAmount, value); }
        public string SellPrice { get => _sellPrice; set => SetProperty(ref _sellPrice, value); }
        public string SellAmount { get => _sellAmount; set => SetProperty(ref _sellAmount, value); }

        ExchangeViewPage _thePage;
        public ExchangeViewModel(ExchangeViewPage page)
        {
            _thePage = page;

            App.Container.OnExchangeOrderChanged += (act, catalog, extInfo) => {
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
                                    SellOrders.Add(new KeyValuePair<String, String>("", ""));   // force alight to bottom
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
                                    _thePage.Scroll(SellOrders.Last());
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
                                    BuyOrders.Add(new KeyValuePair<String, String>("", ""));   // force align to bottom
                                }
                            }
                        }
                        break;
                    case "UserOrder":
                        {
                            var key = JsonConvert.DeserializeObject<CancelKey>(extInfo);
                            MyOrders.Add(new Tuple<string, string, string, string, string>(key.Order?.BuySellType.ToString(),
                                key?.Order.TokenName, key.Order?.Price.ToString(), key.Order?.Amount.ToString(), key.State.ToString()));
                        }
                        break;
                    default:
                        break;
                }
            };

            MessagingCenter.Subscribe<BalanceViewModel>(
                this, MessengerKeys.BalanceRefreshed, async (sender) =>
                {
                    await Touch();
                    UpdateHoldings();
                });

            BuyCommand = new Command(async () =>
            {
                await SubmitOrder(true);
            });

            SellCommand = new Command(async () =>
            {
                await SubmitOrder(false);
            });

        }

        private async Task SubmitOrder(bool IsBuy)
        {
            try
            {
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
                order.Sign(App.Container.PrivateKey);
                var key = await App.Container.SubmitExchangeOrderAsync(order);
                MyOrders.Add(new Tuple<string, string, string, string, string>(key.Order?.BuySellType.ToString(),
                        key?.Order.TokenName, key.Order?.Price.ToString(), key.Order?.Amount.ToString(), key.State.ToString()));
            }
            catch (Exception)
            {

            }
        }
        private void UpdateHoldings()
        {
            if(App.Container.Balances == null)
            {
                LeXBalance = $"Holding Lyra.LeX: 0";
                TargetTokenBalance = $"Holding {SelectedToken}: 0";
            }
            else
            {
                LeXBalance = $"Holdding Lyra.LeX: {App.Container.Balances["Lyra.LeX"]}";
                if(SelectedToken == null)
                {
                    TargetTokenBalance = "";
                }
                else
                {
                    if (App.Container.Balances.ContainsKey(SelectedToken))
                    {
                        TargetTokenBalance = $"Holdding {SelectedToken}: {App.Container.Balances[SelectedToken]}";
                    }
                    else
                    {
                        TargetTokenBalance = $"Holdding {SelectedToken}: 0";
                    }
                }
            }
        }

        internal async Task Touch()
        {
            try
            {
                TokenList = await App.Container.GetTokens(FilterKeyword);
            }
            catch(Exception ex)
            {
                //TokenList = new List<string>() { ex.Message };
            }
        }
    }
}
