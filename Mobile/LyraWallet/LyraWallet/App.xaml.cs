using System;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using LyraWallet.Views;
using LyraWallet.Services;
using System.IO;
using LyraWallet.Models;

[assembly: XamlCompilation(XamlCompilationOptions.Compile)]
namespace LyraWallet
{
    public partial class App : Application
    {
        public static WalletContainer Container;

        public App()
        {
            Container = new WalletContainer();

            InitializeComponent();

            App.Container.DataStoragePath = DependencyService.Get<IPlatformSvc>().GetStoragePath();
            App.Container.WalletFn = App.Container.DataStoragePath + "My Account.db";
            if(File.Exists(App.Container.WalletFn))
            {
                var netfn = App.Container.DataStoragePath + "network.txt";
                if(!File.Exists(netfn))
                {
                    File.WriteAllText(netfn, "devnet1");
                }
                App.Container.CurrentNetwork = File.ReadAllText(netfn);
                MainPage = new MainPage();
            }
            else
            {
                MainPage = new NavigationPage(new NetworkSelectionPage());
            }    
        }

        protected override void OnStart()
        {
            
        }

        protected override void OnSleep()
        {
            // close wallet
            Container.CloseWallet().Wait();
        }

        protected override void OnResume()
        {
            // re-open wallet
            Container.OpenWalletFile().Wait();
        }
    }
}
