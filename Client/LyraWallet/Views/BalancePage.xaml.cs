using Lyra.Core.API;
using LyraWallet.Services;
using LyraWallet.States;
using LyraWallet.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
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
            var vm = new BalanceViewModel(this);
            BindingContext = vm;

            //if (DeviceInfo.Platform == DevicePlatform.UWP)
            //    btnRefresh.IsVisible = true;
            //else
            //    btnRefresh.IsVisible = false;

            lvBalance.ItemTapped += LvBalance_ItemTapped;

            // redux
            App.Store.Select(state => state.Balances)
                .Subscribe(w =>
                {
                    vm.Balances = w;
                    vm.IsRefreshing = false;

                    if (w != null && w.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
                        vm.CanPay = true;
                    else
                        vm.CanPay = false;
                });

            App.Store.Select(state => state.ErrorMessage)
                .Subscribe(w =>
                {
                    // display error message here
                });
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

            // check init load or re-open
            // if init load, goto create/restore
            // if re-open, refresh balance

            if(App.Store.State.IsOpening)
            {
                // refresh balance
            }
            else
            {
                // check default wallet exists. 
                var path = DependencyService.Get<IPlatformSvc>().GetStoragePath();
                var fn = $"{path}/default.lyrawallet";
                if (File.Exists(fn))
                {
                    App.Store.Dispatch(new WalletOpenAction { 
                        path = path,
                        name = "default",
                        password = ""
                    });
                }
                else
                {
                    await Shell.Current.GoToAsync("//LoginPage");
                }
            }
        }
    }
}