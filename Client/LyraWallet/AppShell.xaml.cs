using System;
using System.Collections.Generic;
using Lyra.Core.API;
using LyraWallet.ViewModels;
using LyraWallet.Views;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace LyraWallet
{
    public partial class AppShell : Xamarin.Forms.Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(CreateTokenPage), typeof(CreateTokenPage));
            Routing.RegisterRoute(nameof(CreateAccountPage), typeof(CreateAccountPage));
            Routing.RegisterRoute(nameof(NetworkSelectionPage), typeof(NetworkSelectionPage));
            Routing.RegisterRoute(nameof(RedeemPage), typeof(RedeemPage));
            Routing.RegisterRoute(nameof(AboutPage), typeof(AboutPage));
        }

        private async void OnVisisOnlineClicked(object sender, EventArgs e)
        {
            await Browser.OpenAsync(LyraGlobal.PRODUCTWEBLINK, BrowserLaunchMode.SystemPreferred);
        }

        private async void OnAboutClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("AboutPage");
        }
    }
}
