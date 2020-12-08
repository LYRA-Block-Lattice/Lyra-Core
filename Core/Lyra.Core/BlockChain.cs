using Akka.Actor;
using Akka.Configuration;
using Lyra.Core.Accounts;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Core.Utils;
using Lyra.Data.Utils;
using Lyra.Shared;
using Microsoft.Extensions.Logging;
using Neo.IO.Actors;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lyra
{
    public class BlockChain : ReceiveActor
    {
        public class Startup { }
        public class PersistCompleted { }
        public class Import { }
        public class ImportCompleted { }
        public class BlockAdded
        {
            public Block NewBlock { get; set; }
        }

        public uint Height;
        public string NetworkID { get; private set; }

        AuthorizersFactory _authorizerFactory = new AuthorizersFactory();

        private LyraConfig _nodeConfig;
        private readonly IAccountCollectionAsync _store;
        private DagSystem _sys;
        private ILogger _log;

        public BlockChain(DagSystem sys, IAccountCollectionAsync store)
        {
            _sys = sys;

            var nodeConfig = Neo.Settings.Default.LyraNode;
            _store = store; //new MongoAccountCollection();

            //_store = new LiteAccountCollection(Utilities.LyraDataDir);
            _log = new SimpleLogger("BlockChain").Logger;
            _nodeConfig = nodeConfig;
            NetworkID = nodeConfig.Lyra.NetworkId;

            Receive<Startup>(_ => { });
            Receive<Idle>(_ => { });
        }

        public static Props Props(DagSystem system, IAccountCollectionAsync store)
        {
            return Akka.Actor.Props.Create(() => new BlockChain(system, store)).WithMailbox("blockchain-mailbox");
        }

        #region storage api
        private async Task<bool> AddBlockImplAsync(Block block)
        {
            var result = await _store.AddBlockAsync(block);
            if (result)
            {
                _sys.Consensus.Tell(new BlockAdded { NewBlock = block });
            }
            return result;
        }

        //public async Task<IEnumerable<Block>> GetAllUnConsolidatedBlocksAsync() => await StopWatcher.Track(_store.GetAllUnConsolidatedBlocksAsync(), StopWatcher.GetCurrentMethod());
        //public async Task<IEnumerable<string>> GetAllUnConsolidatedBlockHashesAsync() => await StopWatcher.Track(_store.GetAllUnConsolidatedBlockHashesAsync(), StopWatcher.GetCurrentMethod());
        internal async Task<ConsolidationBlock> GetLastConsolidationBlockAsync() => await StopWatcher.Track(_store.GetLastConsolidationBlockAsync(), StopWatcher.GetCurrentMethod());//_store.GetSyncBlockAsync();
        public async Task<List<ConsolidationBlock>> GetConsolidationBlocksAsync(long startHeight, int count) => await StopWatcher.Track(_store.GetConsolidationBlocksAsync(startHeight, count), StopWatcher.GetCurrentMethod());
        internal async Task<ServiceBlock> GetLastServiceBlockAsync() => await StopWatcher.Track(_store.GetLastServiceBlockAsync(), StopWatcher.GetCurrentMethod());//_store.GetLastServiceBlockAsync();

        // forward api. should have more control here.
        public async Task<bool> AddBlockAsync(Block block) => await StopWatcher.Track(AddBlockImplAsync(block), StopWatcher.GetCurrentMethod());
        public async Task RemoveBlockAsync(string hash) => await _store.RemoveBlockAsync(hash);
        //public async Task AddBlockAsync(ServiceBlock serviceBlock) => await StopWatcher.Track(_store.AddBlockAsync(serviceBlock), StopWatcher.GetCurrentMethod());//_store.AddBlockAsync(serviceBlock);

        // bellow readonly access
        public async Task<bool> AccountExistsAsync(string AccountId) => await StopWatcher.Track(_store.AccountExistsAsync(AccountId), StopWatcher.GetCurrentMethod());//_store.AccountExistsAsync(AccountId);
        public async Task<Block> FindLatestBlockAsync() => await StopWatcher.Track(_store.FindLatestBlockAsync(), StopWatcher.GetCurrentMethod());//_store.FindLatestBlockAsync();
        public async Task<Block> FindLatestBlockAsync(string AccountId) => await StopWatcher.Track(_store.FindLatestBlockAsync(AccountId), StopWatcher.GetCurrentMethod());//_store.FindLatestBlockAsync(AccountId);
        public async Task<NullTransactionBlock> FindNullTransBlockByHashAsync(string hash) => await StopWatcher.Track(_store.FindNullTransBlockByHashAsync(hash), StopWatcher.GetCurrentMethod());//_store.FindNullTransBlockByHashAsync(hash);
        public async Task<Block> FindBlockByHashAsync(string hash) => await StopWatcher.Track(_store.FindBlockByHashAsync(hash), StopWatcher.GetCurrentMethod());//_store.FindBlockByHashAsync(hash);
        public async Task<Block> FindBlockByHashAsync(string AccountId, string hash) => await StopWatcher.Track(_store.FindBlockByHashAsync(AccountId, hash), StopWatcher.GetCurrentMethod());//_store.FindBlockByHashAsync(AccountId, hash);
        public async Task<List<TokenGenesisBlock>> FindTokenGenesisBlocksAsync(string keyword) => await StopWatcher.Track(_store.FindTokenGenesisBlocksAsync(keyword), StopWatcher.GetCurrentMethod());//_store.FindTokenGenesisBlocksAsync(keyword);
        public async Task<TokenGenesisBlock> FindTokenGenesisBlockAsync(string Hash, string Ticker) => await StopWatcher.Track(_store.FindTokenGenesisBlockAsync(Hash, Ticker), StopWatcher.GetCurrentMethod());//_store.FindTokenGenesisBlockAsync(Hash, Ticker);
        public async Task<ReceiveTransferBlock> FindBlockBySourceHashAsync(string hash) => await StopWatcher.Track(_store.FindBlockBySourceHashAsync(hash), StopWatcher.GetCurrentMethod());//_store.FindBlockBySourceHashAsync(hash);
        public async Task<long> GetBlockCountAsync() => await StopWatcher.Track(_store.GetBlockCountAsync(), StopWatcher.GetCurrentMethod());//_store.GetBlockCountAsync();
        public async Task<TransactionBlock> FindBlockByIndexAsync(string AccountId, long index) => await StopWatcher.Track(_store.FindBlockByIndexAsync(AccountId, index), StopWatcher.GetCurrentMethod());//_store.FindBlockByIndexAsync(AccountId, index);
        public async Task<List<NonFungibleToken>> GetNonFungibleTokensAsync(string AccountId) => await StopWatcher.Track(_store.GetNonFungibleTokensAsync(AccountId), StopWatcher.GetCurrentMethod());//_store.GetNonFungibleTokensAsync(AccountId);
        public async Task<SendTransferBlock> FindUnsettledSendBlockAsync(string AccountId) => await StopWatcher.Track(_store.FindUnsettledSendBlockAsync(AccountId), StopWatcher.GetCurrentMethod());//_store.FindUnsettledSendBlockAsync(AccountId);
        public async Task<TransactionBlock> FindBlockByPreviousBlockHashAsync(string previousBlockHash) => await StopWatcher.Track(_store.FindBlockByPreviousBlockHashAsync(previousBlockHash), StopWatcher.GetCurrentMethod());//_store.FindBlockByPreviousBlockHashAsync(previousBlockHash);
        //public async Task<Vote> GetVotesForAccountAsync(string accountId) => await _store.GetVotesForAccountAsync(accountId);
        //public async Task UpdateVotesForAccountAsync(Vote vote) => await _store.UpdateVotesForAccountAsync(vote);
        #endregion
    }

    internal class BlockchainMailbox : PriorityMailbox
    {
        public BlockchainMailbox(Akka.Actor.Settings settings, Config config)
            : base(settings, config)
        {
        }

        internal protected override bool IsHighPriority(object message)
        {
            switch (message)
            {
                //case Header[] _:
                //case Block _:
                //case ConsensusPayload _:
                case Terminated _:
                    return true;
                default:
                    return false;
            }
        }
    }
}
