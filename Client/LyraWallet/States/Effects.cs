using Lyra.Client.Lib;
using Lyra.Core.API;
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
        private static DAGClientHostedService _dagClient;

        public static Effect<RootState> GetApiCompatibleInfo = ReduxSimple.Effects.CreateEffect<RootState>
            (
                () => App.Store.ObserveAction<GetApiVersionAction>()
                    .Select(vers => 
                    {
                        return Observable.FromAsync(async () => await Task.FromResult(new GetVersionAPIResult()));
                        //_dagClient = new LyraRestClient(vers.Platform, vers.AppName, vers.AppVersion, LyraGlobal.SelectNode(vers.Network).restUrl);
                        //return Observable.FromAsync(async () => await _apiClient.GetVersion(LyraGlobal.APIVERSION, vers.AppName, vers.AppVersion));
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
