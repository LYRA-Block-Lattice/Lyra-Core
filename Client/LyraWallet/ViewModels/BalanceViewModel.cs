using System;
using System.Windows.Input;

using Xamarin.Forms;
using LyraWallet.Services;

using LyraWallet.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using LyraWallet.Views;
using System.Linq;
using System.IO;
using ZXing.Net.Mobile.Forms;
using Lyra.Core.API;
using LyraWallet.States;

namespace LyraWallet.ViewModels
{
    public class BalanceViewModel : BaseViewModel
    {
        private Dictionary<string, Decimal> _balances;
        public Dictionary<string, Decimal> Balances
        {
            get => _balances;
            set
            {
                SetProperty(ref _balances, value);
            }
        }

        private bool _notGetLex;
        public bool CanPay
        {
            get => _notGetLex;
            set => SetProperty(ref _notGetLex, value);
        }

        private bool _isRefreshing;
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set
            {
                SetProperty(ref _isRefreshing, value);
                Device.BeginInvokeOnMainThread(() =>
                {
                    if (_isRefreshing)
                        Title = "Updating Balances...";
                    else
                        Title = "Balance";
                });
                
            }
        }

        public BalanceViewModel()
        {
            //App.Container.PropertyChanged += (o, e) => OnPropertyChanged(e.PropertyName);
            //App.Container.OnBalanceChanged += async (act, cat, info) => await Refresh();

            //Title = "Balance";
            //GetLEX = false;
            
            RefreshCommand = new Command(() =>
            {
                App.Store.Dispatch(new WalletRefreshBalanceAction
                {
                    wallet = App.Store.State.wallet
                });
            });


        }

        public async Task Open()
        {
            //if (App.Container.Balances != null)     // don't reopen
            //    return;

            //int times = 0;
            ////while(times < 30)
            ////{
            //    try
            //    {
            //        // check orleans ready state

            //        Title = "Opening Wallet...";
            //        await App.Container.CloseWallet();
            //        await App.Container.OpenWalletFileAsync();
            //        App.Container.GetBalance();
            //        if (App.Container.Balances == null || App.Container.Balances.Count == 0)
            //        {
            //            GetLEX = true;
            //        }

            //        MessagingCenter.Send(this, MessengerKeys.BalanceRefreshed);
            //        await Refresh();
            //        //break;
            //    }
            //    catch (Exception ex)
            //    {
            //        times++;
            //        await Task.Delay(2000 * times);
            //        Title = $"Retry #{times} Opening Wallet...";
            //    }
            ////}

        }

        //private async Task Refresh()
        //{
        //    try
        //    {
        //        IsRefreshing = true;
        //        await App.Container.RefreshBalance();
        //        if (App.Container.Balances == null || App.Container.Balances.Count == 0)
        //        {
        //            GetLEX = true;
        //        }
        //        else
        //        {
        //            GetLEX = false;
        //        }
        //        IsRefreshing = false;
        //        MessagingCenter.Send(this, MessengerKeys.BalanceRefreshed);
        //    }
        //    catch (Exception ex)
        //    {
        //        IsRefreshing = false;
        //        Device.BeginInvokeOnMainThread(() =>
        //        {
        //            _thePage.DisplayAlert("Error Opening Wallet", $"Network: {App.Container.CurrentNetwork}\nError Message: {ex.Message}", "OK");
        //        });
        //    }
        //}

        public ICommand RefreshCommand { get; }
        public ICommand TokenSelected { get; }
        public ICommand ScanCommand { get; }
    }
}