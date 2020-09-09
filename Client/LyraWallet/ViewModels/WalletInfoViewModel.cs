using Lyra.Core.API;
using Lyra.Core.Cryptography;
using LyraWallet.Services;
using LyraWallet.States;
using LyraWallet.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace LyraWallet.ViewModels
{
	public class WalletInfoViewModel : BaseViewModel
    {
        private Page _thePage;

        public string PrivateKey { get; set; }
        public string AccountID { get; set; }
        public string CurrentNetwork { get; set; }
        public string VoteFor { get; set; }

        public ICommand ChangeVoteCommand { get; }
        public ICommand BarcodeGenCommand { get; }
        public ICommand CreateTokenCommand { get; }
        public ICommand RedeemCodeCommand { get; }
        public ICommand ShowBlocksCommand { get; }
        public ICommand RemoveAccountCommand { get; }
        public ICommand VisitCommunityCommand { get;  }
        

        public WalletInfoViewModel (Page page)
		{
            _thePage = page;

            // redux
            App.Store.Select(state => state.wallet)
                .Subscribe(w =>
                {
                    this.PrivateKey = w?.PrivateKey;
                    this.AccountID = w?.AccountId;
                    this.CurrentNetwork = w?.NetworkId;
                    this.VoteFor = w?.VoteFor;
                });

            ChangeVoteCommand = new Command(async () => {
                if (string.IsNullOrWhiteSpace(VoteFor))
                    App.Container.VoteFor = "";
                else if (Signatures.ValidateAccountId(VoteFor))
                    App.Container.VoteFor = VoteFor;
                else
                    await _thePage.DisplayAlert("Alert", "Not a valid Account ID. If unvote, leave it blank.", "OK");
            });

            BarcodeGenCommand = new Command(async () =>
            {
                var nextPage = new BarcodeGenPage($"lyra://localhost/payme?AccountID={AccountID}", AccountID);
                await _thePage.Navigation.PushAsync(nextPage);
            });

            VisitCommunityCommand = new Command(async () =>
            {
                await Browser.OpenAsync(LyraGlobal.PRODUCTWEBLINK, BrowserLaunchMode.SystemPreferred);
                //var nextPage = new LexCommunityPage();
                //await _thePage.Navigation.PushAsync(nextPage);
            });

            CreateTokenCommand = new Command(async () =>
            {
                var nextPage = new CreateTokenPage();
                await _thePage.Navigation.PushAsync(nextPage);
            });
            RedeemCodeCommand = new Command(() =>
            {

            });
            ShowBlocksCommand = new Command(async () =>
            {
                var nextPage = new BlockListPage();
                await _thePage.Navigation.PushAsync(nextPage);
            });
            RemoveAccountCommand = new Command(async () =>
            {
                bool answer = await _thePage.DisplayAlert("Are you sure?", "If you not backup private key properly, all Tokens will be lost after account removing. Confirm removing the account?", "Yes", "No");
                if(answer)
                {
                    App.Store.Dispatch(new WalletRemoveAction
                    {
                        path = DependencyService.Get<IPlatformSvc>().GetStoragePath(),
                        name = "default"
                    });

                    await Shell.Current.GoToAsync("//LoginPage");
                }
                else
                {

                }
            });
        }
    }
}