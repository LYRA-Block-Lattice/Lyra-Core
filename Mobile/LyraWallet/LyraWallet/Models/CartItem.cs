using LyraWallet.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace LyraWallet.Models
{
    public class CartItem : BaseViewModel
    {
        public Product product { get; set; }

        private int count;

        public int Count
        {
            get
            {
                return count;
            }
            set
            {
                SetProperty(ref count, value);
            }
        }
    }
}
