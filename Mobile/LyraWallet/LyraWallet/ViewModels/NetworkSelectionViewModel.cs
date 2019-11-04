using Lyra.Core.API;
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
        public IList<string> LyraNetworks => LyraGlobal.Networks;

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
                await App.Container.CloseWallet();
                var nextPage = new CreateAccountPage(SelectedNetwork);
                await Application.Current.MainPage.Navigation.PushAsync(nextPage);
            });
        }
    }
}