using GrpcClient;
using Lyra.Core.Decentralize;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Lyra.Node2.Services
{
    public class PBFTNetwork : IPBFTNet
    {
        public event EventHandler<SourceSignedMessage> OnMessage;

        MessageProcessor _local;

        Dictionary<string, ConsensusClient> _remoteNodes;

        public PBFTNetwork(MessageProcessor messageProcessor)
        {
            _local = messageProcessor;
            _remoteNodes = new Dictionary<string, ConsensusClient>();

            // _local.NewMsg += (msg) => OnMessage?(msg);
        }

        public void BroadCastMessage(SourceSignedMessage msg)
        {
            foreach(var client in _remoteNodes.Values)
            {
                // client.Send(msg);
            }
        }

        public void AddPosNode(PosNode node)
        {
            var client = new ConsensusClient();
            _remoteNodes.Add(node.AccountID, client);
            // do it
        }

        public void RemovePosNode(PosNode node)
        {
            var client = _remoteNodes[node.AccountID];
            // client.Close();
            _remoteNodes.Remove(node.AccountID);
        }
    }
}
