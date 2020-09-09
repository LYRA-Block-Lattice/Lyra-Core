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
        }

        private async void OnMenuItemClicked(object sender, EventArgs e)
        {
            await Browser.OpenAsync(LyraGlobal.PRODUCTWEBLINK, BrowserLaunchMode.SystemPreferred);
            //await Shell.Current.GoToAsync("//LoginPage");
        }
    }
}
