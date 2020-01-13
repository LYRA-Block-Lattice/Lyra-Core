using System;
using System.Collections.Generic;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Fees;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Bson.Serialization.Options;
using System.Linq;
using Lyra.Core.Utils;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Lyra.Core.Accounts
{
    // this is account collection (collection of block chains) used on the node side only
    // 
    public class MongoAccountCollection : IAccountCollection
    {
        //private const string COLLECTION_DATABASE_NAME = "account_collection";
        private LyraConfig _config;

        private MongoClient _Client;

        private IMongoCollection<TransactionBlock> _blocks;

        readonly string _BlocksCollectionName = null;

        IMongoDatabase _db;

        readonly string _DatabaseName;

        public string Cluster { get; set; }

        public MongoAccountCollection()
        {
            _config = Neo.Settings.Default.LyraNode;

            _DatabaseName = _config.Lyra.Database.DatabaseName;

            _BlocksCollectionName = _config.Lyra.NetworkId + "-" + "Primary" + "-blocks";

            BsonClassMap.RegisterClassMap<TransactionBlock>(cm =>
            {
                cm.AutoMap();
                cm.MapMember(c => c.Balances).SetSerializer(new DictionaryInterfaceImplementerSerializer<Dictionary<string, decimal>>(DictionaryRepresentation.ArrayOfDocuments));
            });

            BsonClassMap.RegisterClassMap<SendTransferBlock>();
            BsonClassMap.RegisterClassMap<ExchangingBlock>();
            BsonClassMap.RegisterClassMap<ReceiveTransferBlock>();
            BsonClassMap.RegisterClassMap<OpenWithReceiveTransferBlock>();
            BsonClassMap.RegisterClassMap<LyraTokenGenesisBlock>();
            BsonClassMap.RegisterClassMap<TokenGenesisBlock>();
            BsonClassMap.RegisterClassMap<TradeBlock>();
            BsonClassMap.RegisterClassMap<TradeOrderBlock>();
            BsonClassMap.RegisterClassMap<ExecuteTradeOrderBlock>();
            BsonClassMap.RegisterClassMap<CancelTradeOrderBlock>();
            BsonClassMap.RegisterClassMap<OpenWithReceiveFeeBlock>();
            BsonClassMap.RegisterClassMap<ReceiveFeeBlock>();
            BsonClassMap.RegisterClassMap<ConsolidationBlock>();
            BsonClassMap.RegisterClassMap<ServiceBlock>();
            BsonClassMap.RegisterClassMap<AuthorizationSignature>();
            BsonClassMap.RegisterClassMap<NullTransactionBlock>();

            _blocks = GetDatabase().GetCollection<TransactionBlock>(_BlocksCollectionName);

            Cluster = GetDatabase().Client.Cluster.ToString();

            async Task CreateIndexes()
            {
                await _blocks.Indexes.CreateOneAsync(new CreateIndexModel<TransactionBlock>(Builders<TransactionBlock>
                    .IndexKeys.Ascending(x => x.UIndex))).ConfigureAwait(false);

                await _blocks.Indexes.CreateOneAsync(new CreateIndexModel<TransactionBlock>(Builders<TransactionBlock>
                    .IndexKeys.Ascending(x => x.Index))).ConfigureAwait(false);

                await _blocks.Indexes.CreateOneAsync(new CreateIndexModel<TransactionBlock>(Builders<TransactionBlock>
                    .IndexKeys.Ascending(x => x.AccountID))).ConfigureAwait(false);

                await _blocks.Indexes.CreateOneAsync(new CreateIndexModel<TransactionBlock>(Builders<TransactionBlock>
                    .IndexKeys.Ascending(x => x.BlockType))).ConfigureAwait(false);

                await _blocks.Indexes.CreateOneAsync(new CreateIndexModel<TransactionBlock>(Builders<TransactionBlock>
                    .IndexKeys.Ascending(x => x.Hash))).ConfigureAwait(false);

                await _blocks.Indexes.CreateOneAsync(new CreateIndexModel<TransactionBlock>(Builders<TransactionBlock>
                    .IndexKeys.Ascending(x => x.PreviousHash))).ConfigureAwait(false);
            }

            CreateIndexes().Wait();
        }

        /// <summary>
        /// Deletes all blocks and the block collection
        /// </summary>
        public void Delete()
        {
            if (GetClient() == null)
                return;

            if (GetDatabase() == null)
                return;

            GetDatabase().DropCollection(_BlocksCollectionName);
        }

        private MongoClient GetClient()
        {
            if (_Client == null)
                _Client = new MongoClient(_config.Lyra.Database.DBConnect);
            return _Client;
        }

        private IMongoDatabase GetDatabase()
        {
            if (_db == null)
                _db = GetClient().GetDatabase(_DatabaseName);
            return _db;
        }

        public long GetBlockCount()
        {
            return _blocks.CountDocumentsAsync(new BsonDocument()).Result;
        }

        public long GetBlockCount(string AccountId)
        {
            //var count = _blocks.Count(Query.EQ("AccountId", AccountId));
            //return (int)count;

            //int count = 0;
            //IEnumerable<AccountableBlock> result = _blocks.Find(Query.EQ("AccountId", AccountId));
            //if (result != null)
            //{
            //    foreach (AccountableBlock b in result)
            //        count = count + 1;
            //}

            //var count = _blocks.CountDocuments(x => x.AccountID == AccountId);

            var blocklist = GetAccountBlockList(AccountId);


            return blocklist.Count;
        }

        public bool AccountExists(string AccountId)
        {
            return GetBlockCount(AccountId) > 0;
        }

        public ServiceBlock GetLastServiceBlock()
        {
            var result = _blocks.Find(a => a.BlockType == BlockTypes.Service).ToList().Last();
            return result as ServiceBlock;
        }

        public ConsolidationBlock GetSyncBlock()
        {
            var result = _blocks.Find(a => a.BlockType == BlockTypes.Consolidation).ToList().Last();
            return result as ConsolidationBlock;
        }

        private List<TransactionBlock> GetAccountBlockList(string AccountId)
        {
            var result = _blocks.Find(x => x.AccountID == AccountId).SortBy(y => y.Index).ToList();
            return result;
        }

        public TransactionBlock FindLatestBlock()
        {
            //Expression<Func<TransactionBlock, bool>> predicate;
            //if (AccountId == null)
            //    predicate = a => true;
            //else
            //    predicate = a => a.AccountID == AccountId;

            var result = _blocks.Find(a => true).SortByDescending(y => y.UIndex).Limit(1);
            if (result.Any())
                return result.First();
            else
                return null;
        }

        public TransactionBlock FindLatestBlock(string AccountId)
        {
            var result = _blocks.Find(a => a.AccountID == AccountId).SortByDescending(y => y.Index).Limit(1);
            if (result.Any())
                return result.First();
            else
                return null;
        }

        public TokenGenesisBlock FindTokenGenesisBlock(string Hash, string Ticker)
        {
            //TokenGenesisBlock result = null;
            if (!string.IsNullOrEmpty(Hash))
            {
                var result = _blocks.Find(x => x.Hash == Hash);
                if (result.Any())
                    return result.First() as TokenGenesisBlock;
            }

            // to do - try to replace this by indexed search using BlockType indexed field (since we can't index Ticker field):
            // find all GenesysBlocks first, then check if one of them has the right ticker
            if (!string.IsNullOrEmpty(Ticker))
            {
                var builder = Builders<TransactionBlock>.Filter;
                var filterDefinition = builder.Eq("Ticker", Ticker);

                var result = _blocks.Find(filterDefinition);
                if (result.Any())
                    return result.First() as TokenGenesisBlock;
            }

            return null;
        }

        public List<TokenGenesisBlock> FindTokenGenesisBlocks(string keyword)
        {
            var builder = Builders<TransactionBlock>.Filter;
            var filterDefinition = builder.Eq("_t", "TokenGenesisBlock");
            var result = _blocks.Find(filterDefinition);

            if (string.IsNullOrEmpty(keyword))
            {
                return result.ToList().Cast<TokenGenesisBlock>().ToList();
            }
            else
            {
                return result.ToList().Cast<TokenGenesisBlock>().Where(a => a.Ticker.Contains(keyword)).ToList();
            }
        }

        public NullTransactionBlock FindNullTransBlockByHash(string hash)
        {
            var result = _blocks.AsQueryable<TransactionBlock>().OfType<NullTransactionBlock>().Where(a => a.FailedBlockHash == hash);
            if (result.Any())
                return result.First();
            else
                return null;
        }

        public TransactionBlock FindBlockByHash(string hash)
        {
            var result = _blocks.Find(x => x.Hash.Equals(hash));
            if (result.Any()) 
                return result.First();
            else
                return null;
        }

        public List<NonFungibleToken> GetNonFungibleTokens(string AccountId)
        {

            var p1 = new BsonArray();
            p1.Add(BlockTypes.ReceiveTransfer.ToString());
            p1.Add(BlockTypes.OpenAccountWithReceiveTransfer.ToString());
            p1.Add(BlockTypes.OpenAccountWithImport.ToString());

            var builder = Builders<TransactionBlock>.Filter;
            var filterDefinition = builder.And(builder.In("BlockType", p1), builder.And(builder.Eq("AccountID", AccountId), builder.Ne("NonFungibleToken", BsonNull.Value)));

            var allNonFungibleReceiveBlocks = _blocks.Find(filterDefinition).ToList();
           
            var the_list = new List<NonFungibleToken>();

            foreach (TransactionBlock receiveBlock in allNonFungibleReceiveBlocks)
            {
                the_list.Add(receiveBlock.NonFungibleToken);
            }

            if (the_list.Count > 0)
                return the_list;

            return null;
        }



        public TransactionBlock FindBlockByHash(string AccountId, string hash)
        {
            var result = _blocks.Find(x => x.AccountID == AccountId && x.Hash == hash);
            if (result.Any())
                return result.First();
            else
                return null;
        }

        public TransactionBlock FindBlockByPreviousBlockHash(string previousBlockHash)
        {
            var result = _blocks.Find(x => x.PreviousHash.Equals(previousBlockHash));
            if (result.Any())
                return result.First();
            else
                return null;
        }

        /// <summary>
        /// Ignores fee blocks!
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        public ReceiveTransferBlock FindBlockBySourceHash(string hash)
        {
            var builder = Builders<TransactionBlock>.Filter;
            var filterDefinition = builder.Eq("SourceHash", hash);

            var result = _blocks.Find(filterDefinition).ToList();

            foreach (var block in result)
            {
                if (block.BlockType == BlockTypes.OpenAccountWithReceiveFee || block.BlockType == BlockTypes.ReceiveFee)
                    continue;
                else
                    return block as ReceiveTransferBlock;
            }
            return null;
        }


        public TransactionBlock FindBlockByIndex(string AccountId, Int64 index)
        {
            var result = _blocks.Find(x => x.AccountID == AccountId && x.Index == index);
            if (result.Any())
                return result.First();
            else
                return null;
        }

        public SendTransferBlock FindUnsettledSendBlock(string AccountId)
        {
            // First, let find all send blocks:
            // (It can be optimzed as it's going to be growing, so it can be called with munimum Service Chain Height parameter to look only for recent blocks) 
            var builder = Builders<TransactionBlock>.Filter;
            var filterDefinition = builder.Eq("DestinationAccountId", AccountId);

            var allSendBlocks = _blocks.Find(filterDefinition).ToList();

            foreach (SendTransferBlock sendBlock in allSendBlocks)
            {
                //// Now, let's try to fetch the corresponding receive block:
                var p1 = new BsonArray();
                p1.Add((int)BlockTypes.ReceiveTransfer);
                p1.Add((int)BlockTypes.OpenAccountWithReceiveTransfer);
                p1.Add((int)BlockTypes.OpenAccountWithImport);
                p1.Add((int)BlockTypes.ImportAccount);

                var builder1 = Builders<TransactionBlock>.Filter;
                var filterDefinition1 = builder1.And(builder1.In("BlockType", p1), builder1.And(builder1.Eq("AccountID", AccountId), builder1.Eq("SourceHash", sendBlock.Hash)));

                var result = _blocks.Find(filterDefinition1);

                if (!result.Any())
                    return sendBlock;

                //var any_receive_block_with_this_source = FindBlockBySourceHash(sendBlock.Hash);
                //if (any_receive_block_with_this_source == null)

            }
            return null;
        }

        /// <summary>
        /// Returns the first unexecuted and incancelled trade aimed to an order created on the account.
        /// </summary>
        /// <param name="AccountId"></param>
        /// <param name="BuyTokenCode">
        /// The code of the token being purchased (optional).
        /// </param>
        /// <param name="SellTokenCode">
        /// The code of the token being sold (optional).
        /// </param>
        /// <returns></returns>
        public TradeBlock FindUnexecutedTrade(string AccountId, string BuyTokenCode, string SellTokenCode)
        {
            if (BuyTokenCode == "*")
                BuyTokenCode = null;

            if (SellTokenCode == "*")
                SellTokenCode = null;

            // First, let find all the trade blocks aimed to this account:
            //var trades = _blocks.Find(Query.And(Query.EQ("BlockType", BlockTypes.Trade.ToString()), Query.EQ("DestinationAccountId", AccountId)));

            var trades_builder = Builders<TransactionBlock>.Filter;
            var trades_filterDefinition = trades_builder.And(trades_builder.Eq("BlockType", BlockTypes.Trade.ToString()), trades_builder.Eq("DestinationAccountId", AccountId));

            var trades = _blocks.Find(trades_filterDefinition).ToList();

            foreach (TradeBlock trade in trades)
            {
                var exec_builder = Builders<TransactionBlock>.Filter;
                var exec_filterDefinition = exec_builder.And(exec_builder.Eq("BlockType", BlockTypes.ExecuteTradeOrder.ToString()), exec_builder.Eq("TradeId", trade.Hash));
                var trade_execution = _blocks.Find(exec_filterDefinition);

                if (trade_execution.Any())
                    continue;

                var cancel_builder = Builders<TransactionBlock>.Filter;
                var cancel_filterDefinition = cancel_builder.And(cancel_builder.Eq("BlockType", BlockTypes.CancelTradeOrder.ToString()), cancel_builder.Eq("TradeOrderId", trade.TradeOrderId));
                var trade_cancellation = _blocks.Find(cancel_filterDefinition);

                if (trade_cancellation.Any())
                    continue;

                if (!string.IsNullOrEmpty(BuyTokenCode) && BuyTokenCode != trade.BuyTokenCode)
                        continue;

                if (!string.IsNullOrEmpty(SellTokenCode) && SellTokenCode != trade.SellTokenCode)
                    continue;

                return trade;
            }
            return null;
        }

        public List<TradeOrderBlock> GetTradeOrderBlocks()
        {
            var list = new List<TradeOrderBlock>();

            //var blocks = _blocks.Find(Query.EQ("BlockType", BlockTypes.TradeOrder.ToString()));

            var builder = Builders<TransactionBlock>.Filter;
            var filterDefinition = builder.Eq("BlockType", BlockTypes.TradeOrder.ToString());
            var trade_blocks = _blocks.Find(filterDefinition).ToList();
            
            foreach (TradeOrderBlock block in trade_blocks)
                list.Add(block);

            return list;
        }

        // returns the list of hashes (order IDs) of all cancelled trade order blocks
        public List<string> GetTradeOrderCancellations()
        {
            var list = new List<string>();
            //var blocks = _blocks.Find(Query.EQ("BlockType", BlockTypes.CancelTradeOrder.ToString()));

            var builder = Builders<TransactionBlock>.Filter;
            var filterDefinition = builder.Eq("BlockType", BlockTypes.CancelTradeOrder.ToString());
            var blocks = _blocks.Find(filterDefinition).ToList();

            foreach (CancelTradeOrderBlock block in blocks)
                list.Add(block.TradeOrderId);

            return list;
        }

        // returns the list of hashes (order IDs) of all cancelled trade order blocks
        public List<string> GetExecutedTradeOrderBlocks()
        {
            var list = new List<string>();
            //var blocks = _blocks.Find(Query.EQ("BlockType", BlockTypes.ExecuteTradeOrder.ToString()));
            var builder = Builders<TransactionBlock>.Filter;
            var filterDefinition = builder.Eq("BlockType", BlockTypes.ExecuteTradeOrder.ToString());
            var blocks = _blocks.Find(filterDefinition).ToList();

            foreach (ExecuteTradeOrderBlock block in blocks)
                list.Add(block.TradeOrderId);

            return list;
        }

        public void AddBlock(TransactionBlock block)
        {
            if(block.Index == 0 || block.UIndex == 0)
                throw new Exception("AccountCollection=>AddBlock: Block with zero index/UIndex is now allowed!");

            if (null != GetBlockByUIndex(block.UIndex))
                throw new Exception("AccountCollection=>AddBlock: Block with such UIndex already exists!");

            if (FindBlockByHash(block.Hash) != null)
                throw new Exception("AccountCollection=>AddBlock: Block with such Hash already exists!");

            if (block.BlockType != BlockTypes.NullTransaction && FindBlockByIndex(block.AccountID, block.Index) != null)
                throw new Exception("AccountCollection=>AddBlock: Block with such Index already exists!");

            _blocks.InsertOne(block);
        }

        public void Dispose()
        {
           // nothing to dispose
        }

        public long GetNewestBlockUIndex()
        {
            var result = _blocks.Find(a => true)
                .SortByDescending(a => a.UIndex).FirstOrDefault();
            if (result != null)
                return result.UIndex;
            else
                return 0;
        }

        public TransactionBlock GetBlockByUIndex(long uindex)
        {
            var result = _blocks.Find(x => x.UIndex == uindex);
            return result.FirstOrDefault();
        }

    }
}