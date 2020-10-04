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

        public TransferViewModel()
        {
            IsWorking = false;
            TokenNames = App.Store.State.wallet.GetLatestBlock()?.Balances?.Keys.ToList();

            MessagingCenter.Subscribe<BalanceViewModel>(
                this, MessengerKeys.BalanceRefreshed, (sender) =>
                {
                    TokenNames = App.Store.State.wallet.GetLatestBlock()?.Balances?.Keys.ToList();
                });

            TransferCommand = new Command(async () =>
            {
                await Shell.Current.GoToAsync($"..?action=transfer&token={SelectedTokenName}&account={TargetAccount}&amount={Amount}");
            });
            ScanCommand = new Command(async () =>
            {
                ZXingScannerPage scanPage = new ZXingScannerPage();
                Shell.SetTabBarIsVisible(scanPage, false);
                scanPage.OnScanResult += (result) =>
                {
                    scanPage.IsScanning = false;
                    ZXing.BarcodeFormat barcodeFormat = result.BarcodeFormat;
                    string type = barcodeFormat.ToString();
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        await Shell.Current.GoToAsync("..");
                        try
                        {
                            var lyraUri = new LyraUri(result.Text);
                            if (lyraUri.Method.Equals("/payme"))
                            {
                                TargetAccount = lyraUri.AccountID;
                            }
                        }
                        catch (Exception ex)
                        {

                        }
                    });
                };
                await Shell.Current.Navigation.PushAsync(scanPage);
            });
        }
    }
}
