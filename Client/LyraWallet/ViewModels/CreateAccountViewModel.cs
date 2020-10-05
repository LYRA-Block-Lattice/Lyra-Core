using LyraWallet.Services;
using LyraWallet.States;
using LyraWallet.Views;
using Microsoft.IdentityModel.Tokens;
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
        private string _password;
		public CreateAccountViewModel ()
		{
            PrivateKey = "";
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
                    await Shell.Current.GoToAsync($"//BalancePage?action=create&network={NetworkId}");
                }
                catch(Exception ex)
                {
                    //await _thePage.DisplayAlert("Error", ex.Message, "OK");
                }
            });
        }

        public ICommand CreateByKeyCommand
        {
            get => new Command(async () =>
            {
                try
                {
                    if(string.IsNullOrWhiteSpace(PrivateKey))
                    {
                        //await _thePage.DisplayAlert("Error", "No private key specified.", "OK");
                        return;
                    }
                    await Shell.Current.GoToAsync($"//BalancePage?action=restore&network={NetworkId}&key={PrivateKey}");
                }
                catch(Exception ex)
                {
                    //await _thePage.DisplayAlert("Error", ex.Message, "OK");
                }
            });
        }
        public string Passenc { set { _password = Base64UrlEncoder.Decode(value); } }
        public string Password => _password;
    }
}