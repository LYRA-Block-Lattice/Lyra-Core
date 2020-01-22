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
using System.Text;
using System.Collections.Concurrent;

namespace Lyra.Node2.Services
{
    public class PBFTNetwork : IPBFTNet
    {
        private Func<SourceSignedMessage, Task> OnMessage;

        readonly Dictionary<string, PosNode> _targetNodes = new Dictionary<string, PosNode>();
        readonly Dictionary<string, ConsensusClient> _remoteNodes = new Dictionary<string, ConsensusClient>();

        readonly BlockingCollection<(string clientId, string type, byte[] payload)> _incomingMsgQueue = new BlockingCollection<(string clientId, string type, byte[] payload)>();

        // status
        ConcurrentDictionary<string, DateTime> _clientActivityTime = new ConcurrentDictionary<string, DateTime>();

        MessageProcessor _srvMsgProcessor;
        public PBFTNetwork(MessageProcessor messageProcessor)
        {
            _srvMsgProcessor = messageProcessor;
            _srvMsgProcessor.OnPayload += (o, msg) =>
            {
                _incomingMsgQueue.TryAdd(msg);
                _clientActivityTime.AddOrUpdate(msg.clientId, DateTime.Now, (k, t) => t = DateTime.Now);
            };

            Task.Run(async () => { 
                while(true)
                {
                    var msg = _incomingMsgQueue.Take();

                    switch (msg.type)       //AuthorizerPrePrepare, AuthorizerPrepare, AuthorizerCommit, BlockConsolidation
                    {
                        case "AuthorizerPrePrepare":
                            await OnMessage(msg.payload.AsSerializable<AuthorizingMsg>());
                            break;
                        case "AuthorizerPrepare":
                            await OnMessage(msg.payload.AsSerializable<AuthorizedMsg>());
                            break;
                        case "AuthorizerCommit":
                            await OnMessage(msg.payload.AsSerializable<AuthorizerCommitMsg>());
                            break;
                        default:
                            Console.WriteLine("unknown message from pbft node");
                            break;
                    }
                }
            });
        }

        public void BroadCastMessage(SourceSignedMessage msg)
        {
            //await _local.BroadcastAsync(msg.MsgType.ToString(), msg.ToArray());
            foreach (var client in _remoteNodes.Values)
            {
                try
                {
                    client.SendMessage(msg.MsgType.ToString(), msg.ToArray());
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Sending to other pbft node: {e.Message}");
                }
            }
        }

        public void AddPosNode(PosNode node)
        {
            if (_targetNodes.ContainsKey(node.AccountID))
            {
                var oldNode = _targetNodes[node.AccountID];
                _targetNodes[node.AccountID] = node;
                if (oldNode.IP == node.IP)
                    return;
            }
            else
            {
                _targetNodes.Add(node.AccountID, node);
            }

            if (_remoteNodes.ContainsKey(node.AccountID))
                return;

            try
            {
                CreateClientFor(node.AccountID, node.IP);
            }
            catch (Exception e)
            {
                Console.WriteLine($"In AddPosNode: {e.Message}");
            }
        }

        public void RemovePosNode(PosNode node)
        {
            if (_targetNodes.ContainsKey(node.AccountID))
            {
                _targetNodes.Remove(node.AccountID);

                try
                {
                    var client = _remoteNodes[node.AccountID];
                    client.Close();
                    _remoteNodes.Remove(node.AccountID);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"In RemovePosNode: {e.Message}");
                }
            }
        }

        public void PingNode(PosNode node)
        {
            if (_remoteNodes.ContainsKey(node.AccountID))
            {
                var client = _remoteNodes[node.AccountID];
                if (client == null)
                {
                    _remoteNodes.Remove(node.AccountID);

                    CreateClientFor(node.AccountID, node.IP);
                }
                else
                {
                    client.SendMessage("ping", Encoding.ASCII.GetBytes("ping"));
                }
            }
            else
            {
                // recreate it
                CreateClientFor(node.AccountID, node.IP);
            }
        }

        private void CreateClientFor(string accoundId, string IP)
        {
            var client = new ConsensusClient();
            _remoteNodes.Add(accoundId, client);

            // do it
            client.OnMessage += (o, msg) =>
            {

            };

            client.OnShutdown += (o, a) =>
            {
                if(_remoteNodes.ContainsKey(a.accountId))
                    _remoteNodes.Remove(a.accountId);
                Task.Run(() => { CreateClientFor(a.accountId, a.ip); });
            };

            try
            {
                client.Start(IP, accoundId.Shorten());
                client.SendMessage("ping", Encoding.ASCII.GetBytes("ping"));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public void RegisterMessageHandler(Func<SourceSignedMessage, Task> onMessage)
        {
            OnMessage = onMessage;
        }

        public MeshNetworkConnecStatus GetNodeMeshNetworkStatus(string clientId)
        {
            if (_remoteNodes.ContainsKey(clientId) && _remoteNodes[clientId].Connected 
                && _clientActivityTime.ContainsKey(clientId) && DateTime.Now - _clientActivityTime[clientId] < TimeSpan.FromSeconds(30))
                return MeshNetworkConnecStatus.FulllyConnected;

            if (_remoteNodes.ContainsKey(clientId) && !_remoteNodes[clientId].Connected && !_clientActivityTime.ContainsKey(clientId))
                return MeshNetworkConnecStatus.Unreachable;

            if (_remoteNodes.ContainsKey(clientId) && !_remoteNodes[clientId].Connected && 
                _clientActivityTime.ContainsKey(clientId) && DateTime.Now - _clientActivityTime[clientId] < TimeSpan.FromSeconds(30))
                return MeshNetworkConnecStatus.OutBoundOnly;

            if (_remoteNodes.ContainsKey(clientId) && _remoteNodes[clientId].Connected && !_clientActivityTime.ContainsKey(clientId))
                return MeshNetworkConnecStatus.InBoundOnly;

            if (_remoteNodes.ContainsKey(clientId) && !_remoteNodes[clientId].Connected
                    && _clientActivityTime.ContainsKey(clientId) && DateTime.Now - _clientActivityTime[clientId] > TimeSpan.FromSeconds(30))
                return MeshNetworkConnecStatus.Disconnected;

            return MeshNetworkConnecStatus.Unknown;
        }
    }
}
