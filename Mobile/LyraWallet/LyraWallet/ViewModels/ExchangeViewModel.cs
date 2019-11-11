using LyraWallet.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace LyraWallet.ViewModels
{
    public class ExchangeViewModel : BaseViewModel
    {
        private List<string> _tokenList;
        public List<string> TokenList
        {
            get
            {
                return _tokenList;
            }
            set
            {
                var st = SelectedToken;
                SetProperty(ref _tokenList, value);
                SelectedToken = st;
            }
        }

        public string FilterKeyword { get; set; }
        public string TargetTokenBalance { get => _targetTokenBalance; set => SetProperty(ref _targetTokenBalance, value); }
        public string LeXBalance { get => _lexBalance; set => SetProperty(ref _lexBalance, value); }
        public string SelectedToken { get => _selectedToken; set {
                SetProperty(ref _selectedToken, value);
                UpdateHoldings();
            }
        }

        private string _selectedToken;
        private string _targetTokenBalance;
        private string _lexBalance;

        public ExchangeViewModel()
        {
            MessagingCenter.Subscribe<BalanceViewModel>(
                this, MessengerKeys.BalanceRefreshed, async (sender) =>
                {
                    await Touch();
                    UpdateHoldings();
                });
        }

        private void UpdateHoldings()
        {
            if(App.Container.Balances == null)
            {
                LeXBalance = $"Hold Lyra.LeX: 0";
                TargetTokenBalance = $"Hold {SelectedToken}: 0";
            }
            else
            {
                LeXBalance = $"Hold Lyra.LeX: {App.Container.Balances["Lyra.LeX"]}";
                if(SelectedToken == null)
                {
                    TargetTokenBalance = "";
                }
                else
                {
                    if (App.Container.Balances.ContainsKey(SelectedToken))
                    {
                        TargetTokenBalance = $"Hold {SelectedToken}: {App.Container.Balances[SelectedToken]}";
                    }
                    else
                    {
                        TargetTokenBalance = $"Hold {SelectedToken}: 0";
                    }
                }
            }
        }

        internal async Task Touch()
        {
            try
            {
                TokenList = await App.Container.GetTokens(FilterKeyword);
            }
            catch(Exception ex)
            {
                TokenList = new List<string>() { ex.Message };
            }
        }
    }
}
