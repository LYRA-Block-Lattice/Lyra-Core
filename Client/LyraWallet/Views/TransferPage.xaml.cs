using LyraWallet.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using ZXing;
using ZXing.Net.Mobile.Forms;

namespace LyraWallet.Views
{
    [QueryProperty("Token", "token")]
    [QueryProperty("Account", "account")]
    [QueryProperty("Amount", "amount")]
    [XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class TransferPage : ContentPage
	{
		public TransferPage ()
		{
			InitializeComponent ();

            //lblTokenName.Text = tokenName;
            var trans = new TransferViewModel();
            BindingContext = trans;
        }

        private async void Paste_Clicked(object sender, EventArgs e)
        {
            txtAddress.Text = await Clipboard.GetTextAsync();
        }

        public string Token
        {
            set
            {
                var trans = BindingContext as TransferViewModel;
                trans.SelectedTokenName = value;
            }
        }

        public string Account
        {
            set
            {
                var trans = BindingContext as TransferViewModel;
                trans.TargetAccount = value;
            }
        }

        public string Amount
        {
            set
            {
                var trans = BindingContext as TransferViewModel;
                trans.Amount = value;
            }
        }
    }
}