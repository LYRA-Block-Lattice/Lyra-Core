using LyraWallet.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace LyraWallet.Views
{
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class BalancePage : ContentPage
	{
		public BalancePage ()
		{
			InitializeComponent ();
            BindingContext = new BalanceViewModel(this);

            if (DeviceInfo.Platform == DevicePlatform.UWP)
                btnRefresh.IsVisible = true;
            else
                btnRefresh.IsVisible = false;

            lvBalance.ItemTapped += LvBalance_ItemTapped;
        }

        private void LvBalance_ItemTapped(object sender, ItemTappedEventArgs e)
        {
            if(e.Item != null) 
            {
                var kvp = (KeyValuePair<string, decimal>)e.Item;
                var nextPage = new TransferPage(kvp.Key);
                Navigation.PushAsync(nextPage);
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            await (BindingContext as BalanceViewModel).Open();
        }
    }
}