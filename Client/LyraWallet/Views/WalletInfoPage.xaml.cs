using LyraWallet.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Xamarin.Essentials;
using LyraWallet.States;
using LyraWallet.Services;

namespace LyraWallet.Views
{
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class WalletInfoPage : ContentPage
	{
		public WalletInfoPage ()
		{
			InitializeComponent ();
            var vm = new WalletInfoViewModel();
            BindingContext = vm;

            // Your label tap event
            var showPrivateKey = new TapGestureRecognizer();
            showPrivateKey.Tapped += (s, e) =>
            {
                if (txtPrivateKey.Text.StartsWith("*"))
                    txtPrivateKey.Text = vm.PrivateKey;
                else
                    txtPrivateKey.Text = "********************************";
            };
            lblViewKey.GestureRecognizers.Add(showPrivateKey);
        }

        private async void CopyAccountID_Clicked(object sender, EventArgs e)
        {
            await Clipboard.SetTextAsync(txtAccountID.Text);
        }

        private async void CopyPrivateKey_Clicked(object sender, EventArgs e)
        {
            await Clipboard.SetTextAsync(txtPrivateKey.Text);
        }

        private async void RemoveAccount_Clicked(object sender, EventArgs e)
        {
            bool answer = await DisplayAlert("Are you sure?", "If you not backup private key properly, all Tokens will be lost after account removing. Confirm removing the account?", "Yes", "No");
            if (answer)
            {
                App.Store.Dispatch(new WalletRemoveAction
                {
                    path = DependencyService.Get<IPlatformSvc>().GetStoragePath(),
                    name = "default"
                });

                await Shell.Current.GoToAsync("NetworkSelectionPage");
            }
            else
            {

            }
        }
    }
}