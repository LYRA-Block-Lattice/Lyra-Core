using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize
{
    public interface IPBFTNet
    {
        Task BroadCastMessageAsync(SourceSignedMessage msg);
        event EventHandler<SourceSignedMessage> OnMessage;
        void AddPosNode(PosNode node);
        void RemovePosNode(PosNode node);
    }
}
