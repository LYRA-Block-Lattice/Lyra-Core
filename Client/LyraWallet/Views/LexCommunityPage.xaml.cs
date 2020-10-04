using Lyra.Core.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace LyraWallet.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class LexCommunityPage : ContentPage
    {
        public LexCommunityPage()
        {
            InitializeComponent();
            webView.Source = LyraGlobal.PRODUCTWEBLINK;

            Shell.SetTabBarIsVisible(this, false);
        }

        async void OnBackButtonClicked(object sender, EventArgs e)
        {
            if (webView.CanGoBack)
            {
                webView.GoBack();
            }
            else
            {
                await Navigation.PopAsync();
            }
        }

        void OnForwardButtonClicked(object sender, EventArgs e)
        {
            if (webView.CanGoForward)
            {
                webView.GoForward();
            }
        }

        void OnHomeButtonClicked(object sender, EventArgs e)
        {
            webView.Source = LyraGlobal.PRODUCTWEBLINK;
        }

        void OnRefreshButtonClicked(object sender, EventArgs e)
        {
            webView.Reload();
        }

        void webviewNavigating(object sender, WebNavigatingEventArgs e)
        {
            labelLoading.IsVisible = true;
        }

        void webviewNavigated(object sender, WebNavigatedEventArgs e)
        {
            labelLoading.IsVisible = false;
        }
    }
}