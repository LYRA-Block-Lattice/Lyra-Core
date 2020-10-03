using LyraWallet.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace LyraWallet.Views
{
	[QueryProperty("Account", "account")]
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class BarcodeGenPage : ContentPage
	{
		public BarcodeGenPage ()
		{
			InitializeComponent ();
		}

        public string Account
        {
            set
            {
                txtAddress.Text = value;
                BarcodeImageView.BarcodeValue = $"lyra://localhost/payme?AccountID={value}";
                BarcodeImageView.IsVisible = false;
                BarcodeImageView.IsVisible = true;
            }
        }
    }
}