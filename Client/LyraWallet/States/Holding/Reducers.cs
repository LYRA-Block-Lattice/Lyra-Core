using Converto;
using Lyra.Core.Accounts;
using Lyra.Core.API;
using ReduxSimple;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using Xamarin.Essentials;
using static ReduxSimple.Reducers;

namespace LyraWallet.States.Holding
{
    public static class Reducers
    {
        public static IEnumerable<On<HoldingState>> CreateReducers()
        {
            return new List<On<HoldingState>>
            {
                On<OpenWallet, HoldingState>(
                    (state, action) =>
                    {
                        var wallet = new Wallet(null, action.database, action.network);
                        wallet.AccountName = action.walletName;
                        wallet.OpenAccount(action.walletPath, wallet.AccountName);
                       
                        return state.With(new {
                            lyraWallet = wallet,
                            apiUrl = action.apiUrl
                        });
                    }
                )
            };
        }
    }
}
