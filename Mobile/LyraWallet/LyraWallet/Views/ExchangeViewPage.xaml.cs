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
    public partial class ExchangeViewPage : ContentPage
    {
        public ExchangeViewPage()
        {
            InitializeComponent();
            BindingContext = new ExchangeViewModel(this);
        }

        public void Scroll(object item)
        {
            lvSell.ScrollTo(item, ScrollToPosition.MakeVisible, false);
        }
        //protected override async void OnAppearing()
        //{
        //    base.OnAppearing();

        //    //await (BindingContext as ExchangeViewModel).Touch();
        //}
    }
}