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
    [QueryProperty("Total", "total")]
    [QueryProperty("Token", "token")]
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class PosCheckoutPage : ContentPage
    {
        public PosCheckoutPage()
        {
            InitializeComponent();

            BindingContext = new PosCheckoutViewModel();

            Shell.SetTabBarIsVisible(this, false);
        }

        public string Total
        {
            set
            {
                var bt = BindingContext as PosCheckoutViewModel;
                bt.TotalPayment = decimal.Parse(value);
            }
        }

        public string Token
        {
            set
            {
                var bt = BindingContext as PosCheckoutViewModel;
                bt.PaymentToken = value;
            }
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