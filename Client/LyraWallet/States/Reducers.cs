using Converto;
using Lyra.Core.API;
using ReduxSimple;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static ReduxSimple.Reducers;

namespace LyraWallet.States
{
    public static class Reducers
    {
        public static IEnumerable<On<RootState>> CreateReducers()
        {
            return new List<On<RootState>>
                {
                    On<WalletOpenResultAction, RootState>(
                        (state, action) => {
                            return state.With(new {
                                wallet = action.wallet,
                                Balances = action.wallet?.GetLatestBlock()?.Balances?.ToDictionary(k => k.Key, k => (decimal)(k.Value / LyraGlobal.TOKENSTORAGERITO)),
                                IsOpening = true
                            });
                        }
                    ),
                    On<WalletErrorAction, RootState>(
                        (state, action) => 
                        {
                            return state.With(new { ErrorMessage = action.Error.Message });
                        }
                    ),
                };

            //var walletReducers = Holding.Reducers.CreateReducers();
            ////var shopReducers = Shop.Reducers.CreateReducers();
            ////var exchangeReducers = Exchange.Reducers.CreateReducers();

            //return ReduxSimple.Reducers.CreateSubReducers(walletReducers.ToArray(), Holding.Selectors.SelectWalletState);
            //    //.Concat(CreateSubReducers(shopReducers.ToArray(), Shop.Selectors.SelectShopState))
            //    //.Concat(CreateSubReducers(exchangeReducers.ToArray(), Exchange.Selectors.SelectExchangeState));
        }
    }
}
