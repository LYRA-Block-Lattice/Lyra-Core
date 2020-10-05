using Acr.UserDialogs;
using LyraWallet.States;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace LyraWallet.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class WalletPassword : ContentPage
    {
        private string _path;
        private CancellationTokenSource _cancel;
        public WalletPassword(bool OpenWallet)
        {
            InitializeComponent();

            confirmPassword.IsVisible = !OpenWallet;
            btnAction.Text = OpenWallet ? "Open Wallet" : "Create Wallet";

            _cancel = new CancellationTokenSource();
            // redux
            App.Store.Select(state => state.IsChanged)
                .Subscribe(w =>
                {
                    if (!OpenWallet)
                    {
                        _cancel.Cancel();
                    }
                    else
                    {
                        Device.BeginInvokeOnMainThread(async () =>
                        {
                            UserDialogs.Instance.HideLoading();

                            if (App.Store.State.ErrorMessage != null)
                            {
                                if (App.Store.State.wallet != null)
                                {
                                    _cancel.Cancel();
                                    App.Current.MainPage = new AppShell();
                                }                                    
                                else
                                    await DisplayAlert("Alert", $"Open wallet failed!\n\n" + App.Store.State.ErrorMessage, "Confirm");
                            }
                        });
                    }
                }, 
                _cancel.Token             
                );
        }

        public string Path { set => _path = value; }

        protected async void OnClicked(object source, EventArgs args)
        {
            if(confirmPassword.IsVisible)
            {
                if(password.Text.Length < 8)
                {
                    await DisplayAlert("Alert", $"Password is too short!", "Confirm");
                    return;
                }

                // create new wallet with password
                if(password.Text == confirmPassword.Text)
                {
                    var nsp = new NetworkSelectionPage();
                    nsp.Passenc = Base64UrlEncoder.Encode(password.Text);
                    await Navigation.PushAsync(nsp);
                }
                else
                {
                    await DisplayAlert("Alert", $"Passwords not match!", "Confirm");
                }
            }
            else
            {
                var txt = "Opening wallet...";
                UserDialogs.Instance.ShowLoading(txt);

                _ = Task.Run(() => {
                    var oAct = new WalletOpenAction
                    {
                        path = _path,
                        name = "default",
                        password = password.Text
                    };
                    App.Store.Dispatch(oAct); 
                });                             
            }            
        }
    }
}