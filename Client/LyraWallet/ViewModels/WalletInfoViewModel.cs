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
        public string PrivateKey { get; set; }
        public string AccountID { get; set; }
        public string CurrentNetwork { get; set; }
        public string VoteFor { get; set; }

        public ICommand ChangeVoteCommand { get; }
        public ICommand BarcodeGenCommand { get; }
        public ICommand CreateTokenCommand { get; }
        public ICommand RedeemCodeCommand { get; }
        public ICommand ShowBlocksCommand { get; }
        public ICommand VisitCommunityCommand { get;  }
        

        public WalletInfoViewModel ()
		{
            // redux
            App.Store.Select(state => state.wallet)
                .Subscribe(w =>
                {
                    this.PrivateKey = w?.PrivateKey;
                    this.AccountID = w?.AccountId;
                    this.CurrentNetwork = w?.NetworkId;
                    this.VoteFor = w?.VoteFor;
                }, App.WalletSubscribeCancellation.Token);

            BarcodeGenCommand = new Command(async () =>
            {
                await Shell.Current.GoToAsync($"BarcodeGenPage?account={AccountID}");
            });

            VisitCommunityCommand = new Command(async () =>
            {
                await Shell.Current.GoToAsync("LexCommunityPage");
            });

            CreateTokenCommand = new Command(async () =>
            {
                await Shell.Current.GoToAsync("CreateTokenPage");
            });
            RedeemCodeCommand = new Command(() =>
            {

            });
            ShowBlocksCommand = new Command(async () =>
            {
                await Shell.Current.GoToAsync("BlockListPage");
            });
        }
    }
}