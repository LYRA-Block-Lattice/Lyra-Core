using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize
{
    public enum MeshNetworkConnecStatus { Unknown, Unreachable, OutBoundOnly, InBoundOnly, FulllyConnected, Disconnected, Stalled, ViaP2p }
    public interface IPBFTNet
    {
        void BroadCastMessage(SourceSignedMessage msg);
        void RegisterMessageHandler(Func<SourceSignedMessage, Task> OnMessage);

        void PingNode(PosNode node);
        void AddPosNode(PosNode node);
        void RemovePosNode(PosNode node);
        List<PosNode> GetConnections();

        MeshNetworkConnecStatus GetNodeMeshNetworkStatus(string clientId);
    }
}
