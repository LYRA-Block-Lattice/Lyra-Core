using System;
using System.Text;
using LyraWallet.Models;
using System.Collections.Immutable;

namespace LyraWallet.States.Shop
{
    public class ShopState
    {
        public ImmutableList<Product> Products { get; set; }

        public static ShopState InitialState =>
            new ShopState
            {

            };
    }
}
