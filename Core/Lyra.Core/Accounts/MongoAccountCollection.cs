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
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Lyra.Core.Accounts
{
    // this is account collection (collection of block chains) used on the node side only
    // 
    public class MongoAccountCollection : IAccountCollectionAsync
    {
        //private const string COLLECTION_DATABASE_NAME = "account_collection";
        private LyraConfig _config;

        private MongoClient _Client;

        private IMongoCollection<Block> _blocks;

        readonly string _blocksCollectionName;
        readonly string _authorizersViewCollectionName;

        IMongoDatabase _db;

        readonly string _DatabaseName;

        ILogger _log;

        public string Cluster { get; set; }

        public MongoAccountCollection()
        {
            _log = new SimpleLogger("Mongo").Logger;

            _config = Neo.Settings.Default.LyraNode;

            _DatabaseName = _config.Lyra.Database.DatabaseName;

            _blocksCollectionName = $"{_config.Lyra.NetworkId}_blocks";
            _authorizersViewCollectionName = $"{_config.Lyra.NetworkId}_views";

            BsonClassMap.RegisterClassMap<Block>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                //cm.MapMember(c => c.Balances).SetSerializer(new DictionaryInterfaceImplementerSerializer<Dictionary<string, decimal>>(DictionaryRepresentation.ArrayOfDocuments));
            });

            BsonClassMap.RegisterClassMap<TransactionBlock>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
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

            _blocks = GetDatabase().GetCollection<Block>(_blocksCollectionName);

            Cluster = GetDatabase().Client.Cluster.ToString();

            async Task CreateIndexes(string columnName, bool uniq)
            {
                try
                {
                    var options = new CreateIndexOptions() { Unique = uniq };
                    var field = new StringFieldDefinition<Block>(columnName);
                    var indexDefinition = new IndexKeysDefinitionBuilder<Block>().Ascending(field);
                    var indexModel = new CreateIndexModel<Block>(indexDefinition, options);
                    await _blocks.Indexes.CreateOneAsync(indexModel);
                }
                catch(Exception ex)
                {
                    await _blocks.Indexes.DropOneAsync(columnName + "_1");
                    await CreateIndexes(columnName, uniq);
                }
            }

            async Task CreateNoneStringIndex(string colName, bool uniq)
            {
                try
                {
                    var options = new CreateIndexOptions() { Unique = uniq };
                    IndexKeysDefinition<Block> keyCode = "{ " + colName + ": 1 }";
                    var codeIndexModel = new CreateIndexModel<Block>(keyCode, options);
                    await _blocks.Indexes.CreateOneAsync(codeIndexModel);

                }
                catch (Exception ex)
                {
                    await _blocks.Indexes.DropOneAsync(colName + "_1");
                    await CreateIndexes(colName, uniq);
                }
            }

            CreateIndexes("Hash", true).Wait();
            CreateIndexes("Consolidated", false).Wait();
            CreateIndexes("PreviousHash", false).Wait();
            CreateIndexes("AccountID", false).Wait();
            CreateNoneStringIndex("Height", false).Wait();
            CreateNoneStringIndex("BlockType", false).Wait();

            CreateIndexes("SourceHash", false).Wait();
            CreateIndexes("DestinationAccountId", false).Wait();
            CreateIndexes("Ticker", false).Wait();
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

            GetDatabase().DropCollection(_blocksCollectionName);
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

        public async Task<long> GetBlockCountAsync()
        {
            return await _blocks.CountDocumentsAsync(new BsonDocument());
        }

        public async Task<long> GetBlockCountAsync(string AccountId)
        {
            var filter = Builders<Block>.Filter.Eq("AccountID", AccountId);
            var result = await _blocks.CountDocumentsAsync(filter);

            return result;
        }

        public async Task<bool> AccountExistsAsync(string AccountId)
        {
            var options = new FindOptions<Block, Block>
            {
                Limit = 1
            };

            var filter = Builders<Block>.Filter.Eq("AccountID", AccountId);
            var result = await _blocks.FindAsync(filter, options);
            return await result.AnyAsync();
        }

        public async Task<ServiceBlock> GetLastServiceBlockAsync()
        {
            var options = new FindOptions<Block, Block>
            {
                Limit = 1,
                Sort = Builders<Block>.Sort.Descending(o => o.Height)
            };
            var filter = Builders<Block>.Filter;
            var filterDefination = filter.Eq("BlockType", BlockTypes.Service);

            var finds = await _blocks.FindAsync(filterDefination, options);
            return await finds.FirstOrDefaultAsync() as ServiceBlock;
        }

        public async Task<ConsolidationBlock> GetLastConsolidationBlockAsync()
        {
            var options = new FindOptions<Block, Block>
            {
                Limit = 1,
                Sort = Builders<Block>.Sort.Descending(o => o.Height)
            };
            var filter = Builders<Block>.Filter.Eq("BlockType", BlockTypes.Consolidation);

            var finds = await _blocks.FindAsync(filter, options);
            var result = await finds.FirstOrDefaultAsync();
            return result as ConsolidationBlock;
        }

        public async Task<List<ConsolidationBlock>> GetConsolidationBlocksAsync(long startHeight)
        {
            var options = new FindOptions<Block, Block>
            {
                Limit = 100,
                Sort = Builders<Block>.Sort.Ascending(o => o.Height)
            };
            var builder = Builders<Block>.Filter;
            var filterDefinition = builder.And(builder.Eq("BlockType", BlockTypes.Consolidation),
                builder.Gte("Height", startHeight));

            var result = await _blocks.FindAsync(filterDefinition, options);
            return result.ToList().Cast<ConsolidationBlock>().ToList();
        }

        //private async Task<List<TransactionBlock>> GetAccountBlockListAsync(string AccountId)
        //{
        //    var finds = await _blocks.FindAsync(x => x.AccountID == AccountId);
        //    var list = await finds.ToListAsync();
        //    var result = list.OrderBy(y => y.Index).ToList();
        //    return result;
        //}

        public async Task<Block> FindLatestBlockAsync()
        {
            var options = new FindOptions<Block, Block>
            {
                Limit = 1,
                Sort = Builders<Block>.Sort.Descending(o => o.TimeStamp)
            };

            var result = await (await _blocks.FindAsync(FilterDefinition<Block>.Empty, options)).FirstOrDefaultAsync();
            return result;
        }

        public async Task<Block> FindLatestBlockAsync(string AccountId)
        {
            var options = new FindOptions<Block, Block>
            {
                Limit = 1,
                Sort = Builders<Block>.Sort.Descending(o => o.TimeStamp)
            };
            var filter = Builders<Block>.Filter.Eq("AccountID", AccountId);

            var result = await (await _blocks.FindAsync(filter, options)).FirstOrDefaultAsync();
            return result;
        }

        public async Task<TokenGenesisBlock> FindTokenGenesisBlockAsync(string Hash, string Ticker)
        {
            //TokenGenesisBlock result = null;
            if (!string.IsNullOrEmpty(Hash))
            {
                var result = await (await _blocks.FindAsync(x => x.Hash == Hash)).FirstOrDefaultAsync();
                if (result != null)
                    return result as TokenGenesisBlock;
            }

            var builder = Builders<Block>.Filter;
            var filterDefinition = builder.And(builder.Eq("BlockType", BlockTypes.TokenGenesis), builder.Eq("Ticker", Ticker));
            var blocks = await _blocks.FindAsync(filterDefinition);
            return await blocks.FirstOrDefaultAsync() as TokenGenesisBlock;
        }

        public async Task<List<TokenGenesisBlock>> FindTokenGenesisBlocksAsync(string keyword)
        {
            var builder = Builders<Block>.Filter;
            var filterDefinition = builder.Eq("BlockType", BlockTypes.TokenGenesis);
            var result = await _blocks.FindAsync(filterDefinition);

            if (string.IsNullOrEmpty(keyword))
            {
                return result.ToList().Cast<TokenGenesisBlock>().ToList();
            }
            else
            {
                return result.ToList().Cast<TokenGenesisBlock>().Where(a => a.Ticker.Contains(keyword)).ToList();
            }
        }

        public async Task<NullTransactionBlock> FindNullTransBlockByHashAsync(string hash)
        {
            var builder = Builders<Block>.Filter;
            var filterDefinition = builder.And(builder.Eq("BlockType", BlockTypes.NullTransaction), builder.Eq("FailedBlockHash", hash));
            var result = await _blocks.FindAsync(filterDefinition);

            return await result.FirstOrDefaultAsync() as NullTransactionBlock;
        }

        public async Task<Block> FindBlockByHashAsync(string hash)
        {
            if (string.IsNullOrEmpty(hash))
                return null;

            var options = new FindOptions<Block, Block>
            {
                Limit = 1,
            };
            var filter = Builders<Block>.Filter.Eq("Hash", hash);

            var block = await (await _blocks.FindAsync(filter)).FirstOrDefaultAsync();
            return block;
        }

        public async Task<Block> FindBlockByHashAsync(string AccountId, string hash)
        {
            var builder = Builders<Block>.Filter;
            var filterDefinition = builder.And(builder.Eq("Hash", hash), builder.Eq("AccountID", AccountId));

            var block = await (await _blocks.FindAsync(filterDefinition)).FirstOrDefaultAsync();
            return block as TransactionBlock;
        }

        public async Task<List<NonFungibleToken>> GetNonFungibleTokensAsync(string AccountId)
        {

            var p1 = new BsonArray();
            p1.Add(BlockTypes.ReceiveTransfer);
            p1.Add(BlockTypes.OpenAccountWithReceiveTransfer);
            p1.Add(BlockTypes.OpenAccountWithImport);

            var builder = Builders<Block>.Filter;
            var filterDefinition = builder.And(builder.In("BlockType", p1), builder.And(builder.Eq("AccountID", AccountId), builder.Ne("NonFungibleToken", BsonNull.Value)));

            var allNonFungibleReceiveBlocks = await (await _blocks.FindAsync(filterDefinition)).ToListAsync();

            var the_list = new List<NonFungibleToken>();

            foreach (TransactionBlock receiveBlock in allNonFungibleReceiveBlocks)
            {
                the_list.Add(receiveBlock.NonFungibleToken);
            }

            if (the_list.Count > 0)
                return the_list;

            return null;
        }


        public async Task<TransactionBlock> FindBlockByPreviousBlockHashAsync(string previousBlockHash)
        {
            var builder = Builders<Block>.Filter;
            var filterDefinition = builder.Eq("PreviousHash", previousBlockHash);
            var result = await _blocks.FindAsync(filterDefinition);

            return await result.FirstOrDefaultAsync() as TransactionBlock;
        }

        /// <summary>
        /// Ignores fee blocks!
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        public async Task<ReceiveTransferBlock> FindBlockBySourceHashAsync(string hash)
        {
            var builder = Builders<Block>.Filter;
            var filterDefinition = builder.Eq("SourceHash", hash);

            var result = await (await _blocks.FindAsync(filterDefinition)).ToListAsync();

            foreach (var block in result)
            {
                if (block.BlockType == BlockTypes.OpenAccountWithReceiveFee || block.BlockType == BlockTypes.ReceiveFee)
                    continue;
                else
                    return block as ReceiveTransferBlock;
            }
            return null;
        }


        public async Task<TransactionBlock> FindBlockByIndexAsync(string AccountId, Int64 index)
        {
            var builder = new FilterDefinitionBuilder<Block>();
            var filterDefinition = builder.And(builder.Eq("AccountID", AccountId),
                builder.Eq("Height", index));

            var block = await (await _blocks.FindAsync(filterDefinition)).FirstOrDefaultAsync();
            return block as TransactionBlock;
        }

        private async Task<ReceiveTransferBlock> FindLastRecvBlock(string AccountId)
        {
            var options = new FindOptions<Block, Block>
            {
                Limit = 1,
                Sort = Builders<Block>.Sort.Descending(o => o.Height)
            };
            var builder = new FilterDefinitionBuilder<Block>();
            var filterDefinition = builder.And(builder.Eq("AccountID", AccountId),
                    builder.Eq("BlockType", BlockTypes.ReceiveTransfer));

            var result = await (await _blocks.FindAsync(filterDefinition, options)).FirstOrDefaultAsync();
            return result as ReceiveTransferBlock;
        }

        public async Task<SendTransferBlock> FindUnsettledSendBlockAsync(string AccountId)
        {
            long fromIndex = 0;

            // get last settled receive block
            var lastRecvBlock = await FindLastRecvBlock(AccountId);
            if(lastRecvBlock != null)
            {
                var lastSendToThisAccountBlock = await FindBlockByHashAsync(lastRecvBlock.SourceHash);

                if (lastSendToThisAccountBlock != null)
                    fromIndex = lastSendToThisAccountBlock.Height;
            }    

            // First, let find all send blocks:
            // (It can be optimzed as it's going to be growing, so it can be called with munimum Service Chain Height parameter to look only for recent blocks) 
            var builder = Builders<Block>.Filter;
            var filterDefinition = builder.Eq("DestinationAccountId", AccountId) & builder.Gt("Height", fromIndex);

            var allSendBlocks = await (await _blocks.FindAsync(filterDefinition)).ToListAsync();

            foreach (SendTransferBlock sendBlock in allSendBlocks)
            {
                //// Now, let's try to fetch the corresponding receive block:
                var p1 = new BsonArray();
                p1.Add((int)BlockTypes.ReceiveTransfer);
                p1.Add((int)BlockTypes.OpenAccountWithReceiveTransfer);
                p1.Add((int)BlockTypes.OpenAccountWithImport);
                p1.Add((int)BlockTypes.ImportAccount);

                var builder1 = Builders<Block>.Filter;
                var filterDefinition1 = builder1.And(builder1.In("BlockType", p1), builder1.And(builder1.Eq("AccountID", AccountId), builder1.Eq("SourceHash", sendBlock.Hash)));

                var result = await (await _blocks.FindAsync(filterDefinition1)).FirstOrDefaultAsync();

                if (result == null)
                    return sendBlock;

                //var any_receive_block_withBlockTypehis_source = FindBlockBySourceHash(sendBlock.Hash);
                //if (any_receive_block_withBlockTypehis_source == null)

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

            var trades_builder = Builders<Block>.Filter;
            var trades_filterDefinition = trades_builder.And(trades_builder.Eq("BlockType", BlockTypes.Trade), trades_builder.Eq("DestinationAccountId", AccountId));

            var trades = _blocks.Find(trades_filterDefinition).ToList();

            foreach (TradeBlock trade in trades)
            {
                var exec_builder = Builders<Block>.Filter;
                var exec_filterDefinition = exec_builder.And(exec_builder.Eq("BlockType", BlockTypes.ExecuteTradeOrder), exec_builder.Eq("TradeId", trade.Hash));
                var trade_execution = _blocks.Find(exec_filterDefinition);

                if (trade_execution.Any())
                    continue;

                var cancel_builder = Builders<Block>.Filter;
                var cancel_filterDefinition = cancel_builder.And(cancel_builder.Eq("BlockType", BlockTypes.CancelTradeOrder), cancel_builder.Eq("TradeOrderId", trade.TradeOrderId));
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

            var builder = Builders<Block>.Filter;
            var filterDefinition = builder.Eq("BlockType", BlockTypes.TradeOrder);
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

            var builder = Builders<Block>.Filter;
            var filterDefinition = builder.Eq("BlockType", BlockTypes.CancelTradeOrder);
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
            var builder = Builders<Block>.Filter;
            var filterDefinition = builder.Eq("BlockType", BlockTypes.ExecuteTradeOrder);
            var blocks = _blocks.Find(filterDefinition).ToList();

            foreach (ExecuteTradeOrderBlock block in blocks)
                list.Add(block.TradeOrderId);

            return list;
        }

        public async Task<bool> AddBlockAsync(Block block)
        {
            if (await FindBlockByHashAsync(block.Hash) != null)
            {
                _log.LogWarning("AccountCollection=>AddBlock: Block with such Hash already exists!");
                return false;
            }

            if(block is TransactionBlock)
            {
                var block1 = block as TransactionBlock;
                if (await FindBlockByIndexAsync(block1.AccountID, block1.Height) != null)
                {
                    _log.LogWarning("AccountCollection=>AddBlock: Block with such Index already exists!");
                    return false;
                }
            }

            //_log.LogInformation($"AddBlockAsync InsertOneAsync: {block.Height}");
            await _blocks.InsertOneAsync(block);
            return true;
        }

        public async Task RemoveBlockAsync(string hash)
        {
            var ret = await _blocks.DeleteOneAsync(a => a.Hash == hash);
            if (ret.IsAcknowledged && ret.DeletedCount == 1)
            {
                _log.LogWarning($"RemoveBlockAsync Block {hash} removed.");
            }
            else
                _log.LogWarning($"RemoveBlockAsync Block {hash} failed.");
        }

        public void Dispose()
        {
            // nothing to dispose
        }

        public async Task<bool> ConsolidateBlock(string hash)
        {
            var options = new FindOptions<Block, Block>
            {
                Limit = 1,
            };
            var filter = Builders<Block>.Filter.Eq("Hash", hash);

            var updateDef = Builders<Block>.Update.Set(o => o.Consolidated, true);
            var result = await _blocks.UpdateOneAsync(filter, updateDef);
            return result.ModifiedCount == 1;
        }

        public async Task<IEnumerable<string>> GetAllUnConsolidatedBlocks()
        {
            var options = new FindOptions<Block, BsonDocument>
            {
                Limit = 100,
                Sort = Builders<Block>.Sort.Ascending(o => o.TimeStamp),
                Projection = Builders<Block>.Projection.Include(a => a.Hash)
            };
            var filter = Builders<Block>.Filter.Eq("Consolidated", false);
            var result = await _blocks.FindAsync(filter, options);
            return (await result.ToListAsync()).Select(a => a["Hash"].AsString);
        }
    }
}