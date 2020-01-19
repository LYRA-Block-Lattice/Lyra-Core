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

        readonly Dictionary<string, PosNode> _targetNodes = new Dictionary<string, PosNode>();
        readonly Dictionary<string, ConsensusClient> _remoteNodes = new Dictionary<string, ConsensusClient>();

        public PBFTNetwork(DuplexService duplexService)
        {
            _local = duplexService;
        }

        public async Task BroadCastMessageAsync(SourceSignedMessage msg)
        {
            await _local.BroadcastAsync(msg.MsgType.ToString(), msg.ToArray());
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
                    client.SendMessage("ping");
                    return;
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
                switch (msg.MessageId)       //AuthorizerPrePrepare, AuthorizerPrepare, AuthorizerCommit, BlockConsolidation
                {
                    case "AuthorizerPrePrepare":
                        OnMessage(this, msg.Payload.ToArray().AsSerializable<AuthorizingMsg>());
                        break;
                    case "AuthorizerPrepare":
                        OnMessage(this, msg.Payload.ToArray().AsSerializable<AuthorizedMsg>());
                        break;
                    case "AuthorizerCommit":
                        OnMessage(this, msg.Payload.ToArray().AsSerializable<AuthorizerCommitMsg>());
                        break;
                    default:
                        Console.WriteLine("unknown message from pbft node");
                        break;
                }
            };

            client.OnShutdown += (o, a) =>
            {
                _remoteNodes.Remove(a.accountId);
                Task.Run(() => { CreateClientFor(a.accountId, a.ip); });
            };

            try
            {
                client.Start(IP, accoundId);
                client.SendMessage("ping");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
