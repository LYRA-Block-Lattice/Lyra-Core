using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize
{
    public interface IPBFTNet
    {
        Task BroadCastMessageAsync(Neo.IO.ISerializable msg);
        event EventHandler<SourceSignedMessage> OnMessage;

        void PingNode(PosNode node);
        void AddPosNode(PosNode node);
        void RemovePosNode(PosNode node);
    }
}
