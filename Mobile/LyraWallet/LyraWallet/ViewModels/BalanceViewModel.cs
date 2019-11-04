using System;
using System.Windows.Input;

using Xamarin.Forms;
using LyraWallet.Services;

using Lyra.Client.Lib;
using LyraWallet.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using LyraWallet.Views;
using System.Linq;
using System.IO;
using ZXing.Net.Mobile.Forms;
using Lyra.Core.Blocks.Transactions;

namespace LyraWallet.ViewModels
{
    public class BalanceViewModel : BaseViewModel
    {
        private Page _thePage;

        //private Dictionary<string, Decimal> _balances;
        public Dictionary<string, Decimal> Balances
        {
            get => App.Container.Balances;
            //get => _balances;
            //set {
            //    SetProperty(ref _balances, value);
            //    Device.BeginInvokeOnMainThread(() =>
            //    {
            //        if (_balances == null)
            //            _thePage.Title = "Wallet has no Token";
            //        else
            //            _thePage.Title = "Balance";
            //    });
            //}
        }

        private string _balanceTxt;
        public string BalanceText
        {
            get => _balanceTxt;
            set => SetProperty(ref _balanceTxt, value);
        }

        private bool _isRefreshing;
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set
            {
                SetProperty(ref _isRefreshing, value);
                Device.BeginInvokeOnMainThread(() =>
                {
                    if (_isRefreshing)
                        Title = "Updating Balances...";
                    else
                        Title = "Balance";
                });
                
            }
        }

        public BalanceViewModel(Page page)
        {
            _thePage = page;
            App.Container.PropertyChanged += (o, e) => OnPropertyChanged(e.PropertyName);

            Title = "Balance";
            BalanceText = "Lyra: 0";
            
            RefreshCommand = new Command(async () =>
            {
                await Refresh();
            });

            ScanCommand = new Command(async () => {
                ZXingScannerPage scanPage = new ZXingScannerPage();
                scanPage.OnScanResult += (result) =>
                {
                    scanPage.IsScanning = false;
                    ZXing.BarcodeFormat barcodeFormat = result.BarcodeFormat;
                    string type = barcodeFormat.ToString();
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        await _thePage.Navigation.PopAsync();
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
                                var isOK = await _thePage.DisplayAlert("Alert", msg, "Confirm", "Canel");
                                if (isOK)
                                {
                                    await App.Container.Transfer(lyraUri.Token, lyraUri.AccountID, total);
                                    await _thePage.DisplayAlert("Info", "Success!", "OK");
                                }
                                return;
                            }
                            else if (lyraUri.PathAndQuery.StartsWith("/payme"))
                            {
                                var transPage = new TransferPage(TokenGenesisBlock.LYRA_TICKER_CODE,
                                    lyraUri.AccountID);
                                await _thePage.Navigation.PushAsync(transPage);
                                return;
                            }
                            else
                            {
                                await _thePage.DisplayAlert("Alert", "Unknown QR Code", "OK");
                            }
                        }
                        catch(Exception ex)
                        {
                            await _thePage.DisplayAlert("Alert", $"Unable to pay: {ex.Message}\n\nQR Code:\n{result.Text}", "OK");
                        }
                    });
                };
                await _thePage.Navigation.PushAsync(scanPage);
            });

            Task.Run(async () =>
            {
                await App.Container.OpenWalletFile();
                await App.Container.GetBalance();
                MessagingCenter.Send(this, MessengerKeys.BalanceRefreshed);
                await Refresh();
            });
        }

        private async Task Refresh()
        {
            try
            {
                IsRefreshing = true;
                await App.Container.RefreshBalance();
                IsRefreshing = false;
                MessagingCenter.Send(this, MessengerKeys.BalanceRefreshed);
            }
            catch (Exception ex)
            {
                IsRefreshing = false;
                Device.BeginInvokeOnMainThread(() =>
                {
                    _thePage.DisplayAlert("Error Opening Wallet", $"Network: {App.Container.CurrentNetwork}\nError Message: {ex.Message}", "OK");
                });
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand TokenSelected { get; }
        public ICommand ScanCommand { get; }
    }
}