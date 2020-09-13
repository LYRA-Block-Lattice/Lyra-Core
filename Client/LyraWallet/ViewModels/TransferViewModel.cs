using LyraWallet.Models;
using LyraWallet.Services;
using LyraWallet.States;
using LyraWallet.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Xamarin.Forms;
using ZXing.Net.Mobile.Forms;

namespace LyraWallet.ViewModels
{
    public class TransferViewModel : BaseViewModel
    {
        private TransferPage _thePage;

        private List<string> _tokenName;
        public List<string> TokenNames
        {
            get => _tokenName;
            set => SetProperty(ref _tokenName, value);
        }

        private string _selectedTokenName;
        public string SelectedTokenName
        {
            get => _selectedTokenName;
            set => SetProperty(ref _selectedTokenName, value);
        }

        private string _targetAccount;
        public string TargetAccount
        {
            get => _targetAccount;
            set => SetProperty(ref _targetAccount, value);
        }

        private string _amount;
        public string Amount
        {
            get => _amount;
            set => SetProperty(ref _amount, value);
        }

        private bool isWorking;

        public ICommand TransferCommand { get; }
        public ICommand ScanCommand { get; }
        public bool IsWorking { get => isWorking; set => SetProperty(ref isWorking, value); }

        public TransferViewModel(Page page)
        {
            _thePage = (TransferPage) page;
            IsWorking = false;
            TokenNames = App.Store.State.wallet.GetLatestBlock().Balances?.Keys.ToList();

            MessagingCenter.Subscribe<BalanceViewModel>(
                this, MessengerKeys.BalanceRefreshed, (sender) =>
                {
                    TokenNames = App.Store.State.wallet.GetLatestBlock().Balances?.Keys.ToList();
                });

            TransferCommand = new Command(async () =>
            {
                IsWorking = true;
                try
                {
                    var amount = decimal.Parse(Amount);

                    var sta = new WalletSendTokenAction
                    {
                        DstAddr = TargetAccount,
                        Amount = amount,
                        TokenName = SelectedTokenName,
                        wallet = App.Store.State.wallet
                    };
                    App.Store.Dispatch(sta);

                    IsWorking = false;
                    await _thePage.DisplayAlert("Success", "Your transaction has been successfully completed.", "OK");
                }
                catch (Exception x)
                {
                    IsWorking = false;
                    await _thePage.DisplayAlert("Error", x.Message, "OK");
                }
            });
            ScanCommand = new Command(async () => {
                ZXingScannerPage scanPage = new ZXingScannerPage();
                scanPage.OnScanResult += (result) =>
                {
                    scanPage.IsScanning = false;
                    ZXing.BarcodeFormat barcodeFormat = result.BarcodeFormat;
                    string type = barcodeFormat.ToString();
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        _thePage.Navigation.PopAsync();
                        try
                        {
                            var lyraUri = new LyraUri(result.Text);
                            if (lyraUri.Method.Equals("/payme"))
                            {
                                TargetAccount = lyraUri.AccountID;
                            }
                        }               
                        catch(Exception ex)
                        {

                        }
                    });
                };
                await _thePage.Navigation.PushAsync(scanPage);
            });
        }
    }
}
