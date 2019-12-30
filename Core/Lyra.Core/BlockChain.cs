using Akka.Actor;
using Akka.Configuration;
using Lyra.Core.Accounts;
using Lyra.Core.Blocks;
using Lyra.Core.Cryptography;
using Lyra.Core.Decentralize;
using Lyra.Core.Utils;
using Neo;
using Neo.IO.Actors;
using System;
using System.Collections.Generic;

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

        //private readonly ServiceAccount _serviceAccount;
        private readonly IAccountCollection _store;
        private LyraSystem _sys;
        public BlockChain(LyraSystem sys, LyraNodeConfig nodeConfig)
        {
            _sys = sys;
            _store = new MongoAccountCollection(nodeConfig);

            Singleton = this;

            if(0 == GetBlockCount())
            {
                // do genesis
                var authGenesis = new ServiceBlock
                {
                    Index = 1,
                    UIndex = 1,                   
                    NetworkId = nodeConfig.Lyra.NetworkId,
                    ShardId = "Primary",
                    TransferFee = 1,
                    TokenGenerationFee = 100,
                    TradeFee = 0.1m
                };
                authGenesis.InitializeBlock(null, NodeService.Instance.PosWallet.PrivateKey,
                    nodeConfig.Lyra.NetworkId, authGenesis.ShardId,
                    NodeService.Instance.PosWallet.AccountId);
                authGenesis.UHash = SignableObject.CalculateHash($"{authGenesis.UIndex}|{authGenesis.Index}|{authGenesis.Hash}");
                authGenesis.Authorizations = new List<AuthorizationSignature>();
                authGenesis.Authorizations.Add(new AuthorizationSignature
                {
                    Key = NodeService.Instance.PosWallet.AccountId,
                    Signature = Signatures.GetSignature(NodeService.Instance.PosWallet.PrivateKey, authGenesis.Hash, NodeService.Instance.PosWallet.AccountId)
                });
                // TODO: add more seed's auth info

                _store.AddBlock(authGenesis);

                // the first consolidate block
                var consBlock = new ConsolidationBlock
                {
                    UIndex = 2,
                    NetworkId = authGenesis.NetworkId,
                    ShardId = authGenesis.ShardId,
                    ServiceHash = authGenesis.Hash,
                    LastServiceBlockHash = authGenesis.Hash
                };
                consBlock.InitializeBlock(authGenesis, NodeService.Instance.PosWallet.PrivateKey,
                    nodeConfig.Lyra.NetworkId, authGenesis.ShardId,
                    NodeService.Instance.PosWallet.AccountId);
                consBlock.UHash = SignableObject.CalculateHash($"{consBlock.UIndex}|{consBlock.Index}|{consBlock.Hash}");
                consBlock.Authorizations = new List<AuthorizationSignature>();
                consBlock.Authorizations.Add(new AuthorizationSignature
                {
                    Key = NodeService.Instance.PosWallet.AccountId,
                    Signature = Signatures.GetSignature(NodeService.Instance.PosWallet.PrivateKey, consBlock.Hash + consBlock.ServiceHash, NodeService.Instance.PosWallet.AccountId)
                });

                _store.AddBlock(consBlock);
            }
        }
        public static Props Props(LyraSystem system, LyraNodeConfig nodeConfig)
        {
            return Akka.Actor.Props.Create(() => new BlockChain(system, nodeConfig)).WithMailbox("blockchain-mailbox");
        }

        internal ConsolidationBlock GetSyncBlock() => _store.GetSyncBlock();
        internal ServiceBlock GetLastServiceBlock() => _store.GetLastServiceBlock();

        // forward api. should have more control here.
        //public ServiceAccount ServiceAccount => _serviceAccount;
        public void AddBlock(TransactionBlock block) => _store.AddBlock(block);
        public void AddBlock(ServiceBlock serviceBlock) => _store.AddBlock(serviceBlock);

        // bellow readonly access
        public bool AccountExists(string AccountId) => _store.AccountExists(AccountId);
        public TransactionBlock FindLatestBlock(string AccountId) => _store.FindLatestBlock(AccountId);
        public TransactionBlock FindBlockByHash(string hash) => _store.FindBlockByHash(hash);
        public TransactionBlock FindBlockByHash(string AccountId, string hash) => _store.FindBlockByHash(AccountId, hash);
        public List<TokenGenesisBlock> FindTokenGenesisBlocks(string keyword) => _store.FindTokenGenesisBlocks(keyword);
        public TokenGenesisBlock FindTokenGenesisBlock(string Hash, string Ticker) => _store.FindTokenGenesisBlock(Hash, Ticker);
        public ReceiveTransferBlock FindBlockBySourceHash(string hash) => _store.FindBlockBySourceHash(hash);
        public long GetBlockCount() => _store.GetBlockCount();
        public TransactionBlock FindBlockByIndex(string AccountId, long index) => _store.FindBlockByIndex(AccountId, index);
        public List<NonFungibleToken> GetNonFungibleTokens(string AccountId) => _store.GetNonFungibleTokens(AccountId);
        public SendTransferBlock FindUnsettledSendBlock(string AccountId) => _store.FindUnsettledSendBlock(AccountId);
        public TransactionBlock FindBlockByPreviousBlockHash(string previousBlockHash) => _store.FindBlockByPreviousBlockHash(previousBlockHash);

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
