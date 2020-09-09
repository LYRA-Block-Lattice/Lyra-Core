using Lyra.Core.Accounts;
using LyraWallet.States.Holding;
using LyraWallet.States.Shop;
using System;
using System.Collections.Generic;
using System.Text;

namespace LyraWallet.States
{
    public class RootState
    {
        public bool IsOpening { get; set; }
        public Wallet wallet { get; set; }
        public List<string> txs { get; set; }

        public string ErrorMessage { get; set; }

        public HoldingState walletState {get; set;}

        //public ShopState shopState { get; set; }

        public ExchangeState exchangeState { get; set; }

        public static RootState InitialState =>
            new RootState
            {
                IsOpening = false,
                wallet = null,
                txs = null,

                walletState = HoldingState.InitialState,
                //shopState = ShopState.InitialState,
                exchangeState = ExchangeState.InitialState,
            };
    }
}
