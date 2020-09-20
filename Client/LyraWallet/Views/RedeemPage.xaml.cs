using LyraWallet.ViewModels;
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
    public partial class RedeemPage : ContentPage
    {
        public RedeemPage()
        {
            InitializeComponent();

            App.Store.Select(state => state.Balances)
                .Subscribe(w =>
                {
                    var vm = BindingContext as RedeemViewModel;
                    vm.TokensToRedeem = w?.Keys.ToList();
                });
        }
    }
}