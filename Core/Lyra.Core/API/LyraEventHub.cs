using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.API;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.Crypto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using System.Web;

namespace Lyra.Core.API
{
    // inherit Hub<T>, where T is your interface defining the messages
    // client call this
    public class LyraEventHub : Hub<ILyraEvent>, IHubInvokeMethods
    {
        private readonly IHubContext<LyraEventHub> _hubContext;

        public LyraEventHub(IHubContext<LyraEventHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                //var qs = Context.GetHttpContext().Request.QueryString;
                //var parsed = HttpUtility.ParseQueryString(qs.Value);
                //var account = parsed["a"];
                //var id = parsed["id"];
                //var sign = parsed["sign"];
                //if (Signatures.VerifyAccountSignature(account, account, sign))
                //    await Groups.AddToGroupAsync(Context.ConnectionId, account);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnConnectedAsync: {ex}");
            }
            //File.AppendAllText("c:\\tmp\\connectionids.txt", $"AddToGroupAsync: {id}, {account}\n");

            //File.AppendAllText("c:\\tmp\\connectionids.txt", $"OnConnectedAsync: {Context.ConnectionId}\n");
            await base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            return base.OnDisconnectedAsync(exception);
        }

        public Task Register(EventRegisterReq req)
        {
            throw new NotImplementedException();
        }
    }
}
