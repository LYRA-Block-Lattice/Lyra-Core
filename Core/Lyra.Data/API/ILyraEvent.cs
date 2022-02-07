using Lyra.Core.API;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API
{
    public enum ConsensusResult { Uncertain, Yea, Nay }

    // Define the hub methods
    public class ConsensusEvent
    {
        public ConsensusResult Result { get; set; }
        public BlockAPIResult BlockAPIResult { get; set; } = null!;
    }

    public class EventRegisterReq
    {

    }

    /// <summary> SignalR Hub push interface (signature for Hub pushing notifications to Clients) </summary>
    public interface ILyraEvent
    {
        Task OnConsensus(ConsensusEvent evt);
    }

    /// <summary> SignalR Hub invoke interface (signature for Clients invoking methods on server Hub) </summary>
    public interface IHubInvokeMethods
    {
        //Task SendFile(FileMessage fm);
        //Task<JoinRoomResponse> JoinRoom(JoinRoomRequest req);
        Task Register(EventRegisterReq req);
    }
}
