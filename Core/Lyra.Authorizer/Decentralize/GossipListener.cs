using Lyra.Authorizer.Authorizers;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;
using Lyra.Core.Cryptography;
using Orleans;
using Orleans.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Authorizer.Decentralize
{
    // listen to gossip messages and activate the necessary grains to do works.
    public class GossipListener
    {
        protected IClusterClient _client;
        protected ISignatures _signr;
        private IAsyncStream<ChatMsg> _gossipStream;
        string Identity;

        Dictionary<long, TransactionBlock> _pendingBlocks;
        Dictionary<long, List<(string nodeTag, ChatMsg msg)>> _activeConsensus;

        Dictionary<BlockTypes, string> _authorizers;

        public GossipListener(IClusterClient clusterClient)
        {
            _client = clusterClient;
            _signr = _client.GetGrain<ISignaturesForGrain>(0);

            _pendingBlocks = new Dictionary<long, TransactionBlock>();
            _activeConsensus = new Dictionary<long, List<(string nodeTag, ChatMsg msg)>>();

            _authorizers = new Dictionary<BlockTypes, string>();
            _authorizers.Add(BlockTypes.SendTransfer, "SendTransferAuthorizer");
            _authorizers.Add(BlockTypes.LyraTokenGenesis, "GenesisAuthorizer");
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
                .GetStream<ChatMsg>(Guid.Parse(LyraGossipConstants.LyraGossipStreamId), LyraGossipConstants.LyraGossipStreamNameSpace);
            await _gossipStream.SubscribeAsync(OnNextAsync, OnErrorAsync, OnCompletedAsync);

            await SendMessage(new ChatMsg { From = IdentityString, Text = "Service Online", Type = ChatMessageType.NodeEvent });
        }

        public virtual async Task SendMessage(ChatMsg msg)
        {
            await _gossipStream.OnNextAsync(msg);
        }

        public Task SendMessage(ChatMsg msg, TransactionBlock block)
        {
            _pendingBlocks.Add(block.UIndex, block);
            return SendMessage(msg);
        }

        public Task OnNextAsync(ChatMsg item, StreamSequenceToken token = null)
        {
            return OnNextAsyncImpl(item);
        }
        async Task OnNextAsyncImpl(ChatMsg item)
        {
            // verify the signatures of msg. make sure it is from the right node.
            //var nodeConfig = null;
            if (!await item.VerifySignatureAsync(_signr, ""))
            {
                // log failed verify
                //return;
            }

            switch(item.Type)
            {
                case ChatMessageType.AuthorizerPrePrepare:
                    await OnPrePrepare(item);
                    break;
                case ChatMessageType.AuthorizerPrepare:
                    await OnPrepare(item);
                    break;
                case ChatMessageType.AuthorizerCommit:
                    await OnCommit(item);
                    break;
                case ChatMessageType.SeedElection:
                    //ConsensusRuntimeConfig. = item.Text;
                    break;
                default:
                    // log msg unknown
                    break;
            }
        }

        private async Task OnPrePrepare(ChatMsg item)
        {
            // send to self node to auth
            _pendingBlocks.Add(item.BlockUIndex, item.BlockToAuth);
            var authrName = _authorizers[item.BlockToAuth.BlockType];
            var authorizer = _client.GetGrain<IAuthorizer>(Guid.NewGuid(), "Lyra.Authorizer.Authorizers." + authrName);

            _ = Task.Run(async () =>
            {
                var localAuthResult = await authorizer.Authorize(item.BlockToAuth);
                var msg = new ChatMsg
                {
                    Type = ChatMessageType.AuthorizerPrepare,
                    From = Identity,
                    BlockUIndex = item.BlockUIndex,
                    AuthResult = localAuthResult,
                    AuthSignature = item.BlockToAuth.Authorizations.First()
                };
                await SendMessage(msg);
            });
        }

        private async Task OnPrepare(ChatMsg item)
        {
            List<(string nodeTag, ChatMsg msg)> authResults;
            if (_activeConsensus.ContainsKey(item.BlockUIndex))
            {
                authResults = _activeConsensus[item.BlockUIndex];
            }
            else
            {
                authResults = new List<(string nodeTag, ChatMsg msg)>();
                _activeConsensus.Add(item.BlockUIndex, authResults);
            }
            authResults.Add((item.From, item));

            if (authResults.Count(a => a.msg.AuthResult == APIResultCodes.Success) > 1)  //need to get from global config
            {
                // do commit
                var commiter = _client.GetGrain<IAuthorizer>(Guid.NewGuid(), "Lyra.Authorizer.Authorizers.AuthorizedCommiter");

                var block = _pendingBlocks[item.BlockUIndex];
                block.Authorizations = authResults.Select(a => a.msg.AuthSignature).ToList();
                await commiter.Commit(block);

                var msg = new ChatMsg
                {
                    BlockUIndex = item.BlockUIndex,
                    AuthResult = APIResultCodes.Success,
                    Type = ChatMessageType.AuthorizerCommit,
                    From = Identity
                };
                await SendMessage(msg);
            }
        }

        private async Task OnCommit(ChatMsg msg)
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
