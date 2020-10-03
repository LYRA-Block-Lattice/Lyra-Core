using LyraWallet.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LyraWallet.ViewModels
{
    public class PosCheckoutViewModel : BaseViewModel
    {
        private Decimal _total;
        public Decimal TotalPayment 
        { 
            get
            {
                return _total;
            }
            set
            {
                SetProperty(ref _total, value);
            }
        }

        private string _token;
        public string PaymentToken { get => _token; set => SetProperty(ref _token, value); }

        public PosCheckoutViewModel()
        {

        }

        public String BarcodeString
        {
            get
            {
                return $"lyra://localhost/cart/checkout?AccountID={App.Store.State.wallet.AccountId}&&Shop=[shop name]&&Token={PaymentToken}&&Total={TotalPayment}";
            }
        }
    }
}
