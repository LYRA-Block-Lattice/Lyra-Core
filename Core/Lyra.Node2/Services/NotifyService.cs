﻿using Lyra.Core.API;
using Lyra.Core.Blocks;
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
            public string Action { get; set; }
            public string Catalog { get; set; }
            public string ExtInfo { get; set; }
            public TaskCompletionSource<bool> tcs { get; set; }
        }

        private static readonly Dictionary<string, NotifyClient> _peers = new Dictionary<string, NotifyClient>();
        private static NotifyClient _allClient = new NotifyClient();

        public static void Notify(string AccountID, NotifySource Source, string action, string catalog, string extraInfo)
        {
            if(string.IsNullOrEmpty(AccountID))
            {
                // broadcast to every connected client
                foreach(var nc in _peers.Values.ToList())
                {
                    nc.Source = Source;
                    nc.Action = action;
                    nc.Catalog = catalog;
                    nc.ExtInfo = extraInfo;
                    nc.tcs.TrySetResult(true);
                }
            }
            else if(_peers.ContainsKey(AccountID))
            {
                var nc = _peers[AccountID];
                nc.Source = Source;
                nc.Action = action;
                nc.Catalog = catalog;
                nc.ExtInfo = extraInfo;
                nc.tcs.TrySetResult(true);
            }
        }
        public async Task<GetNotificationAPIResult> GetNotificationAsync(string AccountID, string Signature)
        {
            //verify signature here
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
                        ResultCode = APIResultCodes.Success,
                        HasEvent = true,
                        Action = nc.Action,
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
                        ResultCode = APIResultCodes.Success,
                        HasEvent = false,
                        Source = NotifySource.None
                    };
                }
            }
            catch(Exception)
            {
                // network timeout etc.
                result = new GetNotificationAPIResult()
                {
                    ResultCode = APIResultCodes.UnknownError,
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
