using Lyra.Authorizer.Authorizers;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;
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
        private IAsyncStream<ChatMsg> _gossipStream;
        string Identity;

        Dictionary<long, TransactionBlock> _pendingBlocks;
        Dictionary<long, List<(string nodeTag, APIResultCodes result)>> _activeConsensus;

        Dictionary<BlockTypes, string> _authorizers;

        public GossipListener(IClusterClient clusterClient)
        {
            _client = clusterClient;

            _pendingBlocks = new Dictionary<long, TransactionBlock>();
            _activeConsensus = new Dictionary<long, List<(string nodeTag, APIResultCodes result)>>();

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

            await SendMessage(new ChatMsg { From = IdentityString, Text = IdentityString + " Up", Type = ChatMessageType.NodeEvent });
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
            if(item.Type == ChatMessageType.AuthorizerPrePrepare)
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
                          BlockUIndex = item.BlockUIndex,
                          AuthResult = localAuthResult,
                          Type = ChatMessageType.AuthorizerPrepare,
                          From = Identity
                      };
                      await SendMessage(msg);
                  });
            }
            else if(item.Type == ChatMessageType.AuthorizerPrepare)
            {
                List<(string nodeTag, APIResultCodes result)> authResults;
                if (_activeConsensus.ContainsKey(item.BlockUIndex))
                {
                    authResults = _activeConsensus[item.BlockUIndex];
                }
                else
                {
                    authResults = new List<(string nodeTag, APIResultCodes result)>();
                    _activeConsensus.Add(item.BlockUIndex, authResults);
                }
                authResults.Add((item.From, item.AuthResult));     
                
                if(authResults.Count(a => a.result == APIResultCodes.Success) > 1)  //need to get from global config
                {
                    // do commit
                    var commiter = _client.GetGrain<IAuthorizer>(Guid.NewGuid(), "Lyra.Authorizer.Authorizers.AuthorizedCommiter");
                    await commiter.Commit(_pendingBlocks[item.BlockUIndex]);

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
            else if(item.Type == ChatMessageType.AuthorizerCommit)
            {
                // reply to client
            }
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
