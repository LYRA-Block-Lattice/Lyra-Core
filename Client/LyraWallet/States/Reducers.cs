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
                            var lb = action.wallet?.GetLatestBlock();
                            return state.With(new {
                                IsChanged = Guid.NewGuid().ToString(),
                                wallet = action.wallet,
                                NonFungible = lb?.NonFungibleToken,
                                Balances = lb?.Balances?.ToDictionary(k => k.Key, k => k.Value.ToBalanceDecimal()),
                                IsOpening = action.wallet == null ? false : true,
                                InitRefresh = false,
                                ErrorMessage = action.errorMessage
                            });
                        }
                    ),
                    On<WalletOpenAndSyncResultAction, RootState>(
                        (state, action) => {
                            var lb = action.wallet?.GetLatestBlock();
                            return state.With(new {
                                IsChanged = Guid.NewGuid().ToString(),
                                wallet = action.wallet,
                                NonFungible = lb?.NonFungibleToken,
                                Balances = lb?.Balances?.ToDictionary(k => k.Key, k => k.Value.ToBalanceDecimal()),
                                IsOpening = true,
                                InitRefresh = true,
                                ErrorMessage = ""
                            });
                        }
                    ),
                    On<WalletTransactionResultAction, RootState>(
                        (state, action) => {
                            var lb = action.wallet?.GetLatestBlock();
                            return state.With(new {
                                IsChanged = Guid.NewGuid().ToString(),
                                wallet = action.wallet,
                                NonFungible = lb?.NonFungibleToken,
                                Balances = lb?.Balances?.ToDictionary(k => k.Key, k => k.Value.ToBalanceDecimal()),
                                IsOpening = true,
                                InitRefresh = true,
                                LastTransactionName = action.txName,
                                ErrorMessage = action.txResult.ResultCode == Lyra.Core.Blocks.APIResultCodes.Success ? "" : action.txResult.ResultCode.ToString()
                            });
                        }
                    ),
                    On<WalletNonFungibleTokenResultAction, RootState>(
                        (state, action) => {
                            var lb = action.wallet?.GetLatestBlock();
                            return state.With(new {
                                IsChanged = Guid.NewGuid().ToString(),
                                LastTransactionName = "Redemption Code",
                                ErrorMessage = $"{action.name} Discount: {action.denomination.ToString("C")} Redemption Code: {action.redemptionCode}"
                            });
                        }
                    ),
                    On<WalletErrorAction, RootState>(
                        (state, action) => 
                        {
                            return state.With(new {
                                IsChanged = Guid.NewGuid().ToString(),
                                ErrorMessage = action.Error.Message
                            });
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
