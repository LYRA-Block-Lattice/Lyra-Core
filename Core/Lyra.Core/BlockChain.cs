using Akka.Actor;
using Akka.Configuration;
using Lyra.Core.Accounts;
using Lyra.Core.Blocks;
using Neo;
using Neo.IO.Actors;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra
{
    public class BlockChain : UntypedActor
    {
        public class PersistCompleted { }
        public class Import { }
        public class ImportCompleted { }
        public class FillMemoryPool { public IEnumerable<Transaction> Transactions; }
        public class FillCompleted { }

        public static BlockChain Singleton;
        public uint Height;

        private readonly ServiceAccount _serviceAccount;
        private readonly IAccountCollection _blockLists;
        private LyraSystem _sys;
        public BlockChain(LyraSystem sys)
        {
            _sys = sys;
            Singleton = this;
        }
        public static Props Props(LyraSystem system)
        {
            return Akka.Actor.Props.Create(() => new BlockChain(system)).WithMailbox("blockchain-mailbox");
        }

        // forward api. should have more control here.
        public ServiceAccount ServiceAccount => _serviceAccount;
        public void AddBlock(TransactionBlock block) => _blockLists.AddBlock(block);


        // bellow readonly access
        public bool AccountExists(string AccountId) => _blockLists.AccountExists(AccountId);
        public TransactionBlock FindLatestBlock(string AccountId) => _blockLists.FindLatestBlock(AccountId);
        public TransactionBlock FindBlockByHash(string hash) => _blockLists.FindBlockByHash(hash);
        public TransactionBlock FindBlockByHash(string AccountId, string hash) => _blockLists.FindBlockByHash(AccountId, hash);
        public List<TokenGenesisBlock> FindTokenGenesisBlocks(string keyword) => _blockLists.FindTokenGenesisBlocks(keyword);
        public TokenGenesisBlock FindTokenGenesisBlock(string Hash, string Ticker) => _blockLists.FindTokenGenesisBlock(Hash, Ticker);
        public ReceiveTransferBlock FindBlockBySourceHash(string hash) => _blockLists.FindBlockBySourceHash(hash);
        public Task<long> GetBlockCountAsync() => _blockLists.GetBlockCountAsync();
        public TransactionBlock FindBlockByIndex(string AccountId, long index) => _blockLists.FindBlockByIndex(AccountId, index);
        public List<NonFungibleToken> GetNonFungibleTokens(string AccountId) => _blockLists.GetNonFungibleTokens(AccountId);
        public SendTransferBlock FindUnsettledSendBlock(string AccountId) => _blockLists.FindUnsettledSendBlock(AccountId);
        public TransactionBlock FindBlockByPreviousBlockHash(string previousBlockHash) => _blockLists.FindBlockByPreviousBlockHash(previousBlockHash);

        protected override void OnReceive(object message)
        {
            //switch (message)
            //{
            //    case Import import:
            //        OnImport(import.Blocks);
            //        break;
            //    case FillMemoryPool fill:
            //        OnFillMemoryPool(fill.Transactions);
            //        break;
            //    case Header[] headers:
            //        OnNewHeaders(headers);
            //        break;
            //    case Block block:
            //        Sender.Tell(OnNewBlock(block));
            //        break;
            //    case Transaction[] transactions:
            //        {
            //            // This message comes from a mempool's revalidation, already relayed
            //            foreach (var tx in transactions) OnNewTransaction(tx, false);
            //            break;
            //        }
            //    case Transaction transaction:
            //        Sender.Tell(OnNewTransaction(transaction, true));
            //        break;
            //    case ConsensusPayload payload:
            //        Sender.Tell(OnNewConsensus(payload));
            //        break;
            //    case Idle _:
            //        if (MemPool.ReVerifyTopUnverifiedTransactionsIfNeeded(MaxTxToReverifyPerIdle, currentSnapshot))
            //            Self.Tell(Idle.Instance, ActorRefs.NoSender);
            //        break;
            //}
        }
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
