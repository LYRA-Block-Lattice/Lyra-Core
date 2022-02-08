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
        public ConsensusResult? Consensus { get; set; }
        public BlockAPIResult BlockAPIResult { get; set; } = null!;
    }

    public class WorkflowEvent
    {
        public string Owner { get; set; }   // request account ID
        public string Name { get; set; }        // = svcreq tag
        public string Key { get; set; }         // request sendblock's hash
        public string State { get; set; }   
        public string Action { get; set; }
        public string Result { get; set; }
        public string Message { get; set; }
    }

    public enum EventTypes { Null, Consensus, Workflow }

    public class EventContainer
    {
        public EventTypes EvtType { get; set; }
        public string Json { get; set; } = null!;

        public EventContainer()
        {

        }
        public EventContainer(ConsensusEvent ce)
        {
            EvtType = EventTypes.Consensus;
            Json = JsonConvert.SerializeObject(ce);
        }

        public EventContainer(WorkflowEvent wf)
        {
            EvtType = EventTypes.Workflow;
            Json = JsonConvert.SerializeObject(wf);
        }

        public object? Get()
        {
            return EvtType switch
            {
                EventTypes.Null => null,
                EventTypes.Consensus => JsonConvert.DeserializeObject<ConsensusEvent>(Json),
                EventTypes.Workflow => JsonConvert.DeserializeObject<WorkflowEvent>(Json),
            };
        }
    }

    public class EventRegisterReq
    {

    }

    /// <summary> SignalR Hub push interface (signature for Hub pushing notifications to Clients) </summary>
    public interface ILyraEvent
    {
        Task OnEvent(EventContainer evt);
    }

    /// <summary> SignalR Hub invoke interface (signature for Clients invoking methods on server Hub) </summary>
    public interface ILyraEventRegister
    {
        //Task SendFile(FileMessage fm);
        //Task<JoinRoomResponse> JoinRoom(JoinRoomRequest req);
        Task Register(EventRegisterReq req);
    }
}
