using System;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using LyraWallet.Views;
using LyraWallet.Services;
using System.IO;
using LyraWallet.Models;
using ReduxSimple;
using LyraWallet.States;

[assembly: XamlCompilation(XamlCompilationOptions.Compile)]
namespace LyraWallet
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; set; }

        public static readonly ReduxStore<RootState> Store =
            new ReduxStore<RootState>(States.Reducers.CreateReducers(), RootState.InitialState, true);

        public App()
        {
            InitializeComponent();

            Store.RegisterEffects(
                LyraWallet.States.Effects.CreateWalletEffect,
                LyraWallet.States.Effects.OpenWalletEffect,
                LyraWallet.States.Effects.RestoreWalletEffect,
                LyraWallet.States.Effects.RemoveWalletEffect,
                LyraWallet.States.Effects.ChangeVoteWalletEffect,
                LyraWallet.States.Effects.RefreshWalletEffect,
                LyraWallet.States.Effects.SendTokenWalletEffect,
                LyraWallet.States.Effects.CreateTokenWalletEffect,
                LyraWallet.States.Effects.ImportWalletEffect,
                LyraWallet.States.Effects.RedeemWalletEffect,
                LyraWallet.States.Effects.NonFungibleTokenWalletEffect
                );

            MainPage = new AppShell();
        }

        protected override void OnSleep()
        {
            // close wallet
            //Container.CloseWallet();
        }

        protected override void OnResume()
        {
            // re-open wallet
            //Container.OpenWalletFileAsync();
        }
    }
}
