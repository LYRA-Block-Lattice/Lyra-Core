using LyraWallet.Models;
using LyraWallet.Services;
using LyraWallet.Views;
using System;
using System.Collections.Generic;
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

            MessagingCenter.Subscribe<BalanceViewModel>(
                this, MessengerKeys.BalanceRefreshed, (sender) =>
                {
                    TokenNames = App.Container.TokenList;
                });

            TransferCommand = new Command(async () =>
            {
                IsWorking = true;
                try
                {
                    var amount = decimal.Parse(Amount);

                    await App.Container.Transfer(SelectedTokenName, TargetAccount, amount);

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
                        TargetAccount = result.Text;
                    });
                };
                await _thePage.Navigation.PushAsync(scanPage);
            });
        }
    }
}
