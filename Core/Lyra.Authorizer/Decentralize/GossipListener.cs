using Lyra.Authorizer.Authorizers;
using Lyra.Authorizer.Services;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;
using Lyra.Core.Cryptography;
using Orleans;
using Orleans.Streams;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Authorizer.Decentralize
{
    // listen to gossip messages and activate the necessary grains to do works.
    public class GossipListener
    {
        protected IClusterClient _client;
        ServiceAccount _serviceAccount;
        private IAsyncStream<SourceSignedMessage> _gossipStream;
        string Identity;

        Dictionary<long, AuthorizingMsg> _pendingAuthMsg;
        Dictionary<long, List<(string from, long uIndex, bool authResult, AuthorizedMsg msg)>> _activeConsensus;

        Dictionary<BlockTypes, string> _authorizers;

        public GossipListener(IClusterClient clusterClient, ServiceAccount serviceAccount)
        {
            _client = clusterClient;
            _serviceAccount = serviceAccount;

            _pendingAuthMsg = new Dictionary<long, AuthorizingMsg>();
            _activeConsensus = new Dictionary<long, List<(string from, long uIndex, bool authResult, AuthorizedMsg msg)>>();

            _authorizers = new Dictionary<BlockTypes, string>();
            _authorizers.Add(BlockTypes.SendTransfer, "SendTransferAuthorizer");
            _authorizers.Add(BlockTypes.LyraTokenGenesis, "GenesisAuthorizer");
            _authorizers.Add(BlockTypes.OpenAccountWithReceiveFee, "NewAccountAuthorizer");
            _authorizers.Add(BlockTypes.OpenAccountWithReceiveTransfer, "NewAccountAuthorizer");
            _authorizers.Add(BlockTypes.OpenAccountWithImport, "NewAccountWithImportAuthorizer");
            _authorizers.Add(BlockTypes.ReceiveTransfer, "ReceiveTransferAuthorizer");
            _authorizers.Add(BlockTypes.ImportAccount, "ImportAccountAuthorizer");
            _authorizers.Add(BlockTypes.TokenGenesis, "NewTokenAuthorizer");
        }

        public async Task Init(string IdentityString)
        {
            Identity = IdentityString;

            _gossipStream = _client.GetStreamProvider(LyraGossipConstants.LyraGossipStreamProvider)
                .GetStream<SourceSignedMessage>(Guid.Parse(LyraGossipConstants.LyraGossipStreamId), LyraGossipConstants.LyraGossipStreamNameSpace);
            await _gossipStream.SubscribeAsync(OnNextAsync, OnErrorAsync, OnCompletedAsync);

//            await SendMessage(new ChatMsg { From = _serviceAccount.AccountId, Text = "account id goes here", Type = ChatMessageType.NodeUp });
        }

        public virtual async Task SendMessage(SourceSignedMessage msg)
        {
            while (_serviceAccount.PrivateKey == null)  //starup. need to wait it generated
                await Task.Delay(1000);
            msg.Sign(_serviceAccount.PrivateKey, msg.From);
            msg.VerifySignature(_serviceAccount.AccountId);
            await _gossipStream.OnNextAsync(msg);
        }

        //public async Task SendMessage(SourceSignedMessage msg, TransactionBlock[] block)
        //{
        //    var 
        //    _pendingAuthMsg.Add(block.UIndex, block);
        //    await SendMessage(msg);
        //}

        public Task OnNextAsync(SourceSignedMessage item, StreamSequenceToken token = null)
        {
            return OnNextAsyncImpl(item);
        }
        async Task OnNextAsyncImpl(SourceSignedMessage item)
        {
            // verify the signatures of msg. make sure it is from the right node.
            //var nodeConfig = null;
            if (!item.VerifySignature(item.From))
            {
                // log failed verify
                return;
            }

            switch(item)
            {
                case AuthorizingMsg msg:
                    OnPrePrepare(msg);
                    break;
                case AuthorizedMsg authed:
                    await OnPrepare(authed);
                    break;
                case AuthorizerCommitMsg commited:
                    await OnCommit(commited);
                    break;
                //case ChatMsg chat:
                //    await OnChatMsg(chat);
                //    break;
                default:
                    // log msg unknown
                    break;
            }
        }

        //private async Task OnChatMsg(ChatMsg chat)
        //{

        //}

        private void OnPrePrepare(AuthorizingMsg item)
        {
            // send to self node to auth
            _pendingAuthMsg.Add(item.Blocks.First().Key, item);

            _ = Task.Run(async () =>
            {
                var resultMsg = new AuthorizedMsg
                {
                    From = _serviceAccount.AccountId,
                    AuthResults = new SortedList<long, AuthorizedMsg.AuthSignForBlock>()
                };

                foreach (var b in item.Blocks)
                {
                    var authrName = _authorizers[b.Value.BlockType];
                    var authorizer = _client.GetGrain<IAuthorizer>(Guid.NewGuid(), "Lyra.Authorizer.Authorizers." + authrName);

                    var localAuthResult = await authorizer.Authorize(b.Value);
                    var result = new AuthorizedMsg.AuthSignForBlock
                    {
                        Result = localAuthResult,
                        AuthSign = b.Value.Authorizations?.First()
                    };
                    resultMsg.AuthResults.Add(b.Value.UIndex, result);
                }

                await SendMessage(resultMsg);
            });
        }

        private async Task OnPrepare(AuthorizedMsg item)
        {
            List<(string from, long uIndex, bool authResult, AuthorizedMsg msg)> authResults;
            var uindex = item.AuthResults.First().Key;
            if (_activeConsensus.ContainsKey(uindex))
            {
                authResults = _activeConsensus[uindex];
            }
            else
            {
                authResults = new List<(string from, long uIndex, bool authResult, AuthorizedMsg msg)>();
                _activeConsensus.Add(uindex, authResults);
            }
            authResults.Add((item.From, uindex, item.AuthResults.Values.Any(a => a.Result != APIResultCodes.Success), item));

            if (authResults.Where(a => a.uIndex == uindex)
                .Count(b => b.authResult) > 1)  //need to get from global config
            {
                // do commit
                var commiter = _client.GetGrain<IAuthorizer>(Guid.NewGuid(), "Lyra.Authorizer.Authorizers.AuthorizedCommiter");

                var authMsg = _pendingAuthMsg[uindex];
                foreach(var block in authMsg.Blocks)
                {
                    block.Value.Authorizations = authResults.Where(a => a.uIndex == uindex)
                        .Select(a => a.msg.AuthResults[block.Key].AuthSign).ToList();
                    await commiter.Commit(block.Value);
                }

                var msg = new AuthorizerCommitMsg
                {
                    From = _serviceAccount.AccountId,
                    Commited = new SortedList<long, bool>()
                };
                authMsg.Blocks.Select(a => { msg.Commited.Add(a.Key, true); return true; });
                await SendMessage(msg);
            }
        }

        private async Task OnCommit(AuthorizerCommitMsg msg)
        {
            // reply to client
        }

        public virtual Task OnCompletedAsync()
        {
            Console.WriteLine("Chatroom message stream received stream completed event");
            return Task.CompletedTask;
        }

        public virtual Task OnErrorAsync(Exception ex)
        {
            Console.WriteLine($"Chatroom is experiencing message delivery failure, ex :{ex}");
            return Task.CompletedTask;
        }
    }
}
