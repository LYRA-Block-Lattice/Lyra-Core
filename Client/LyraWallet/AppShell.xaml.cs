using System;
using System.Collections.Generic;
using LyraWallet.ViewModels;
using LyraWallet.Views;
using Xamarin.Forms;

namespace LyraWallet
{
    public partial class AppShell : Xamarin.Forms.Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(BalancePage), typeof(BalancePage));
            Routing.RegisterRoute(nameof(CreateTokenPage), typeof(CreateTokenPage));
        }

        private async void OnMenuItemClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//LoginPage");
        }
    }
}
