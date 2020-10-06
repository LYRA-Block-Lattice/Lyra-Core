using LyraWallet.States;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Xamarin.Forms;

namespace LyraWallet.ViewModels
{
    public class RedeemViewModel : BaseViewModel, INotifyDataErrorInfo
    {
        readonly IDictionary<string, List<string>> _errors = new Dictionary<string, List<string>>();

        private IList<string> _tokenNames;
        public IList<string> TokensToRedeem { get => _tokenNames; set => SetProperty(ref _tokenNames, value); }

        private string _selectedToken;
        public string SelectedToken
        {
            get
            {
                return _selectedToken;
            }

            set
            {
                _selectedToken = value;
                Validate(() => !string.IsNullOrWhiteSpace(_selectedToken), "Must select a token to redeem.");
                OnPropertyChanged("SelectedToken");
            }
        }
        private int _tokenCount;
        public int TokenCount { 
            get
            {
                return _tokenCount;
            }
            set
            {
                _tokenCount = value;

                //var balance = App.Store.State.wallet.GetLatestBlock().Balances;
                //if (!string.IsNullOrWhiteSpace(_selectedToken) && balance.ContainsKey(_selectedToken))
                //{
                //    var total = balance[_selectedToken];
                //    Validate(() => _tokenCount <= total, $"You can redeem no more than {total} tokens.");
                //}                
                //else
                //{
                //    Validate(() => false, $"Must select a token to redeem.");
                //}
                
                OnPropertyChanged("TokenCount");
            }
        }

        public ICommand NextCommand
        {
            get => new Command(async () =>
            {
                var oAct = new WalletRedeemAction
                {
                    wallet = App.Store.State.wallet,
                    tokenToRedeem = SelectedToken,
                    countToRedeem = TokenCount
                };
                _ = Task.Run(() => { App.Store.Dispatch(oAct); });

                await Shell.Current.GoToAsync("//BalancePage");
            });
        }

        protected void Validate(Func<bool> rule, string error,
                [CallerMemberName] string propertyName = "")
        {
            if (string.IsNullOrWhiteSpace(propertyName)) return;

            if (_errors.ContainsKey(propertyName))
            {
                _errors.Remove(propertyName);
            }

            if (rule() == false)
            {
                _errors.Add(propertyName, new List<string> { error });
            }

            OnPropertyChanged(nameof(HasErrors));

            ErrorsChanged?.Invoke(this,
                new DataErrorsChangedEventArgs(propertyName));
        }

        public bool HasErrors => _errors?.Any(x => x.Value?.Any() == true) == true;

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        public IEnumerable GetErrors(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return _errors.SelectMany(x => x.Value);
            }

            if (_errors.ContainsKey(propertyName)
                && _errors[propertyName].Any())
            {
                return _errors[propertyName];
            }

            return new List<string>();
        }
    }
}
