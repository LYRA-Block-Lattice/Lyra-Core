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
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class BarcodeGenPage : ContentPage
	{
		public BarcodeGenPage (string txtToGen)
		{
			InitializeComponent ();

            BindingContext = new BarcodeGenViewModel(txtToGen);
            txtAddress.Text = txtToGen;
            BarcodeImageView.BarcodeValue = txtToGen;
            BarcodeImageView.IsVisible = false;
            BarcodeImageView.IsVisible = true;
		}
	}
}