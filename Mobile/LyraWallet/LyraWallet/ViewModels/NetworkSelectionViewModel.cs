using LyraWallet.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using Xamarin.Forms;

namespace LyraWallet.ViewModels
{
	public class NetworkSelectionViewModel : BaseViewModel
    {
        private readonly IList<string> _networks = new [] { "mainnet", "testnet",
            "stagenet", "devnet0", "devnet1", "local", "lexnet"
        };

        public IList<string> LyraNetworks => _networks;

        private string _selectedNetwork;
        public string SelectedNetwork
        {
            get => _selectedNetwork;
            set => SetProperty(ref _selectedNetwork, value);
        }

        public NetworkSelectionViewModel ()
		{
            Title = "Network Selection";
		}

        public ICommand NextCommand
        {
            get => new Command(async () =>
            {
                App.Container.CurrentNetwork = SelectedNetwork;
                var nextPage = new CreateAccountPage(SelectedNetwork);
                await Application.Current.MainPage.Navigation.PushAsync(nextPage);
            });
        }
    }
}