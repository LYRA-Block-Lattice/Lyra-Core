using GrpcClient;
using Lyra.Core.Decentralize;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lyra.Shared;
using Communication;
using Newtonsoft.Json;
using Neo.IO;

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

        public async Task BroadCastMessageAsync(Neo.IO.ISerializable msg)
        {
            await _local.BroadcastAsync(msg.ToArray());
        }

        public void AddPosNode(PosNode node)
        {
            if (_remoteNodes.ContainsKey(node.AccountID))
                return;

            CreateClientFor(node);
        }

        public void RemovePosNode(PosNode node)
        {
            var client = _remoteNodes[node.AccountID];
            // client.Close();
            _remoteNodes.Remove(node.AccountID);
        }

        public void PingNode(PosNode node)
        {
            if (_remoteNodes.ContainsKey(node.AccountID))
            {
                var client = _remoteNodes[node.AccountID];
                if(client == null)
                {
                    _remoteNodes.Remove(node.AccountID);

                    CreateClientFor(node);
                }
                else
                {
                    client.SendMessage("ping");
                    return;
                }
            }
            else
            {
                // recreate it
                CreateClientFor(node);
            }
        }

        private void CreateClientFor(PosNode node)
        {
            var client = new ConsensusClient();
            _remoteNodes.Add(node.AccountID, client);

            // do it
            client.OnMessage += (o, msg) =>
            {
                if(msg.MessageId == null)
                    OnMessage(this, msg.Payload.ToArray().AsSerializable<SourceSignedMessage>());
            };

            client.OnShutdown += (o, a) =>
            {
                _remoteNodes.Remove(a);
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
    }
}
