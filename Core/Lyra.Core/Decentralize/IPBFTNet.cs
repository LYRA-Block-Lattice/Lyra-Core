using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Decentralize
{
    public interface IPBFTNet
    {
        void BroadCastMessage(SourceSignedMessage msg);
        event EventHandler<SourceSignedMessage> OnMessage;
        void AddPosNode(PosNode node);
        void RemovePosNode(PosNode node);
    }
}
