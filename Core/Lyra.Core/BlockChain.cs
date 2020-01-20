using Akka.Actor;
using Akka.Configuration;
using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Cryptography;
using Lyra.Core.Decentralize;
using Lyra.Core.Utils;
using Microsoft.Extensions.Logging;
using Neo;
using Neo.Cryptography.ECC;
using Neo.IO.Actors;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Lyra
{
    public class BlockChain : UntypedActor
    {
        public class NeedSync { public long ToUIndex { get; set; } }
        public class Startup { }
        public class PersistCompleted { }
        public class Import { }
        public class ImportCompleted { }
        public class FillMemoryPool { public IEnumerable<Transaction> Transactions; }
        public class FillCompleted { }
        public class BlockAdded { public string hash { get; set; } }

        public static BlockChain Singleton;
        public static readonly ECPoint[] StandbyValidators = ProtocolSettings.Default.StandbyValidators.OfType<string>().Select(p => //ECPoint.DecodePoint(p.HexToBytes(), ECCurve.Secp256r1)).ToArray();
                                                        ECPoint.FromBytes(Base58Encoding.DecodeAccountId(p), ECCurve.Secp256r1)).ToArray();

        public uint Height;
        public string NetworkID { get; private set; }
        public bool InSyncing { get; private set; }
        private LyraConfig _nodeConfig;
        private readonly IAccountCollectionAsync _store;
        private LyraSystem _sys;
        private ILogger _log;
        public BlockChain(LyraSystem sys)
        {
            if (Singleton != null)
                throw new Exception("Blockchain reinitialization");

            _sys = sys;

            var nodeConfig = Neo.Settings.Default.LyraNode;
            _store = new MongoAccountCollection();
            _log = new SimpleLogger("BlockChain").Logger;
            _nodeConfig = nodeConfig;
            NetworkID = nodeConfig.Lyra.NetworkId;

            Singleton = this;
        }
        public static Props Props(LyraSystem system)
        {
            return Akka.Actor.Props.Create(() => new BlockChain(system)).WithMailbox("blockchain-mailbox");
        }

        public async Task<long> GetNewestBlockUIndexAsync() => await _store.GetNewestBlockUIndexAsync();
        public async Task<TransactionBlock> GetBlockByUIndexAsync(long uindex) => await _store.GetBlockByUIndexAsync(uindex);
        internal async Task<ConsolidationBlock> GetSyncBlockAsync() => await _store.GetSyncBlockAsync();
        internal async Task<ServiceBlock> GetLastServiceBlockAsync() => await _store.GetLastServiceBlockAsync();

        // forward api. should have more control here.
        //public ServiceAccount ServiceAccount => _serviceAccount;
        public async Task<bool> AddBlockAsync(TransactionBlock block)
        {
            var result = await _store.AddBlockAsync(block);
            if(result)
            {
                LyraSystem.Singleton.Consensus.Tell(new BlockAdded { hash = block.Hash });
            }
            return result;
        }
        public async Task AddBlockAsync(ServiceBlock serviceBlock) => await _store.AddBlockAsync(serviceBlock);

        // bellow readonly access
        public async Task<bool> AccountExistsAsync(string AccountId) => await _store.AccountExistsAsync(AccountId);
        public async Task<TransactionBlock> FindLatestBlockAsync() => await _store.FindLatestBlockAsync();
        public async Task<TransactionBlock> FindLatestBlockAsync(string AccountId) => await _store.FindLatestBlockAsync(AccountId);
        public async Task<NullTransactionBlock> FindNullTransBlockByHashAsync(string hash) => await _store.FindNullTransBlockByHashAsync(hash);
        public async Task<TransactionBlock> FindBlockByHashAsync(string hash) => await _store.FindBlockByHashAsync(hash);
        public async Task<TransactionBlock> FindBlockByHashAsync(string AccountId, string hash) => await _store.FindBlockByHashAsync(AccountId, hash);
        public async Task<List<TokenGenesisBlock>> FindTokenGenesisBlocksAsync(string keyword) => await _store.FindTokenGenesisBlocksAsync(keyword);
        public async Task<TokenGenesisBlock> FindTokenGenesisBlockAsync(string Hash, string Ticker) => await _store.FindTokenGenesisBlockAsync(Hash, Ticker);
        public async Task<ReceiveTransferBlock> FindBlockBySourceHashAsync(string hash) => await _store.FindBlockBySourceHashAsync(hash);
        public async Task<long> GetBlockCountAsync() => await _store.GetBlockCountAsync();
        public async Task<TransactionBlock> FindBlockByIndexAsync(string AccountId, long index) => await _store.FindBlockByIndexAsync(AccountId, index);
        public async Task<List<NonFungibleToken>> GetNonFungibleTokensAsync(string AccountId) => await _store.GetNonFungibleTokensAsync(AccountId);
        public async Task<SendTransferBlock> FindUnsettledSendBlockAsync(string AccountId) => await _store.FindUnsettledSendBlockAsync(AccountId);
        public async Task<TransactionBlock> FindBlockByPreviousBlockHashAsync(string previousBlockHash) => await _store.FindBlockByPreviousBlockHashAsync(previousBlockHash);

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case NeedSync cmd:
                    SyncBlocksFromSeeds(cmd.ToUIndex);
                    break;
                case Startup _:
                    StartInitAsync().Wait();
                    break;
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
            }
        }

        private async Task StartInitAsync()
        {
            if (0 == await GetBlockCountAsync() && NodeService.Instance.PosWallet.AccountId ==
    ProtocolSettings.Default.StandbyValidators[0])
            {
                // do genesis
                var authGenesis = new ServiceBlock
                {
                    Index = 1,
                    UIndex = 1,
                    NetworkId = _nodeConfig.Lyra.NetworkId,
                    ShardId = "Primary",
                    TransferFee = 1,
                    TokenGenerationFee = 100,
                    TradeFee = 0.1m,
                    SvcAccountID = NodeService.Instance.PosWallet.AccountId
                };
                authGenesis.InitializeBlock(null, NodeService.Instance.PosWallet.PrivateKey,
                    _nodeConfig.Lyra.NetworkId, authGenesis.ShardId,
                    NodeService.Instance.PosWallet.AccountId);
                authGenesis.UHash = SignableObject.CalculateHash($"{authGenesis.UIndex}|{authGenesis.Index}|{authGenesis.Hash}");
                authGenesis.Authorizations = new List<AuthorizationSignature>();
                authGenesis.Authorizations.Add(new AuthorizationSignature
                {
                    Key = NodeService.Instance.PosWallet.AccountId,
                    Signature = Signatures.GetSignature(NodeService.Instance.PosWallet.PrivateKey, authGenesis.Hash, NodeService.Instance.PosWallet.AccountId)
                });
                // TODO: add more seed's auth info

                await _store.AddBlockAsync(authGenesis);

                // the first consolidate block
                var consBlock = new ConsolidationBlock
                {
                    UIndex = 2,
                    NetworkId = authGenesis.NetworkId,
                    ShardId = authGenesis.ShardId,
                    ServiceHash = authGenesis.Hash,
                    SvcAccountID = NodeService.Instance.PosWallet.AccountId
                };
                consBlock.InitializeBlock(authGenesis, NodeService.Instance.PosWallet.PrivateKey,
                    _nodeConfig.Lyra.NetworkId, authGenesis.ShardId,
                    NodeService.Instance.PosWallet.AccountId);
                consBlock.UHash = SignableObject.CalculateHash($"{consBlock.UIndex}|{consBlock.Index}|{consBlock.Hash}");
                consBlock.Authorizations = new List<AuthorizationSignature>();
                consBlock.Authorizations.Add(new AuthorizationSignature
                {
                    Key = NodeService.Instance.PosWallet.AccountId,
                    Signature = Signatures.GetSignature(NodeService.Instance.PosWallet.PrivateKey, consBlock.Hash + consBlock.ServiceHash, NodeService.Instance.PosWallet.AccountId)
                });

                await _store.AddBlockAsync(consBlock);

                // tell consensus what happened
                InSyncing = false;

                var board = new BillBoard();
                await board.AddMeAsync();   // add me!

                LyraSystem.Singleton.Consensus.Tell(board);
                LyraSystem.Singleton.Consensus.Tell(new ConsensusService.BlockChainSynced());
                _log.LogInformation("Service Genesis Completed.");
            }
            else
            {
                SyncBlocksFromSeeds(0);
            }
        }

        /// <summary>
        /// if this node is seed0 then sync with seeds others (random choice the one that is in normal state)
        /// if this node is seed1+ then sync with seed0
        /// otherwise sync with any seed node
        /// </summary>
        private void SyncBlocksFromSeeds(long ToUIndex)
        {
            InSyncing = true;
            Task.Run(async () => {

                while(true)
                {
                    _log.LogInformation("BlockChain Doing Sync...");
                    string syncWithUrl = null;
                    LyraRestClient client = null;
                    long syncToUIndex = ToUIndex;
#if DEBUG
                    for (int i = 0; i < 2; i++)         // save time 
#else
                    for (int i = 0; i < ProtocolSettings.Default.SeedList.Length; i++)
#endif
                    {
                        if (NodeService.Instance.PosWallet.AccountId == ProtocolSettings.Default.StandbyValidators[i])  // self
                            continue;

                        try
                        {
                            var addr = ProtocolSettings.Default.SeedList[i].Split(':')[0];
                            var apiUrl = $"https://{addr}:4505/api/LyraNode/";
                            _log.LogInformation("Platform {1} Use seed node of {0}", apiUrl, Environment.OSVersion.Platform);
                            client = await LyraRestClient.CreateAsync(NetworkID, Environment.OSVersion.Platform.ToString(), "LyraNode2", "1.0", apiUrl);
                            var mode = await client.GetSyncState();
                            if (mode.ResultCode == APIResultCodes.Success && mode.Mode == ConsensusWorkingMode.Normal)
                            {
                                syncWithUrl = apiUrl;
                                if (syncToUIndex == 0)
                                    syncToUIndex = mode.NewestBlockUIndex;
                                break;
                            }
                        }
                        catch(Exception ex)
                        {
                            _log.LogWarning($"Trying to sync.. {ex.Message}");
                        }
                    }

                    if (syncWithUrl == null)
                    {
                        // no node to sync.
                        if (NodeService.Instance.PosWallet.AccountId == ProtocolSettings.Default.StandbyValidators[0])
                        {
                            // seed0. no seed to sync. this seed must have the NORMAL blockchain     
                            var board = new BillBoard();
                            await board.AddMeAsync();   // add me!
                            LyraSystem.Singleton.Consensus.Tell(board);
                            break;
                        }
                        else
                        {
                            _log.LogError("No seed node in normal state. Wait...");
                            await Task.Delay(300 * 1000);
                        }
                    }
                    else
                    {
                        // update latest billboard
                        var board = await client.GetBillBoardAsync();
                        LyraSystem.Singleton.Consensus.Tell(board);

                        // do sync with node
                        long startUIndex = await _store.GetNewestBlockUIndexAsync() + 1;

                        _log.LogInformation($"BlockChain Doing sync from {startUIndex} to {syncToUIndex} from node {syncWithUrl}");

                        async Task<bool> DoCopyBlock(long fromUIndex, long toUIndex)
                        {
                            var authorizers = new AuthorizersFactory();

                            for (long j = fromUIndex; j <= toUIndex; j++)
                            {
                                var blockResult = await client.GetBlockByUIndex(j).ConfigureAwait(false);
                                if (blockResult.ResultCode == APIResultCodes.Success)
                                {
                                    var blockX = blockResult.GetBlock() as TransactionBlock;
                                    if(blockX.UIndex <= 2)      // the two genesis service block
                                    {
                                        await AddBlockAsync(blockX);
                                        continue;
                                    }

                                    var stopwatch = Stopwatch.StartNew();

                                    var authorizer = authorizers.Create(blockX.BlockType);
                                    var localAuthResult = await authorizer.AuthorizeAsync(blockX, false);

                                    stopwatch.Stop();
                                    _log.LogInformation($"Authorize takes {stopwatch.ElapsedMilliseconds} ms");

                                    if(localAuthResult.Item1 == APIResultCodes.Success)
                                    {
                                        await AddBlockAsync(blockX);
                                        fromUIndex = j + 1;
                                        _log.LogInformation($"BlockChain Synced Block Number: {j}");
                                    }
                                    else
                                    {
                                        _log.LogInformation($"BlockChain Block Number: {j} verify failed for {localAuthResult.Item1}");
                                        return false;
                                    }
                                }
                                else
                                {
                                    // error
                                    _log.LogInformation($"Error syncing block: {blockResult.ResultCode}");
                                    continue;
                                }
                            }
                            return true;
                        }

                        var copyOK = await DoCopyBlock(startUIndex, syncToUIndex).ConfigureAwait(false);
                        if(copyOK)
                        {
                            //// check missing block
                            //for(long k = 1; k <= startUIndex; k++)
                            //{
                            //    if(await BlockChain.Singleton.GetBlockByUIndex(k) == null)
                            //    {
                            //        _log.LogInformation($"syncing one missing block: {k}");
                            //        await DoCopyBlock(k, k).ConfigureAwait(false);
                            //    }
                            //}
                            break;
                        }
                        else
                        {
                            await Task.Delay(5000).ConfigureAwait(false);
                        }
                    }
                }

                InSyncing = false;
                LyraSystem.Singleton.Consensus.Tell(new ConsensusService.BlockChainSynced());
                _log.LogInformation("BlockChain Sync Completed.");
            });
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
