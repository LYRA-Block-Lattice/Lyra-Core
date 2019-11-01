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
	public partial class BalancePage : ContentPage
	{
		public BalancePage ()
		{
			InitializeComponent ();
            BindingContext = new BalanceViewModel(this);

            btnRefresh.IsVisible = Device.OnPlatform<bool>(false, false, true);

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
    }
}