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
using Microsoft.Extensions.Logging;

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

        ILogger<PBFTNetwork> _log;
        public PBFTNetwork(MessageProcessor messageProcessor, ILogger<PBFTNetwork> logger)
        {
            _log = logger;
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
                            //_log.LogInformation("unknown message from pbft node");
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
                    _log.LogInformation($"Error sending to pbft node {client.Ip}: {e.Message}");
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
                _log.LogInformation($"In AddPosNode: {e.Message}");
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
                    _log.LogInformation($"In RemovePosNode: {e.Message}");
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
                client.Start(IP, accoundId, NodeService.Instance.PosWallet.AccountId);
                client.SendMessage("ping", Encoding.ASCII.GetBytes("ping"));
            }
            catch (Exception ex)
            {
                _log.LogInformation(ex.Message);
            }
        }

        public void RegisterMessageHandler(Func<SourceSignedMessage, Task> onMessage)
        {
            OnMessage = onMessage;
        }

        public MeshNetworkConnecStatus GetNodeMeshNetworkStatus(string clientId)
        {
            if (_remoteNodes.ContainsKey(clientId) && _remoteNodes[clientId].Connected)
            {
                if (_clientActivityTime.ContainsKey(clientId))
                {
                    if (DateTime.Now - _clientActivityTime[clientId] < TimeSpan.FromSeconds(90))
                        return MeshNetworkConnecStatus.FulllyConnected;
                    else
                        return MeshNetworkConnecStatus.Stalled;
                }
                else
                    return MeshNetworkConnecStatus.InBoundOnly;                    
            }
            else if (_remoteNodes.ContainsKey(clientId) && !_remoteNodes[clientId].Connected)
            {
                if (_clientActivityTime.ContainsKey(clientId))
                {
                    if (DateTime.Now - _clientActivityTime[clientId] < TimeSpan.FromSeconds(90))
                        return MeshNetworkConnecStatus.OutBoundOnly;
                    else
                        return MeshNetworkConnecStatus.Disconnected;
                }
                else
                    return MeshNetworkConnecStatus.Unreachable;
            }

            return MeshNetworkConnecStatus.Unknown;
        }
    }
}
