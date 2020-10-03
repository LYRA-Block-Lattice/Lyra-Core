using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using ReduxSimple;
using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LyraWallet.States
{
    public static class Effects
    {
        public static Effect<RootState> CreateWalletEffect = ReduxSimple.Effects.CreateEffect<RootState>
            (
                () => App.Store.ObserveAction<WalletCreateAction>()
                    .Select(action => 
                    {
                        return Observable.FromAsync(async () => { 
                            var store = new SecuredWalletStore(action.path);
                            Wallet.Create(store, action.name, action.password, action.network);

                            var wallet = Wallet.Open(store, action.name, action.password);
                            var client = LyraRestClient.Create(action.network, Environment.OSVersion.ToString(), "Mobile Wallet", "1.0");
                            await wallet.Sync(client);

                            return wallet;
                        }).ObserveOn(Scheduler.Default);
                    })
                    .Switch()
                    .Select(result =>
                    {
                        return new WalletOpenResultAction
                        {
                            wallet = result
                        };
                    })
                    .Catch<object, Exception>(e =>
                    {
                        return Observable.Return(new WalletErrorAction
                        {
                            Error = e
                        });
                    }),
                true
            );

        public static Effect<RootState> OpenWalletEffect = ReduxSimple.Effects.CreateEffect<RootState>
            (
                () => App.Store.ObserveAction<WalletOpenAction>()
                    .Select(action =>
                    {
                        return Observable.FromAsync(async () => {
                            var store = new SecuredWalletStore(action.path);
                            var wallet = Wallet.Open(store, action.name, action.password);
                            var client = LyraRestClient.Create(wallet.NetworkId, Environment.OSVersion.ToString(), "Mobile Wallet", "1.0");
                            await wallet.Sync(client);

                            return wallet;
                        }).ObserveOn(Scheduler.Default);
                    })
                    .Switch()
                    .Select(result =>
                    {
                        return new WalletOpenResultAction
                        {
                            wallet = result
                        };
                    })
                    .Catch<object, Exception>(e =>
                    {
                        return Observable.Return(new WalletErrorAction
                        {
                            Error = e
                        });
                    }),
                true
            );

        public static Effect<RootState> RestoreWalletEffect = ReduxSimple.Effects.CreateEffect<RootState>
            (
                () => App.Store.ObserveAction<WalletRestoreAction>()
                    .Select(action =>
                    {
                        return Observable.FromAsync(async () => {
                            var store = new SecuredWalletStore(action.path);
                            Wallet.Create(store, action.name, action.password, action.network, action.privateKey);

                            var wallet = Wallet.Open(store, action.name, action.password);
                            var client = LyraRestClient.Create(action.network, Environment.OSVersion.ToString(), "Mobile Wallet", "1.0");
                            await wallet.Sync(client);

                            return wallet;
                        }).ObserveOn(Scheduler.Default);
                    })
                    .Switch()
                    .Select(result =>
                    {
                        return new WalletOpenResultAction
                        {
                            wallet = result
                        };
                    })
                    .Catch<object, Exception>(e =>
                    {
                        return Observable.Return(new WalletErrorAction
                        {
                            Error = e
                        });
                    }),
                true
            );

        public static Effect<RootState> RemoveWalletEffect = ReduxSimple.Effects.CreateEffect<RootState>
            (
                () => App.Store.ObserveAction<WalletRemoveAction>()
                    .Select(action =>
                    {
                        var store = new SecuredWalletStore(action.path);
                        store.Delete(action.name);
                        return Observable.Empty<Wallet>();
                     })
                    .Switch()
                    .Select(result =>
                    {
                        return new WalletOpenResultAction
                        {
                            wallet = null
                        };
                    })
                    .Catch<object, Exception>(e =>
                    {
                        return Observable.Return(new WalletErrorAction
                        {
                            Error = e
                        });
                    }),
                true
            );


        public static Effect<RootState> ChangeVoteWalletEffect = ReduxSimple.Effects.CreateEffect<RootState>
            (
                () => App.Store.ObserveAction<WalletChangeVoteAction>()
                    .Select(action =>
                    {
                        action.wallet.VoteFor = action.VoteFor;
                        return Observable.Return(action.wallet);
                    })
                    .Switch()
                    .Select(result =>
                    {
                        return new WalletOpenResultAction
                        {
                            wallet = result
                        };
                    })
                    .Catch<object, Exception>(e =>
                    {
                        return Observable.Return(new WalletErrorAction
                        {
                            Error = e
                        });
                    }),
                true
            );

        public static Effect<RootState> RefreshWalletEffect = ReduxSimple.Effects.CreateEffect<RootState>
            (
                () => App.Store.ObserveAction<WalletRefreshBalanceAction>()
                    .Select(action =>
                    {
                        return Observable.FromAsync(async () =>
                        {
                            var ret = await action.wallet.Sync(null);
                            return (action.wallet, new APIResult { ResultCode = ret });
                        }).ObserveOn(Scheduler.Default);
                    })
                    .Switch()
                    .Select(result =>
                    {
                        return new WalletTransactionResultAction
                        {
                            wallet = result.wallet,
                            txName = "Refresh Balance",
                            txResult = result.Item2
                        };
                    })
                    .Catch<object, Exception>(e =>
                    {
                        return Observable.Return(new WalletErrorAction
                        {
                            Error = e
                        });
                    }),
                true
            );

        public static Effect<RootState> SendTokenWalletEffect = ReduxSimple.Effects.CreateEffect<RootState>
            (
                () => App.Store.ObserveAction<WalletSendTokenAction>()
                    .Select(action =>
                    {
                        return Observable.FromAsync(async () =>
                        {
                            await action.wallet.Sync(null);

                            var result = await action.wallet.Send(action.Amount, action.DstAddr, action.TokenName);

                            return (action.wallet, result);
                        }).ObserveOn(Scheduler.Default);
                    })
                    .Switch()
                    .Select(result =>
                    {
                        return new WalletTransactionResultAction
                        {
                            wallet = result.wallet,
                            txName = "Send",
                            txResult = result.result
                        };
                    })
                    .Catch<object, Exception>(e =>
                    {
                        return Observable.Return(new WalletErrorAction
                        {
                            Error = e
                        });
                    }),
                true
            );

        public static Effect<RootState> CreateTokenWalletEffect = ReduxSimple.Effects.CreateEffect<RootState>
            (
                () => App.Store.ObserveAction<WalletCreateTokenAction>()
                    .Select(action =>
                    {
                        return Observable.FromAsync(async () =>
                        {
                            await action.wallet.Sync(null);

                            var result = await action.wallet.CreateToken(action.tokenName, action.tokenDomain ?? "", action.description ?? "", Convert.ToSByte(action.precision), action.totalSupply,
                                        true, action.ownerName ?? "", action.ownerAddress ?? "", null, ContractTypes.Default, null);

                            return (action.wallet, result);
                        }).ObserveOn(Scheduler.Default);
                    })
                    .Switch()
                    .Select(result =>
                    {
                        return new WalletTransactionResultAction
                        {
                            wallet = result.wallet,
                            txName = "Create Token",
                            txResult = result.result
                        };
                    })
                    .Catch<object, Exception>(e =>
                    {
                        return Observable.Return(new WalletErrorAction
                        {
                            Error = e
                        });
                    }),
                true
            );

        public static Effect<RootState> ImportWalletEffect = ReduxSimple.Effects.CreateEffect<RootState>
            (
                () => App.Store.ObserveAction<WalletImportAction>()
                    .Select(action =>
                    {
                        return Observable.FromAsync(async () =>
                        {
                            await action.wallet.Sync(null);

                            var result = await action.wallet.ImportAccount(action.targetPrivateKey);

                            return (action.wallet, result);
                        }).ObserveOn(Scheduler.Default);
                    })
                    .Switch()
                    .Select(result =>
                    {
                        return new WalletTransactionResultAction
                        {
                            wallet = result.wallet,
                            txName = "Import",
                            txResult = result.result
                        };
                    })
                    .Catch<object, Exception>(e =>
                    {
                        return Observable.Return(new WalletErrorAction
                        {
                            Error = e
                        });
                    }),
                true
            );


        public static Effect<RootState> RedeemWalletEffect = ReduxSimple.Effects.CreateEffect<RootState>
            (
                () => App.Store.ObserveAction<WalletRedeemAction>()
                    .Select(action =>
                    {
                        return Observable.FromAsync(async () =>
                        {
                            await action.wallet.Sync(null);

                            var result = await action.wallet.RedeemRewards(action.tokenToRedeem, action.countToRedeem);

                            return (action.wallet, result);
                        }).ObserveOn(Scheduler.Default);
                    })
                    .Switch()
                    .Select(result =>
                    {
                        return new WalletTransactionResultAction
                        {
                            wallet = result.wallet,
                            txName = "Redeem",
                            txResult = result.result
                        };
                    })
                    .Catch<object, Exception>(e =>
                    {
                        return Observable.Return(new WalletErrorAction
                        {
                            Error = e
                        });
                    }),
                true
            );

        public static Effect<RootState> NonFungibleTokenWalletEffect = ReduxSimple.Effects.CreateEffect<RootState>
            (
                () => App.Store.ObserveAction<WalletNonFungibleTokenAction>()
                    .Select(action =>
                    {
                        return Observable.FromAsync(async () =>
                        {
                            var result = await action.wallet.NonFungToStringAsync(action.nfToken);

                            return (action.wallet, result);
                        });
                    })
                    .Switch()
                    .Select(result =>
                    {
                        return new WalletNonFungibleTokenResultAction
                        {
                            wallet = result.wallet,
                            name = result.result.name,
                            denomination = result.result.Denomination,
                            redemptionCode = result.result.Redemption
                        };
                    })
                    .Catch<object, Exception>(e =>
                    {
                        return Observable.Return(new WalletErrorAction
                        {
                            Error = e
                        });
                    }),
                true
            );
    }
}
