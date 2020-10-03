using LyraWallet.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LyraWallet.ViewModels
{
    public class PosCheckoutViewModel : BaseViewModel
    {
        public Decimal TotalPayment { get; set; }
        public string PaymentToken { get; set; }

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
