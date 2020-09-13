using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using ReduxSimple;
using System;
using System.Collections.Generic;
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
                        });
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
                        });
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
                        });
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
                            await action.wallet.Sync(null);
                            return action.wallet;
                        });
                    })
                    .Switch()
                    .Select(result =>
                    {
                        return new WalletTransactionResultAction
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

        public static Effect<RootState> SendTokenWalletEffect = ReduxSimple.Effects.CreateEffect<RootState>
            (
                () => App.Store.ObserveAction<WalletSendTokenAction>()
                    .Select(action =>
                    {
                        return Observable.FromAsync(async () =>
                        {
                            await action.wallet.Sync(null);

                            await action.wallet.Send(action.Amount, action.DstAddr, action.TokenName);

                            return action.wallet;
                        });
                    })
                    .Switch()
                    .Select(result =>
                    {
                        return new WalletTransactionResultAction
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

        public static Effect<RootState> CreateTokenWalletEffect = ReduxSimple.Effects.CreateEffect<RootState>
            (
                () => App.Store.ObserveAction<WalletCreateTokenAction>()
                    .Select(action =>
                    {
                        return Observable.FromAsync(async () =>
                        {
                            await action.wallet.Sync(null);

                            await action.wallet.CreateToken(action.tokenName, action.tokenDomain ?? "", action.description ?? "", Convert.ToSByte(action.precision), action.totalSupply,
                                        true, action.ownerName ?? "", action.ownerAddress ?? "", null, ContractTypes.Default, null);

                            return action.wallet;
                        });
                    })
                    .Switch()
                    .Select(result =>
                    {
                        return new WalletTransactionResultAction
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
    }
}
