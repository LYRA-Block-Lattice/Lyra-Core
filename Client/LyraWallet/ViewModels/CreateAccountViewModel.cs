using LyraWallet.Services;
using LyraWallet.States;
using LyraWallet.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Input;
using Xamarin.Forms;

namespace LyraWallet.ViewModels
{
	public class CreateAccountViewModel : BaseViewModel
    {
        private Page _thePage;
		public CreateAccountViewModel (Page page)
		{
            PrivateKey = "";
            _thePage = page;
		}

        private string privateKey;
        public string PrivateKey
        {
            get => privateKey;
            set => SetProperty(ref privateKey, value);
        }

        private string networkId;
        public string NetworkId
        {
            get => networkId;
            set => SetProperty(ref networkId, value);
        }

        public ICommand CreateNewCommand
        {
            get => new Command(async () =>
            {
                try
                {
                    //App.Container.CreateNew(NetworkId);
                    //await App.Container.CloseWallet();

                    //var nextPage = new BalancePage();
                    //Application.Current.MainPage = nextPage;

                    App.Store.Dispatch(new WalletCreateAction
                    {
                        // TODO
                        network = App.Store.State.Network,
                        name = "default.lyrawallet",
                        password = "",
                        path = DependencyService.Get<IPlatformSvc>().GetStoragePath()
                    });

                    await Shell.Current.GoToAsync("//BalancePage");
                }
                catch(Exception ex)
                {
                    await _thePage.DisplayAlert("Error", ex.Message, "OK");
                }
            });
        }

        public ICommand CreateByKeyCommand
        {
            get => new Command(async () =>
            {
                try
                {
                    App.Container.CreateByPrivateKey(NetworkId, PrivateKey);
                    await App.Container.CloseWallet();

                    var nextPage = new BalancePage();
                    Application.Current.MainPage = nextPage;
                }
                catch(Exception ex)
                {
                    await _thePage.DisplayAlert("Error", ex.Message, "OK");
                }
            });
        }
    }
}