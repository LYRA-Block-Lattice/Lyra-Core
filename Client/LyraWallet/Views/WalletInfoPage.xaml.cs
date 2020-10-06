using LyraWallet.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Xamarin.Essentials;
using LyraWallet.States;
using LyraWallet.Services;
using Acr.UserDialogs;
using Lyra.Core.Cryptography;
using System.IO;
using System.Threading;

namespace LyraWallet.Views
{
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class WalletInfoPage : ContentPage
	{
        private CancellationTokenSource _cancel;

        public WalletInfoPage ()
		{
			InitializeComponent ();
            var vm = new WalletInfoViewModel();
            BindingContext = vm;

            // Your label tap event
            var showPrivateKey = new TapGestureRecognizer();
            showPrivateKey.Tapped += (s, e) =>
            {
                if (txtPrivateKey.Text.StartsWith("*"))
                    txtPrivateKey.Text = vm.PrivateKey;
                else
                    txtPrivateKey.Text = "********************************";
            };
            lblViewKey.GestureRecognizers.Add(showPrivateKey);

            _cancel = new CancellationTokenSource();
            // redux
            App.Store.Select(state => state.wallet.VoteFor)
                .Subscribe(w =>
                {
                    UserDialogs.Instance.HideLoading();
                }, _cancel.Token);
        }

        private async void CopyAccountID_Clicked(object sender, EventArgs e)
        {
            await Clipboard.SetTextAsync(txtAccountID.Text);
        }

        private async void CopyPrivateKey_Clicked(object sender, EventArgs e)
        {
            await Clipboard.SetTextAsync(txtPrivateKey.Text);
        }

        private async void RemoveAccount_Clicked(object sender, EventArgs e)
        {
            bool answer = await DisplayAlert("Are you sure?", "If you not backup private key properly, all Tokens will be lost after account removing. Confirm removing the account?", "Yes", "No");
            if (answer)
            {
                UserDialogs.Instance.ShowLoading("Removing wallet data...");

                _cancel.Cancel();
                App.WalletSubscribeCancellation.Cancel();

                var path = DependencyService.Get<IPlatformSvc>().GetStoragePath();
                _ = Task.Run(() => {
                    App.Store.Dispatch(new WalletRemoveAction
                    {
                        path = path,
                        name = "default"
                    });
                });

                var fn = $"{path}/default.lyrawallet";
                while (File.Exists(fn))
                {
                    await Task.Delay(100);
                }

                UserDialogs.Instance.HideLoading();

                var wp = new WalletPassword(false);
                wp.Path = path;
                App.Current.MainPage = new NavigationPage(wp);
            }
            else
            {

            }
        }

        private async void ChangeVote_Clicked(object sender, EventArgs e)
        {
            var bt = BindingContext as WalletInfoViewModel;

            if(bt.VoteFor != App.Store.State.wallet.VoteFor)
            {
                UserDialogs.Instance.ShowLoading("Changing vote for...");

                if (string.IsNullOrWhiteSpace(bt.VoteFor))
                    _ = Task.Run(() =>
                    {
                        App.Store.Dispatch(new WalletChangeVoteAction
                        {
                            wallet = App.Store.State.wallet,
                            VoteFor = ""
                        });
                    });
                else if (Signatures.ValidateAccountId(bt.VoteFor))
                    _ = Task.Run(() =>
                    {
                        App.Store.Dispatch(new WalletChangeVoteAction
                        {
                            wallet = App.Store.State.wallet,
                            VoteFor = bt.VoteFor
                        });
                    });
            }
            else
            {
                await DisplayAlert("Info", "Vote for not changed", "OK");
            }
        }
    }
}