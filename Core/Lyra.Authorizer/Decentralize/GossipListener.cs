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
        private string Identity;

        Dictionary<BlockTypes, string> _authorizers;

        // queue bellow
        Dictionary<long, AuthState> _activeConsensus;



        public GossipListener(IClusterClient clusterClient, ServiceAccount serviceAccount)
        {
            _client = clusterClient;
            _serviceAccount = serviceAccount;

            _activeConsensus = new Dictionary<long, AuthState>();

            _authorizers = new Dictionary<BlockTypes, string>();
            _authorizers.Add(BlockTypes.SendTransfer, "SendTransferAuthorizer");
            _authorizers.Add(BlockTypes.LyraTokenGenesis, "GenesisAuthorizer");
            _authorizers.Add(BlockTypes.ReceiveFee, "ReceiveTransferAuthorizer");
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
            await _gossipStream.OnNextAsync(msg);
        }

        public async Task<AuthState> SendAuthorizingMessage(AuthorizingMsg block)
        {
            await SendMessage(block);
            var state = CreateAuthringState(block);
            return state;
        }

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
                    OnPrepare(authed);
                    break;
                case AuthorizerCommitMsg commited:
                    OnCommit(commited);
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

        private AuthState CreateAuthringState(AuthorizingMsg item)
        {
            var ukey = item.Block.UIndex;
            if (_activeConsensus.ContainsKey(ukey))
            {
                return _activeConsensus[ukey];
            }

            var state = new AuthState
            {
                UIndexOfFirstBlock = ukey,
                InputMsg = item,
            };
            _activeConsensus.Add(ukey, state);
            return state;
        }

        private void OnPrePrepare(AuthorizingMsg item)
        {
            var state = CreateAuthringState(item);

            _ = Task.Run(async () =>
            {
                var authrName = _authorizers[item.Block.BlockType];
                var authorizer = _client.GetGrain<IAuthorizer>(Guid.NewGuid(), "Lyra.Authorizer.Authorizers." + authrName);

                var localAuthResult = await authorizer.Authorize(item.Block);
                var result = new AuthorizedMsg
                {
                    From = _serviceAccount.AccountId,
                    BlockIndex = item.Block.UIndex,
                    Result = localAuthResult.Item1,
                    AuthSign = localAuthResult.Item2
                };

                await SendMessage(result);
                state.AddAuthResult(result);
            });
        }

        private void OnPrepare(AuthorizedMsg item)
        {
            var state = _activeConsensus[item.BlockIndex];
            state.AddAuthResult(item);

            if (state.IsAuthoringSuccess)
            {
                _ = Task.Run(async () =>
                {
                    // do commit
                    var block = state.InputMsg.Block;
                    block.Authorizations = state.OutputMsgs.Select(a => a.AuthSign).ToList();

                    var commiter = _client.GetGrain<IAuthorizer>(Guid.NewGuid(), "Lyra.Authorizer.Authorizers.AuthorizedCommiter");
                    await commiter.Commit(block);

                    var msg = new AuthorizerCommitMsg
                    {
                        From = _serviceAccount.AccountId,
                        BlockIndex = block.UIndex,
                        Commited = true
                    };

                    await SendMessage(msg);
                });
            }
        }

        private void OnCommit(AuthorizerCommitMsg item)
        {
            var state = _activeConsensus[item.BlockIndex];
            state.AddCommitedResult(item);
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

        public IDisposable Subscribe(IObserver<AuthorizingMsg> observer)
        {
            throw new NotImplementedException();
        }
    }
}
