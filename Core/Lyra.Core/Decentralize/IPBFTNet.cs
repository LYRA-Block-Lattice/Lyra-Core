using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize
{
    public enum MeshNetworkConnecStatus { Unknown, Unreachable, OutBoundOnly, InBoundOnly, FulllyConnected, Disconnected, Stalled }
    public interface IPBFTNet
    {
        void BroadCastMessage(SourceSignedMessage msg);
        void RegisterMessageHandler(Func<SourceSignedMessage, Task> OnMessage);

        void PingNode(PosNode node);
        void AddPosNode(PosNode node);
        void RemovePosNode(PosNode node);

        MeshNetworkConnecStatus GetNodeMeshNetworkStatus(string clientId);
    }
}
