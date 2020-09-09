using Converto;
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
                    On<WalletNetworkSelectedAction, RootState>(
                        (state, action) => state.With(new {Network = action.network })
                        ),

                    On<WalletOpenResultAction, RootState>(
                        (state, action) => {
                            return state.With(new {
                                wallet = action.wallet,
                                IsOpening = true
                            });
                        }
                    ),
                    On<WalletRestoreAction, RootState>(
                        state => 
                        {
                            //var newPages = state.Pages.RemoveAt(state.Pages.Length - 1);
                            //return state.With(new {
                            //    CurrentPage = newPages.LastOrDefault(),
                            //    Pages = newPages
                            //});
                            return null;
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
