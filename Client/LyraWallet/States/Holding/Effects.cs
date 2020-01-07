using Lyra.Core.API;
using ReduxSimple;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;

namespace LyraWallet.States.Holding
{
    public static class Effects
    {
        private static LyraRestClient _apiClient;
        private readonly static LyraRestNotify _apiNotify;

        public static Effect<RootState> GetApiCompatibleInfo = ReduxSimple.Effects.CreateEffect<RootState>
            (
                () => App.Store.ObserveAction<GetApiVersionAction>()
                    .Select(vers => 
                    {
                        _apiClient = new LyraRestClient(vers.Platform, vers.AppName, vers.AppVersion, LyraGlobal.SelectNode(vers.Network));
                        return Observable.FromAsync(async () => await _apiClient.GetVersion(LyraGlobal.ProtocolVersion, vers.AppName, vers.AppVersion));
                    })
                    .Switch()
                    .Select(result =>
                    {
                        return new GetApiVersionSuccessAction
                        {
                            UpgradeNeeded = result.UpgradeNeeded,
                            MustUpgradeToConnect = result.MustUpgradeToConnect
                        };
                    })
                    .Catch<object, Exception>(e =>
                    {
                        return Observable.Return(new GetApiVersionFailedAction
                        {
                            Error = e
                        });
                    }),
                true
            );
    }
}
