using GrpcClient;
using Lyra.Core.Decentralize;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lyra.Shared;
using Communication;
using Newtonsoft.Json;

namespace Lyra.Node2.Services
{
    public class PBFTNetwork : IPBFTNet
    {
        public event EventHandler<SourceSignedMessage> OnMessage;

        DuplexService _local;

        readonly Dictionary<string, ConsensusClient> _remoteNodes = new Dictionary<string, ConsensusClient>();

        public PBFTNetwork(DuplexService duplexService)
        {
            _local = duplexService;
        }

        public async Task BroadCastMessageAsync(SourceSignedMessage msg)
        {
            await _local.BroadcastAsync(JsonConvert.SerializeObject(msg));
        }

        public void AddPosNode(PosNode node)
        {
            if (_remoteNodes.ContainsKey(node.AccountID))
                return;

            var client = new ConsensusClient();
            _remoteNodes.Add(node.AccountID, client);

            // do it
            client.OnMessage += (o, json) =>
            {
                if (json != "pong")
                    OnMessage(this, json.UnJson<SourceSignedMessage>());
            };

            try
            {
                client.Start(node.IP, node.AccountID);
                client.SendMessage("ping");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public void RemovePosNode(PosNode node)
        {
            var client = _remoteNodes[node.AccountID];
            // client.Close();
            _remoteNodes.Remove(node.AccountID);
        }
    }
}
