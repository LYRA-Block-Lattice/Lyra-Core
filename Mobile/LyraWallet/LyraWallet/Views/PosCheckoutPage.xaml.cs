using LyraWallet.Models;
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
    public partial class PosCheckoutPage : ContentPage
    {
        public PosCheckoutPage(List<CartItem> items)
        {
            InitializeComponent();

            BindingContext = new PosCheckoutViewModel(items);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            BarcodeImageView.BarcodeValue = (BindingContext as PosCheckoutViewModel).BarcodeString;
            BarcodeImageView.IsVisible = false;
            BarcodeImageView.IsVisible = true;
        }
    }
}