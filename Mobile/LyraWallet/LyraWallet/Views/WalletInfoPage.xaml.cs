using LyraWallet.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Xamarin.Essentials;

namespace LyraWallet.Views
{
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class WalletInfoPage : ContentPage
	{
		public WalletInfoPage ()
		{
			InitializeComponent ();
            BindingContext = new WalletInfoViewModel(this);

            // Your label tap event
            var showPrivateKey = new TapGestureRecognizer();
            showPrivateKey.Tapped += (s, e) =>
            {
                if (txtPrivateKey.Text.StartsWith("*"))
                    txtPrivateKey.Text = App.Container.PrivateKey;
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
    }
}