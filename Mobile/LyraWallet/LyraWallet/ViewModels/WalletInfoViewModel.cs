using LyraWallet.Services;
using LyraWallet.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using Xamarin.Forms;

namespace LyraWallet.ViewModels
{
	public class WalletInfoViewModel : BaseViewModel
    {
        private Page _thePage;
        public string AccountID
        {
            get => App.Container.AccountID;
        }
        public string CurrentNetwork
        {
            get => App.Container.CurrentNetwork;
        }

        public ICommand BarcodeGenCommand { get; }
        public ICommand CreateTokenCommand { get; }
        public ICommand RedeemCodeCommand { get; }
        public ICommand ShowBlocksCommand { get; }
        public ICommand RemoveAccountCommand { get; }
        public WalletInfoViewModel (Page page)
		{
            _thePage = page;

            App.Container.PropertyChanged += (o, e) => OnPropertyChanged(e.PropertyName);

            BarcodeGenCommand = new Command(async () =>
            {
                var nextPage = new BarcodeGenPage($"lyra://localhost/payme?AccountID={AccountID}", AccountID);
                await _thePage.Navigation.PushAsync(nextPage);
            });

            CreateTokenCommand = new Command(async () =>
            {
                var nextPage = new CreateTokenPage();
                await _thePage.Navigation.PushAsync(nextPage);
            });
            RedeemCodeCommand = new Command(() =>
            {

            });
            ShowBlocksCommand = new Command(async () =>
            {
                var nextPage = new BlockListPage();
                await _thePage.Navigation.PushAsync(nextPage);
            });
            RemoveAccountCommand = new Command(async () =>
            {
                bool answer = await _thePage.DisplayAlert("Are you sure?", "If you not backup private key properly, all Tokens will be lost after account removing. Confirm removing the account?", "Yes", "No");
                if(answer)
                {
                    await App.Container.Remove();
                    App.Current.MainPage = new NavigationPage(new NetworkSelectionPage());
                }
                else
                {

                }
            });
        }
    }
}