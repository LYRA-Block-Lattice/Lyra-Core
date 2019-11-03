using LyraWallet.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LyraWallet.ViewModels
{
    public class PosCheckoutViewModel : BaseViewModel
    {
        // lyra://localhost/cart/checkout/?AccountID=[xxxxxx]&&Shop=[shop name]&&Token=Lyra.LeX&&Total=123.3
        private List<CartItem> itemsToCheckout;

        public Decimal TotalPayment
        {
            get
            {
                return itemsToCheckout.Sum(a => a.Count * a.product.Price);
            }
        }

        public string PaymentToken
        {
            get
            {
                return itemsToCheckout.First().product.PricingToken;
            }
        }
        public PosCheckoutViewModel(List<CartItem> items)
        {
            itemsToCheckout = items;
        }

        public String BarcodeString
        {
            get
            {
                return $"lyra://localhost/cart/checkout/?AccountID={App.Container.AccountID}&&Shop=[shop name]&&Token={PaymentToken}&&Total={TotalPayment}";
            }
        }
    }
}
