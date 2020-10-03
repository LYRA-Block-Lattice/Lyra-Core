using Acr.UserDialogs;
using Lyra.Core.API;
using LyraWallet.Models;
using LyraWallet.Services;
using LyraWallet.States;
using LyraWallet.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using ZXing.Net.Mobile.Forms;

namespace LyraWallet.Views
{
    [QueryProperty("Action", "action")]
    [QueryProperty("Network", "network")]
    [QueryProperty("PrivateKey", "key")]
    [QueryProperty("Refresh", "refresh")]
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class BalancePage : ContentPage
    {
        private string _action;
        private string _network;
        private string _key;

        public string Refresh
        {
            set
            {
                if (value == "yes")
                {
                    App.Store.Dispatch(new WalletRefreshBalanceAction
                    {
                        wallet = App.Store.State.wallet
                    });
                }
            }
        }
        public string Action
        {
            set
            {
                _action = value;
            }
        }

        public string Network
        {
            set
            {
                _network = value;
            }
        }

        public string PrivateKey
        {
            set
            {
                _key = value;
            }
        }
        public BalancePage()
        {
            InitializeComponent();

            //if (DeviceInfo.Platform == DevicePlatform.UWP)
            //    btnRefresh.IsVisible = true;
            //else
            //    btnRefresh.IsVisible = false;

            lvBalance.ItemTapped += LvBalance_ItemTapped;

            // redux
            App.Store.Select(state => state.Balances)
                .Subscribe(w =>
                {
                    BalanceViewModel vm = BindingContext as BalanceViewModel;
                    vm.Balances = w;
                    vm.IsRefreshing = false;

                    if (w != null && w.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
                        vm.CanPay = true;
                    else
                        vm.CanPay = false;

                    UserDialogs.Instance.HideLoading();
                });

            App.Store.Select(state => state.NonFungible)
                .Subscribe(nf =>
                {
                    if (nf != null)
                    {
                        App.Store.Dispatch(new WalletNonFungibleTokenAction
                        {
                            wallet = App.Store.State.wallet,
                            nfToken = nf
                        });
                    }
                });

            App.Store.Select(state => state.ErrorMessage)
                .Subscribe(errMsg =>
                {
                    if (errMsg == null)
                        return;

                    // display error message here
                    // var icon = await BitmapLoader.Current.LoadFromResource("emoji_cool_small.png", null, null);

                    ToastConfig.DefaultBackgroundColor = System.Drawing.Color.AliceBlue;
                    ToastConfig.DefaultMessageTextColor = System.Drawing.Color.Red;
                    ToastConfig.DefaultActionTextColor = System.Drawing.Color.DarkRed;
                    //var bgColor = FromHex(this.BackgroundColor);
                    //var msgColor = FromHex(this.MessageTextColor);
                    //var actionColor = FromHex(this.ActionTextColor);

                    UserDialogs.Instance.Toast(new ToastConfig(errMsg)
                        //.SetBackgroundColor(bgColor)
                        //.SetMessageTextColor(msgColor)
                        .SetDuration(TimeSpan.FromSeconds(5))
                        .SetPosition(false ? ToastPosition.Top : ToastPosition.Bottom)
                        //.SetIcon(icon)
                        .SetAction(x => x
                            .SetText("Close")
                            .SetTextColor(Color.Gray)
                            .SetAction(() => { }/* UserDialogs.Instance.Alert("You clicked the primary toast button")*/)
                        )
                    );
                });
        }

        private async void LvBalance_ItemTapped(object sender, ItemTappedEventArgs e)
        {
            if (e.Item != null)
            {
                var kvp = (KeyValuePair<string, decimal>)e.Item;
                await Shell.Current.GoToAsync($"TransferPage?token={kvp.Key}");
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            string txt = null;

            // check init load or re-open
            // if init load, goto create/restore
            // if re-open, refresh balance

            if (_action != null)
            {
                if(_action == "create")
                {
                    App.Store.Dispatch(new WalletCreateAction
                    {
                        network = _network,
                        name = "default",
                        password = "",
                        path = DependencyService.Get<IPlatformSvc>().GetStoragePath()
                    });

                    txt = "Creating new wallet...";
                }
                else if(_action == "restore")
                {
                    App.Store.Dispatch(new WalletRestoreAction
                    {
                        privateKey = _key,
                        network = _network,
                        name = "default",
                        password = "",
                        path = DependencyService.Get<IPlatformSvc>().GetStoragePath()
                    });
                    txt = "Restoring wallet and syncing...";
                }
                else
                {
                    // 
                }
            }
            else
            {
                if (App.Store.State.IsOpening)
                {
                    // refresh balance
                }
                else
                {
                    // check default wallet exists. 
                    var path = DependencyService.Get<IPlatformSvc>().GetStoragePath();
                    var fn = $"{path}/default.lyrawallet";
                    if (File.Exists(fn))
                    {
                        App.Store.Dispatch(new WalletOpenAction
                        {
                            path = path,
                            name = "default",
                            password = ""
                        });

                        txt = "Opening wallet and syncing...";
                    }
                    else
                    {
                        await Shell.Current.GoToAsync("NetworkSelectionPage");
                    }
                }
            }

            if (txt != null)
                UserDialogs.Instance.ShowLoading(txt);
        }

        private async void Import_ClickedAsync(object sender, EventArgs e)
        {
            string result = await DisplayPromptAsync("Import Account", "What's your private key to import?");
            if (!string.IsNullOrWhiteSpace(result))
            {
                App.Store.Dispatch(new WalletImportAction
                {
                    wallet = App.Store.State.wallet,
                    targetPrivateKey = result
                });
            }
        }

        private async void Redeem_Clicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("RedeemPage");
        }

        private async void ScanToPay_Clicked(object sender, EventArgs e)
        {
            ZXingScannerPage scanPage = new ZXingScannerPage();
            scanPage.OnScanResult += (result) =>
            {
                scanPage.IsScanning = false;
                ZXing.BarcodeFormat barcodeFormat = result.BarcodeFormat;
                string type = barcodeFormat.ToString();
                Device.BeginInvokeOnMainThread(async () =>
                {
                    await Navigation.PopAsync();
                        // pay to result.Text
                        try
                    {
                        if (string.IsNullOrWhiteSpace(result.Text))
                            throw new Exception("Scan QR Code failed.");

                        var lyraUri = new LyraUri(result.Text);

                        var msg = "";
                        if (lyraUri.Method.Equals("/cart/checkout"))
                        {
                            Decimal total = 0;
                                // check parameters is exists
                                if (string.IsNullOrWhiteSpace(lyraUri.Token)
                                || string.IsNullOrWhiteSpace(lyraUri.AccountID)
                                || string.IsNullOrWhiteSpace(lyraUri.Total)
                                || !Decimal.TryParse(lyraUri.Total, out total))
                                throw new Exception("Unknown QR Code: " + result.Text);

                            msg = $"Store {lyraUri.Shop} Checkout, Total to Pay:\n";
                            msg += $"{total} {lyraUri.Token}";
                            var isOK = await DisplayAlert("Alert", msg, "Confirm", "Canel");
                            if (isOK)
                            {
                                var sta = new WalletSendTokenAction
                                {
                                    DstAddr = lyraUri.AccountID,
                                    Amount = total,
                                    TokenName = lyraUri.Token,
                                    wallet = App.Store.State.wallet
                                };
                                App.Store.Dispatch(sta);
                            }
                            return;
                        }
                        else if (lyraUri.PathAndQuery.StartsWith("/payme"))
                        {
                            await Shell.Current.GoToAsync($"TransferPage?token={LyraGlobal.OFFICIALTICKERCODE}&account={lyraUri.AccountID}");
                            return;
                        }
                        else
                        {
                            await DisplayAlert("Alert", "Unknown QR Code", "OK");
                        }
                    }
                    catch (Exception ex)
                    {
                        await DisplayAlert("Alert", $"Unable to pay: {ex.Message}\n\nQR Code:\n{result.Text}", "OK");
                    }
                });
            };
            await Navigation.PushAsync(scanPage);
        }
    }
}