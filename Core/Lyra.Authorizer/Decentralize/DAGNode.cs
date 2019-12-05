using Lyra.Core.API;
using Lyra.Core.Protos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Lyra.Authorizer.Decentralize
{
    //public class DAGNode : Orleans.Grain, INodeAPI
    //{
    //    private INodeAPI _apiSvc;

    //    public DAGNode(INodeAPI node)
    //    {
    //        _apiSvc = node;
    //    }

    //    public Task<BlockAPIResult> GetLastServiceBlock(string AccountId, string Signature)
    //    {
    //        return _apiSvc.GetLastServiceBlock(AccountId, Signature);
    //    }

    //    public Task<AccountHeightAPIResult> GetSyncHeight()
    //    {
    //        return _apiSvc.GetSyncHeight();
    //    }

    //    public Task<GetVersionAPIResult> GetVersion(int apiVersion, string appName, string appVersion)
    //    {
    //        var result = new GetVersionAPIResult()
    //        {
    //            ResultCode = APIResultCodes.Success,
    //            ApiVersion = LyraGlobal.APIVERSION,
    //            NodeVersion = LyraGlobal.NodeVersion,
    //            UpgradeNeeded = apiVersion < LyraGlobal.APIVERSION,
    //            MustUpgradeToConnect = apiVersion < LyraGlobal.APIVERSION
    //        };
    //        return Task.FromResult(result);
    //    }
    //}
}
