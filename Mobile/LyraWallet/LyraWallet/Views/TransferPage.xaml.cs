using crypto;
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
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class TransferPage : ContentPage
	{
		public TransferPage ()
		{
			InitializeComponent ();

            BindingContext = new TransferViewModel(this);
		}

        private async void Paste_Clicked(object sender, EventArgs e)
        {
            txtAddress.Text = await Clipboard.GetTextAsync();
        }
    }
}