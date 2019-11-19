using Lyra.Core.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Lyra.Node2.Services
{
    public class NotifyService : INotifyAPI
    {
        private class NotifyClient
        {
            public string AccountID { get; set; }
            public NotifySource Source { get; set; }
            public string Catalog { get; set; }
            public string ExtInfo { get; set; }
            public TaskCompletionSource<bool> tcs { get; set; }
        }

        private static readonly Dictionary<string, NotifyClient> _peers = new Dictionary<string, NotifyClient>();

        public static void Notify(string AccountID, NotifySource Source, string catalog, string extraInfo)
        {
            if(_peers.ContainsKey(AccountID))
            {
                var nc = _peers[AccountID];
                nc.Source = Source;
                nc.Catalog = catalog;
                nc.ExtInfo = extraInfo;
                nc.tcs.TrySetResult(true);
            }
        }
        public async Task<GetNotificationAPIResult> GetNotification(string AccountID, string Signature)
        {
            //TODO verify signature here
            NotifyClient nc;
            if (_peers.ContainsKey(AccountID))
            {
                nc = _peers[AccountID];
            }
            else
            {
                nc = new NotifyClient()
                {
                    AccountID = AccountID                    
                };
                _peers.Add(AccountID, nc);
            }
            nc.Source = NotifySource.None;
            nc.ExtInfo = string.Empty;
            nc.tcs = new TaskCompletionSource<bool>(false);

            GetNotificationAPIResult result;
            try
            {
                await Task.WhenAny(nc.tcs.Task, Task.Delay(5 * 60 * 1000));
                if (nc.tcs.Task.IsCompleted)
                {
                    // has notify
                    result = new GetNotificationAPIResult()
                    {
                        ResultCode = Core.Protos.APIResultCodes.Success,
                        HasEvent = true,
                        Catalog = nc.Catalog,
                        ExtraInfo = nc.ExtInfo,
                        Source = nc.Source
                    };
                }
                else
                {
                    // no notify, just timeout
                    result = new GetNotificationAPIResult()
                    {
                        ResultCode = Core.Protos.APIResultCodes.Success,
                        HasEvent = false,
                        Source = NotifySource.None
                    };
                }
            }
            catch(Exception e)
            {
                // network timeout etc.
                result = new GetNotificationAPIResult()
                {
                    ResultCode = Core.Protos.APIResultCodes.UnknownError,
                    HasEvent = false,
                    Source = NotifySource.None
                };
            }
            finally
            {
                if(_peers.ContainsKey(AccountID))
                    _peers.Remove(AccountID);
            }
            return result;
        }
    }
}
