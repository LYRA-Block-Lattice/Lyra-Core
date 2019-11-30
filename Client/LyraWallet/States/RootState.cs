using LyraWallet.States.Holding;
using LyraWallet.States.Shop;
using System;
using System.Collections.Generic;
using System.Text;

namespace LyraWallet.States
{
    public class RootState
    {
        public string Network { get; set; }

        public HoldingState walletState {get; set;}

        public ShopState shopState { get; set; }

        public ExchangeState exchangeState { get; set; }

        public static RootState InitialState =>
            new RootState
            {
                walletState = HoldingState.InitialState,
                shopState = ShopState.InitialState,
                exchangeState = ExchangeState.InitialState,
            };
    }
}
