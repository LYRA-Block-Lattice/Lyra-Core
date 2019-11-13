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

        HttpClient _client;
        HubConnection _exchangeHub;
        ExchangeViewPage _thePage;
        public ExchangeViewModel(ExchangeViewPage page)
        {
            _thePage = page;
            _exchangeHub = new HubConnectionBuilder()
                .WithUrl("http://lex.lyratokens.com:5493/ExchangeHub", HttpTransportType.WebSockets | HttpTransportType.LongPolling)
                .AddJsonProtocol(options => {
                    options.PayloadSerializerOptions.PropertyNamingPolicy = null;
                })
                .Build();

            _exchangeHub.On<string>("SellOrders", (ordersJson) =>
            {
                SellOrders.Clear();
                var orders = JsonConvert.DeserializeObject<List<KeyValuePair<Decimal, Decimal>>>(ordersJson);
                if(orders.Count < 10)
                {
                    for(int i = 0; i < 10 - orders.Count; i++)
                    {
                        SellOrders.Add(new KeyValuePair<String, String>("", ""));   // force alight to bottom
                    }
                }
                foreach (var order in orders)
                {
                    SellOrders.Add(new KeyValuePair<String, String>(order.Key.ToString(), order.Value.ToString()));
                }
                if(orders.Count > 0)
                {
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        _thePage.Scroll(SellOrders.Last());
                    });
                }
            });

            _exchangeHub.On<string>("BuyOrders", (ordersJson) =>
            {
                BuyOrders.Clear();
                var orders = JsonConvert.DeserializeObject<List<KeyValuePair<Decimal, Decimal>>>(ordersJson);
                foreach (var order in orders)
                {
                    BuyOrders.Add(new KeyValuePair<String, String>(order.Key.ToString(), order.Value.ToString()));
                }
                if (orders.Count < 10)
                {
                    for (int i = 0; i < 10 - orders.Count; i++)
                    {
                        BuyOrders.Add(new KeyValuePair<String, String>("", ""));   // force align to bottom
                    }
                }
            });

            _exchangeHub.On<string>("UserOrder", (orderJson) =>
            {
                var key = JsonConvert.DeserializeObject<CancelKey>(orderJson);
                MyOrders.Add(new Tuple<string, string, string, string, string>(key.Order?.BuySellType.ToString(),
                    key?.Order.TokenName, key.Order?.Price.ToString(), key.Order?.Amount.ToString(), key.State.ToString()));
            });

            Task.Run(async () => await Connect() );

            _client = new HttpClient
            {
                //BaseAddress = new Uri("https://localhost:5001/api/")
                BaseAddress = new Uri("http://lex.lyratokens.com:5493/api/"),
#if DEBUG
                Timeout = new TimeSpan(0, 30, 0)        // for debug. but 10 sec is too short for real env
#else
                Timeout = new TimeSpan(0, 0, 30)
#endif
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

        async Task Connect()
        {
            try
            {
                if(_exchangeHub.State == HubConnectionState.Disconnected)
                    await _exchangeHub.StartAsync();
            }
            catch (Exception ex)
            {
                // Something has gone wrong
            }
        }

        public async Task FetchOrders(string tokenName)
        {
            try
            {
                await Connect();
                await _exchangeHub.InvokeAsync("FetchOrders", tokenName);
            }
            catch (Exception ex)
            {
                // send failed
            }
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
                var reqStr = JsonConvert.SerializeObject(order);
                try
                {
                    await _exchangeHub.SendAsync("SendOrder", reqStr);
                }
                catch(Exception ex)
                {
                    await Connect();
                    await _exchangeHub.SendAsync("SendOrder", reqStr);
                }
                
            }
            catch (Exception)
            {

            }
        }
        private void UpdateHoldings()
        {
            if(App.Container.Balances == null)
            {
                LeXBalance = $"Hold Lyra.LeX: 0";
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
