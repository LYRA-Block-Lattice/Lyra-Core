using Acr.UserDialogs;
using LyraWallet.Services;
using LyraWallet.States;
using LyraWallet.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace LyraWallet.Views
{
	[XamlCompilation(XamlCompilationOptions.Compile)]
    [QueryProperty("Passenc", "passenc")]
    [QueryProperty("Network", "network")]
    public partial class CreateAccountPage : ContentPage
	{
        private CancellationTokenSource _cancel;
        public string Network
        {
            set
            {
                (BindingContext as CreateAccountViewModel).NetworkId = Uri.UnescapeDataString(value);
            }
        }

        public string Passenc { set => (BindingContext as CreateAccountViewModel).Passenc = value; }

        public CreateAccountPage ()
		{
            InitializeComponent ();

            var viewModel = new CreateAccountViewModel();
            BindingContext = viewModel;

            Shell.SetTabBarIsVisible(this, false);

            _cancel = new CancellationTokenSource();
            // redux
            App.Store.Select(state => state)
                .Subscribe(w =>
                {
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        UserDialogs.Instance.HideLoading();

                        if(w.IsOpening)
                        {
                            _cancel.Cancel();
                            App.WalletSubscribeCancellation = new CancellationTokenSource();
                            App.Current.MainPage = new AppShell();
                        }                            
                        else if(w.ErrorMessage != null)
                            await DisplayAlert("Warnning", "Wallet creation or restore failed.\n\n" + w.ErrorMessage, "Confirm");                           
                    });
                }, _cancel.Token);
        }

        protected async void OnCreateNewClicked(object source, EventArgs args)
        {
            // create or restore then goto appshell
            var bt = BindingContext as CreateAccountViewModel;
            var oAct = new WalletCreateAction
            {
                network = bt.NetworkId,
                name = "default",
                password = bt.Password,
                path = DependencyService.Get<IPlatformSvc>().GetStoragePath()
            };

            var txt = "Creating new wallet...";
            UserDialogs.Instance.ShowLoading(txt);
            _ = Task.Run(() => { App.Store.Dispatch(oAct); });
        }

        protected async void OnRestoreClicked(object source, EventArgs args)
        {
            // create or restore then goto appshell
            var bt = BindingContext as CreateAccountViewModel;
            var oAct = new WalletRestoreAction
            {
                privateKey = bt.PrivateKey,
                network = bt.NetworkId,
                name = "default",
                password = bt.Password,
                path = DependencyService.Get<IPlatformSvc>().GetStoragePath()
            };
            var txt = "Restoring wallet and syncing...";
            UserDialogs.Instance.ShowLoading(txt);
            _ = Task.Run(() => { App.Store.Dispatch(oAct); });
        }
    }
}