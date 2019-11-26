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

            lvSell.ItemTapped += LvSell_ItemTapped;
            lvBuy.ItemTapped += LvBuy_ItemTapped;
        }

        private void LvBuy_ItemTapped(object sender, ItemTappedEventArgs e)
        {
            if (e.Item != null)
            {
                var kvp = (KeyValuePair<string, string>)e.Item;
                if(!string.IsNullOrWhiteSpace(kvp.Key))
                {
                    txtSellPrice.Text = kvp.Key;
                    txtSellAmount.Text = kvp.Value;
                }
            }
        }

        private void LvSell_ItemTapped(object sender, ItemTappedEventArgs e)
        {
            if (e.Item != null)
            {
                var kvp = (KeyValuePair<string, string>)e.Item;
                if (!string.IsNullOrWhiteSpace(kvp.Key))
                {
                    txtBuyPrice.Text = kvp.Key;
                    txtBuyAmount.Text = kvp.Value;
                }
            }
        }

        public void Scroll(object item)
        {
            lvSell.ScrollTo(item, ScrollToPosition.MakeVisible, false);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            model.ThePage = this;
        }
    }
}