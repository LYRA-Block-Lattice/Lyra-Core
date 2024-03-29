using System;
using System.Collections.Generic;
using Lyra.Core.Blocks;
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
using Lyra.Core.API;
using Lyra.Data.API;
using Lyra.Data.Utils;
using System.Text.RegularExpressions;
using Lyra.Data.Blocks;
using Lyra.Core.Decentralize;
using System.Reflection;
using System.Globalization;
using Lyra.Data.API.WorkFlow;
using Lyra.Shared;
using Neo;
using Newtonsoft.Json;
using Lyra.Data.API.ODR;
using MongoDB.Driver.Linq;
using Lyra.Data.API.WorkFlow.UniMarket;
using System.Collections;
using Akka.Remote.Transport;
using System.Reflection.Metadata;
using System.IO;

namespace Lyra.Core.Accounts
{
    // this is account collection (collection of block chains) used on the node side only
    // 
    public class MongoAccountCollection : IAccountCollectionAsync
    {
        //private const string COLLECTION_DATABASE_NAME = "account_collection";
        private MongoClient _Client;

        private IMongoCollection<Block> _blocks;
        private IMongoCollection<TransactionBlock> _snapshots;
        private IMongoCollection<AccountChange> _accountChanges;

        readonly string _blocksCollectionName;
        readonly string _snapshotsCollectionName;
        readonly string _accountChangesCollectionName;

        IMongoDatabase _db;

        readonly string _DatabaseName;
        readonly ILogger _log;

        public string Cluster { get; set; }
        private string _networkId;

        public MongoAccountCollection(ILogger<MongoAccountCollection> logger)
            : this(Settings.Default.LyraNode.Lyra.Database.DBConnect,
                    Settings.Default.LyraNode.Lyra.Database.DatabaseName)
        {
            _log = logger;
        }

        public MongoAccountCollection(string connStr, string dbName)
        {
            _networkId = LyraNodeConfig.GetNetworkId();
            _Client = new MongoClient(connStr);
            _DatabaseName = dbName;
            _blocksCollectionName = $"{LyraNodeConfig.GetNetworkId()}_blocks";
            _snapshotsCollectionName = $"{LyraNodeConfig.GetNetworkId()}_snapshots";
            _accountChangesCollectionName = $"{LyraNodeConfig.GetNetworkId()}_acctchgs";

            // reset db every time for unit test.
            if (LyraNodeConfig.GetNetworkId() == "xtest")// || LyraNodeConfig.GetNetworkId() == "devnet")
            {
                if(File.Exists("c:\\tmp\\GetHashInput.txt"))
                    File.Delete("c:\\tmp\\GetHashInput.txt");
                if (GetClient() == null)
                    return;

                if (GetDatabase() == null)
                    return;

                var db = GetDatabase();

                if (db.ListCollectionNames().ToList().Contains(_blocksCollectionName))
                    db.DropCollection(_blocksCollectionName);
                if (db.ListCollectionNames().ToList().Contains(_snapshotsCollectionName))
                    db.DropCollection(_snapshotsCollectionName);
                if (db.ListCollectionNames().ToList().Contains(_accountChangesCollectionName))
                    db.DropCollection(_accountChangesCollectionName);
            }

            BsonSerializer.RegisterSerializer(typeof(DateTime), new DateTimeSerializer(DateTimeKind.Utc, BsonType.Document));
            BsonSerializer.RegisterSerializer(new EnumSerializer<PoDCatalog>(BsonType.String));

            BsonClassMap.RegisterClassMap<Block>(cm =>
            {
                cm.AutoMap();
                cm.SetIsRootClass(true);
            });

            BsonClassMap.RegisterClassMap<AccountChange>(cm =>
            {
                cm.AutoMap();
                cm.SetIsRootClass(false);
            });

            var alltypes = typeof(Block)
                .Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(Block)) && !t.IsAbstract);
            foreach (var type in alltypes)
            {
                Register(type);
            }

            _blocks = GetDatabase().GetCollection<Block>(_blocksCollectionName);
            _snapshots = GetDatabase().GetCollection<TransactionBlock>(_snapshotsCollectionName);
            _accountChanges = GetDatabase().GetCollection<AccountChange>(_accountChangesCollectionName);

            Cluster = GetDatabase().Client.Cluster.ToString();

            //async Task CreateCompoundIndex()
            //{
            //    // need to seprate different block into different collections.
            //    // svc, cons, tx
            //    //try
            //    //{
            //    //    var options = new CreateIndexOptions() { Unique = true };
            //    //    var field1 = new StringFieldDefinition<TransactionBlock>("AccountID");
            //    //    var field2 = new StringFieldDefinition<TransactionBlock>("Height");
            //    //    var indexDefinition = new IndexKeysDefinitionBuilder<TransactionBlock>()
            //    //        .Ascending(field1).Ascending(field2);
            //    //    var indexModel = new CreateIndexModel<TransactionBlock>(indexDefinition, options);
            //    //    await _blocks.OfType<TransactionBlock>().Indexes.CreateOneAsync(indexModel);
            //    //}
            //    //catch(Exception ex)
            //    //{

            //    //}
            //}

            async Task CreateIndexes<T>(IMongoCollection<T> colls, string columnName, bool uniq)
            {
                try
                {
                    var options = new CreateIndexOptions() { Unique = uniq };
                    var field = new StringFieldDefinition<T>(columnName);
                    var indexDefinition = new IndexKeysDefinitionBuilder<T>().Ascending(field);
                    var indexModel = new CreateIndexModel<T>(indexDefinition, options);
                    await colls.Indexes.CreateOneAsync(indexModel);
                }
                catch(Exception ex)
                {
                    if (ex.Message.Contains("already exists"))
                        return;
                    //await _blocks.Indexes.DropOneAsync(columnName + "_1");
                    //await CreateIndexes(columnName, uniq);
                }
            }

            async Task CreateNoneStringIndex<T>(IMongoCollection<T> colls, string colName, bool uniq)
            {
                try
                {
                    var options = new CreateIndexOptions() { Unique = uniq };
                    IndexKeysDefinition<T> keyCode = "{ " + colName + ": 1 }";
                    var codeIndexModel = new CreateIndexModel<T>(keyCode, options);
                    await colls.Indexes.CreateOneAsync(codeIndexModel);

                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("already exists"))
                        return;

                    await _blocks.Indexes.DropOneAsync(colName + "_1");
                    await CreateIndexes(colls, colName, uniq);
                }
            }

            _ = Task.Run(async () =>
            {
                //Console.WriteLine("ensure mongodb index...");

                try
                {
                    //await CreateCompoundIndex();

                    await CreateIndexes(_blocks, "_t", false);
                    await CreateIndexes(_blocks, "Hash", true);
                    await CreateIndexes(_blocks, "TimeStamp", false);
                    await CreateIndexes(_blocks, "TimeStamp.Ticks", false);
                    await CreateIndexes(_blocks, "PreviousHash", false);
                    await CreateIndexes(_blocks, "AccountID", false);
                    await CreateNoneStringIndex(_blocks, "Height", false);
                    await CreateNoneStringIndex(_blocks, "BlockType", false);

                    await CreateIndexes(_blocks, "SourceHash", false);
                    await CreateIndexes(_blocks, "DestinationAccountId", false);
                    await CreateIndexes(_blocks, "Ticker", false);
                    await CreateIndexes(_blocks, "VoteFor", false);

                    await CreateNoneStringIndex(_blocks, "OrderType", false);
                    await CreateIndexes(_blocks, "SellTokenCode", false);
                    await CreateIndexes(_blocks, "BuyTokenCode", false);
                    //await CreateIndexes(_blocks, "TradeOrderId", false);

                    await CreateIndexes(_blocks, "ImportedAccountId", false);

                    await CreateIndexes(_blocks, "Token0", false);
                    await CreateIndexes(_blocks, "Token1", false);
                    await CreateIndexes(_blocks, "RelatedTx", false);
                    await CreateIndexes(_blocks, "StakingAccountId", false);

                    // account changes
                    await CreateIndexes(_accountChanges, "AccountID", false);
                    await CreateIndexes(_accountChanges, "Time", false);
                    await CreateIndexes(_accountChanges, "TxHash", true);
                    await CreateNoneStringIndex(_accountChanges, "LyrChg", false);
                    await CreateNoneStringIndex(_accountChanges, "ConsHeight", false);

                    // snapshots
                    await CreateIndexes(_snapshots, "Hash", true);
                    await CreateIndexes(_snapshots, "AccountID", true);
                    await CreateIndexes(_snapshots, "BlockType", false);
                    await CreateIndexes(_snapshots, "OOStatus", false);
                    await CreateIndexes(_snapshots, "OTStatus", false);
                    await CreateIndexes(_snapshots, "Treasure", false);
                    await CreateIndexes(_snapshots, "UOStatus", false);
                    await CreateIndexes(_snapshots, "UTStatus", false);

                    // Uni Order/Trade


                    await SnapshotAllAsync();
                }
                catch(Exception e)
                {
                    Console.WriteLine("In create index: " + e.ToString());
                }
            }).ConfigureAwait(false);
        }

        //BsonClassMap.RegisterClassMap<UnStakingBlock>();
        private void Register(Type type)
        {
            if (BsonClassMap.IsClassMapRegistered(type))
                return;

            //will check if the type is registered. if not it will be automatically registered.
            //AutoMap will also called automatically.
            BsonClassMap.LookupClassMap(type);
        }

        private async Task<bool> SnapshotAllAsync()
        {
            var importedAccounts = FindAllImportedAccountID();

            var latests = _blocks
                .OfType<TransactionBlock>()
                .Aggregate()
                .SortByDescending(a => a.Height)
                .Group(a => a.AccountID,
                    g => new { g.Key, hash = g.First().Hash })

                // we got all hashes. so look for missing ones
                //.Lookup(_networkId + "_blocks", "hash", "Hash", "asBlock")
                // now we have all blocks
                .Lookup(_networkId + "_snapshots", "hash", "Hash", "as")
                //.Match()
                .ToList()
                .Where(a => a["as"].AsBsonArray.Capacity == 0);

            foreach(var acct in latests)
            {
                var block = await FindBlockByHashAsync(acct["hash"].AsString) as TransactionBlock;
                if (!importedAccounts.Contains(block.AccountID))
                    await UpdateSnapshotAsync(block);
            }
            return true;
        }

        public async Task UpdateStatsAsync()
        {
            await StopWatcher.TrackAsync(SnapshotAllAsync, "SnapshotAllAsync");

            var options = new FindOptions<AccountChange, AccountChange>
            {
                Limit = 1,
                Sort = Builders<AccountChange>.Sort.Descending(o => o.ConsHeight)
            };
            var filter = Builders<Block>.Filter;
            var filterDefination = filter.Eq("BlockType", BlockTypes.Service);

            long start = 1;
            var lastQ = await _accountChanges.FindAsync(new BsonDocument(), options);
            if(lastQ != null)
            {
                var last = await lastQ.FirstOrDefaultAsync();

                if (last != null)
                    start = last.ConsHeight + 1;
            }

            var endCons = await GetLastConsolidationBlockAsync();
            var end = endCons.Height;

            for(var i = start; i <= end; i++)
            {
                var acs = new List<AccountChange>();
                var cons = await FindConsolidationBlockByIndexAsync(i);
                if (cons == null)
                    return;

                foreach(var hash in cons.blockHashes)
                {
                    var blk = await FindBlockByHashAsync(hash);
                    if (blk == null)
                        return;

                    decimal chg = 0;
                    string acct;
                    if (blk is ReceiveTransferBlock recv)
                    {
                        acct = recv.AccountID;
                        if (recv.SourceHash == null) // genesis
                        {
                            if (recv is ReceiveNodeProfitBlock)
                            {
                                var prev = await FindBlockByHashAsync(recv.PreviousHash) as TransactionBlock;
                                var chgs = recv.GetBalanceChanges(prev);
                                if (chgs.Changes.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
                                    chg = chgs.Changes[LyraGlobal.OFFICIALTICKERCODE];
                            }
                            if (true == recv.Balances?.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
                                chg = recv.Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();
                        }
                        else
                        {
                            var srcblk = await FindBlockByHashAsync(recv.SourceHash);
                            if(srcblk is TransactionBlock send)
                            {
                                var sendPrev = await FindBlockByHashAsync(send.PreviousHash) as TransactionBlock;
                                var chgs = send.GetBalanceChanges(sendPrev);
                                if (chgs.Changes.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
                                    chg = chgs.Changes[LyraGlobal.OFFICIALTICKERCODE];
                            }
                            else
                            {
                                // the old fee block use service block as source
                                // treat as ReceiveNodeProfitBlock
                                var prev = await FindBlockByHashAsync(recv.PreviousHash) as TransactionBlock;
                                var chgs = recv.GetBalanceChanges(prev);
                                if (chgs.Changes.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
                                    chg = chgs.Changes[LyraGlobal.OFFICIALTICKERCODE];
                            }
                        }
                    }
                    else if (blk is SendTransferBlock send)
                    {
                        acct = send.AccountID;
                        var prev = await FindBlockByHashAsync(send.PreviousHash) as TransactionBlock;
                        var chgs = send.GetBalanceChanges(prev);
                        if (chgs.Changes.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
                            chg = -1 * chgs.Changes[LyraGlobal.OFFICIALTICKERCODE];
                    }
                    else if(blk is ServiceBlock || blk is ConsolidationBlock)
                    {
                        continue;
                    }
                    else if(blk is TokenMintBlock || blk is TokenBurnBlock)
                    {
                        continue;
                    }
                    else if (blk is FiatPrintBlock)
                    {
                        continue;
                    }
                    else
                    {
                        _log.LogCritical($"Unprocessed block type: {blk.BlockType} Height: {blk.Height}");
                        return; // just abort. no data cruption. (because of batch insert)
                    }

                    var ac = new AccountChange
                    {
                        Time = blk.TimeStamp,
                        AccountID = acct,
                        TxHash = blk.Hash,
                        LyrChg = chg,
                        ConsHeight = cons.Height
                    };
                    acs.Add(ac);
                    //if (ac.LyrChg < -100000)
                    //    ac.LyrChg += 1;       what's this???
                }
                if(acs.Count > 0)
                    await _accountChanges.InsertManyAsync(acs);
            }
        }

        /// <summary>
        /// Deletes all blocks and the block collection
        /// </summary>
        public void Delete(bool backup = true)
        {
            if (GetClient() == null)
                return;

            if (GetDatabase() == null)
                return;

            var db = GetDatabase();

            var backupName = _blocksCollectionName + "_backup";
            if (db.ListCollectionNames().ToList().Contains(backupName))
                db.DropCollection(backupName);

            if (backup)
                db.RenameCollection(_blocksCollectionName, backupName);
            //else
            //{
            //    if (db.ListCollectionNames().ToList().Contains(_blocksCollectionName))
            //        db.DropCollection(_blocksCollectionName);
            //}

            _blocks = db.GetCollection<Block>(_blocksCollectionName);

            db.DropCollection(_snapshotsCollectionName);
        }

        private MongoClient GetClient()
        {
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

        public Task<bool> AccountExistsAsync(string AccountId)
        {
            // 5ms
            //var options = new FindOptions<Block, Block>
            //{
            //    Limit = 1
            //};

            //var filter = Builders<Block>.Filter.Eq("AccountID", AccountId);
            //var result = await _blocks.FindAsync(filter, options);
            //return await result.AnyAsync();

            // max 18ms avg 0.68
            var q = _blocks.OfType<TransactionBlock>()
                .AsQueryable()
                .Where(a => a.AccountID == AccountId)
                .FirstOrDefault();
            return Task.FromResult(q != null);
        }

        public ServiceBlock GetLastServiceBlock()
        {
            var q = _blocks.OfType<ServiceBlock>()
                .AsQueryable()
                .OrderByDescending(a => a.Height)
                .FirstOrDefault();

            return q;
        }

        public Task<ServiceBlock> GetLastServiceBlockAsync()
        {
            // 31 ms
            //var q = _blocks.OfType<ServiceBlock>()
            //    .AsQueryable()
            //    .OrderByDescending(a => a.Height)
            //    .FirstOrDefault();

            //return Task.FromResult(q);

            // 2.5-3.2 ms
            var filter = Builders<Block>.Filter.Eq("BlockType", BlockTypes.Service);

            var block = _blocks.Find(filter)
                .SortByDescending(a => a.Height)
                .Limit(1)
                .FirstOrDefault();
            return Task.FromResult(block as ServiceBlock);

            //var filter = Builders<Block>.Filter;
            //var filterDefination = filter.Eq("BlockType", BlockTypes.Service);

            //var finds = await _blocks.FindAsync(filterDefination, options);
            //return await finds.FirstOrDefaultAsync() as ServiceBlock;
        }

        public Task<ConsolidationBlock> GetLastConsolidationBlockAsync()
        {
            var filter = Builders<Block>.Filter.Eq("BlockType", BlockTypes.Consolidation);

            var block = _blocks.Find(filter)
                .SortByDescending(a => a.Height)
                .Limit(1)
                .FirstOrDefault();
            return Task.FromResult(block as ConsolidationBlock);

            // 16 ms
            //var options = new FindOptions<Block, Block>
            //{
            //    Limit = 1,
            //    Sort = Builders<Block>.Sort.Descending(o => o.Height)
            //};
            //var filter = Builders<Block>.Filter.Eq("BlockType", BlockTypes.Consolidation);

            //var finds = await _blocks.FindAsync(filter, options);
            //var result = await finds.FirstOrDefaultAsync();
            //return result as ConsolidationBlock;

            // 11 ms
            //var block = _blocks.OfType<ConsolidationBlock>()
            //    .AsQueryable()
            //    .OrderByDescending(a => a.Height)
            //    .FirstOrDefault();
            //return Task.FromResult(block);

            // 16 ms
            //var filter = Builders<Block>.Filter.Eq("BlockType", BlockTypes.Consolidation);

            //var block = _blocks.Find(filter)
            //    .SortByDescending(a => a.Height)
            //    .Limit(1)
            //    .FirstOrDefault();
            //return Task.FromResult(block as ConsolidationBlock);
        }

        // max 30
        public async Task<List<ConsolidationBlock>> GetConsolidationBlocksAsync(long startHeight, int count)
        {
            var options = new FindOptions<ConsolidationBlock, ConsolidationBlock>
            {
                Limit = count > 30 ? 30 : count,
                Sort = Builders<ConsolidationBlock>.Sort.Ascending(o => o.Height)
            };
            var builder = Builders<ConsolidationBlock>.Filter;
            var filterDefinition = builder.And(builder.Eq("BlockType", BlockTypes.Consolidation),
                builder.Gte("Height", startHeight));

            var result = await _blocks.OfType<ConsolidationBlock>()
                .FindAsync(filterDefinition, options);
            return result.ToList();
        }

        public async Task<List<ConsolidationBlock>> GetConsolidationBlocksAsync(string belongToSvcHash)
        {
            var options = new FindOptions<Block, Block>
            {
                //Limit = 100,
                Sort = Builders<Block>.Sort.Ascending(o => o.Height)
            };
            var builder = Builders<Block>.Filter;
            var filterDefinition = builder.And(builder.Eq("BlockType", BlockTypes.Consolidation),
                builder.Eq("ServiceHash", belongToSvcHash));

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

        public Task<Block> FindLatestBlockAsync()
        {
            //var options = new FindOptions<Block, Block>
            //{
            //    Limit = 1,
            //    Sort = Builders<Block>.Sort.Descending(o => o.TimeStamp)
            //};

            //var result = await (await _blocks.FindAsync(FilterDefinition<Block>.Empty, options)).FirstOrDefaultAsync();
            //return result;

            var blk = _blocks.Find(FilterDefinition<Block>.Empty)
                .SortByDescending(a => a.TimeStamp)
                .Limit(1)
                .FirstOrDefault();
            return Task.FromResult(blk);
        }

        public async Task<Block> FindBlockByHeightAsync(string AccountId, long height)
        {
            var ftr = Builders<Block>.Filter;
            var def = ftr.And(ftr.Eq("AccountID", AccountId), ftr.Eq("Height", height));
            var blk = await _blocks.Find(def)
                .FirstOrDefaultAsync();
            return blk;
        }

        public Task<Block> FindLatestBlockAsync(string AccountId)
        {
            var blk = _blocks.Find(Builders<Block>.Filter.Eq("AccountID", AccountId))
                .SortByDescending(a => a.TimeStamp)
                .Limit(1)
                .FirstOrDefault();
            return Task.FromResult(blk);
        }
        public Block FindLatestBlock(string AccountId)
        {
            var blk = _blocks.Find(Builders<Block>.Filter.Eq("AccountID", AccountId))
                .SortByDescending(a => a.TimeStamp)
                .Limit(1)
                .FirstOrDefault();
            return blk;
        }

        public Task<Block> FindFirstBlockAsync(string AccountId)
        {
            //var options = new FindOptions<Block, Block>
            //{
            //    Limit = 1,
            //    Sort = Builders<Block>.Sort.Ascending(o => o.Height)
            //};
            var filter = Builders<Block>.Filter.Eq("AccountID", AccountId);

            var result = _blocks.Find(filter)
                .SortBy(a => a.Height)
                .Limit(1)
                .FirstOrDefault();
            return Task.FromResult(result);
        }

        public TransactionBlock FindFirstBlock(string AccountId)
        {
            var filter = Builders<Block>.Filter.Eq("AccountID", AccountId);

            var result = _blocks.Find(filter).SortBy(a => a.Height).FirstOrDefault();
            return result as TransactionBlock;
        }

        public async Task<TokenGenesisBlock> FindTokenGenesisBlockAsync(string? Hash, string Ticker)
        {
            //TokenGenesisBlock result = null;
            if (!string.IsNullOrEmpty(Hash))
            {
                var result = await (await _blocks.FindAsync(x => x.Hash == Hash)).FirstOrDefaultAsync();
                if (result != null)
                    return result as TokenGenesisBlock;
            }

            if (Ticker == null)
                return null;

            //var q = _blocks.OfType<TokenGenesisBlock>()
            //    .Find(a => a.Ticker == Ticker)
            //    .FirstOrDefault();

            //return q;
            var regexFilter = Regex.Escape(Ticker);
            var filter = Builders<TokenGenesisBlock>.Filter.Regex(u => u.Ticker, new BsonRegularExpression("/^" + regexFilter + "$/i"));
            var genResults = await _blocks.OfType<TokenGenesisBlock>()
                .FindAsync(filter);

             return genResults.FirstOrDefault();
        }

        public async Task<List<TokenGenesisBlock>> FindTokenGenesisBlocksAsync(string keyword)
        {
            // LJc... is a bot which created a lot of spam tokens
            //.Where(x => x.AccountID != "LJcP9ztmYqzjbSRsr2sKZ44pSkhqdtUp5g8YbgPQbxNPNf9FuQ93K1FQUSXYxcofZqgV8qgzWYXArjR9w9VPGBbENcS1Z3") // filter out trash token
            var builder = Builders<TokenGenesisBlock>.Filter;
            var filterDefinition =
                builder.And(
                    builder.Eq("BlockType", BlockTypes.TokenGenesis),
                    builder.Ne("AccountID", "LJcP9ztmYqzjbSRsr2sKZ44pSkhqdtUp5g8YbgPQbxNPNf9FuQ93K1FQUSXYxcofZqgV8qgzWYXArjR9w9VPGBbENcS1Z3"));
            var result = await _blocks.OfType< TokenGenesisBlock>()
                .FindAsync(filterDefinition);

            if (string.IsNullOrEmpty(keyword))
            {
                return result.ToList();
            }
            else
            {
                return result.ToList().Where(a => a.Ticker.Contains(keyword)).ToList();
            }
        }
        public async Task<List<TokenGenesisBlock>?> FindTokensForAccountAsync(string accountId, string keyword, string catalog)
        {
            var block = FindLatestBlock(accountId);
            if (block == null || block is not TransactionBlock tx)
                return null;

            var genss = new List<TokenGenesisBlock>();
            foreach(var b in tx.Balances.Where(a => a.Value > 0))   //  not need. we need 0 balance for fiat, etc.
            {
                if (catalog == "Fiat" && !b.Key.StartsWith("fiat/"))
                    continue;

                if(catalog == "TOT" && !(b.Key.StartsWith("tot/") || b.Key.StartsWith("svc/")))
                    continue;

                if(catalog == "NFT" && !b.Key.StartsWith("nft/"))
                    continue;

                if (catalog == "Token" && (b.Key.StartsWith("nft/") ||
                    b.Key.StartsWith("fiat/") ||
                    b.Key.StartsWith("tot/") ||
                    b.Key.StartsWith("svc/")
                    ))
                    continue;

                var gens = await FindTokenGenesisBlockAsync(null, b.Key);

                if (gens != null && (b.Key.IndexOf(keyword, 0, StringComparison.OrdinalIgnoreCase) != -1
                    || gens.Custom1?.IndexOf(keyword, 0, StringComparison.OrdinalIgnoreCase) != -1))
                {
                    genss.Add(gens);
                }                
            }

            return genss;
        }
        public async Task<List<TokenGenesisBlock>> FindTokensAsync(string keyword, string catalog)
        {
            var builder = Builders<TokenGenesisBlock>.Filter;

            FilterDefinition<TokenGenesisBlock> filter;
            if (catalog == "TOT" || catalog == "tot")
            {
                filter = builder.Regex(u => u.Ticker, new BsonRegularExpression("/^tot/"));
            }
            if (catalog == "Service" || catalog == "svc")
            {
                filter = builder.Regex(u => u.Ticker, new BsonRegularExpression("/^svc/"));
            }
            else if (catalog == "Fiat" || catalog == "fiat")
            {
                filter = builder.Regex(u => u.Ticker, new BsonRegularExpression("/^fiat/"));
            }
            else if (catalog == "NFT" || catalog == "nft")
            {
                filter = builder.Regex(u => u.Ticker, new BsonRegularExpression("/^nft/")
            );
            }
            else if (catalog == "Token" || catalog == "token" || string.IsNullOrWhiteSpace(catalog))
            {
                filter = builder.Not(
                    builder.Or(
                        builder.Regex(u => u.Ticker, new BsonRegularExpression("/^fiat/")),
                        builder.Regex(u => u.Ticker, new BsonRegularExpression("/^tot/")),
                        builder.Regex(u => u.Ticker, new BsonRegularExpression("/^nft/")),
                        builder.Regex(u => u.Ticker, new BsonRegularExpression("/^svc/"))
                        )

                );
            }
            else
            {
                throw new InvalidOperationException("Unknown token catalog.");
                //filter = builder.Or(
                //    builder.Regex(u => u.Ticker, new BsonRegularExpression("/" + regexFilter + "/i")),
                //    builder.Regex(u => u.Custom1, new BsonRegularExpression("/" + regexFilter + "/i"))
                //    );
            }

            FilterDefinition<TokenGenesisBlock> finalFilter;
            if (string.IsNullOrWhiteSpace(keyword))
            {
                finalFilter = filter;
            }
            else
            {
                var regexFilter = new BsonRegularExpression($"/{Regex.Escape(keyword)}/i");
                finalFilter =
                    builder.And(filter,
                                builder.Or(builder.Regex(u => u.Ticker, regexFilter),
                                            builder.Regex(u => u.Custom1, regexFilter))
                                );
            }

            var genResult = await _blocks.OfType<TokenGenesisBlock>()
                .Find(finalFilter)
                .Limit(200)
                .ToListAsync();

            return genResult;
        }

        public async Task<List<TransactionBlock>> FindDaosAsync(string keyword)
        {
            List<DaoGenesisBlock> result = new List<DaoGenesisBlock>();

            if(string.IsNullOrWhiteSpace(keyword))
            {
                var genResult = await _blocks.OfType<DaoGenesisBlock>()
                    .Find(FilterDefinition<DaoGenesisBlock>.Empty)
                    .Limit(200)
                    .ToListAsync();
                result = genResult;
            }
            else
            {
                var regexFilter = Regex.Escape(keyword);
                var builder = Builders<DaoGenesisBlock>.Filter;

                FilterDefinition<DaoGenesisBlock> filter = builder.Regex(u => u.Name, new BsonRegularExpression("/" + regexFilter + "/i"));

                var genResult = await _blocks.OfType<DaoGenesisBlock>()
                    .Find(filter)
                    .Limit(200)
                    .ToListAsync();

                result = genResult;
            }

            List<TransactionBlock> daos = new List<TransactionBlock>();
            foreach (var dao in result)
            {
                if(dao.Name != "Lyra Guild")
                {
                    var daoBlock = FindLatestBlock(dao.AccountID) as TransactionBlock;
                    if (daoBlock != null)
                    {
                        daos.Add(daoBlock);
                    }
                }
            }
            return daos;
        }

        private IEnumerable<String> FindAllImportedAccountID()
        {
            var p1 = new BsonArray
            {
                BlockTypes.ImportAccount,
                BlockTypes.OpenAccountWithImport
            };

            var builder = Builders<ImportAccountBlock>.Filter;
            var filterDefinition = builder.In("BlockType", p1);

            var result = _blocks.OfType<ImportAccountBlock>()
                .Aggregate()
                .Match(filterDefinition)
                .Project(x => new { x.ImportedAccountId })
                .ToEnumerable()
                .Select(a => a.ImportedAccountId);

            return result;
        }

        public Task<bool> WasAccountImportedAsync(string ImportedAccountId)
        {
            var p1 = new BsonArray
            {
                BlockTypes.ImportAccount,
                BlockTypes.OpenAccountWithImport
            };

            var builder = Builders<Block>.Filter;
            var filterDefinition = builder.And(builder.In("BlockType", p1), builder.And(builder.Eq("ImportedAccountId", ImportedAccountId)));

            var result = _blocks.Find(filterDefinition).FirstOrDefault();

            return Task.FromResult(result != null);
        }

        public async Task<bool> WasAccountImportedAsync(string ImportedAccountId, string AccountId)
        {
            var p1 = new BsonArray
            {
                BlockTypes.ImportAccount,
                BlockTypes.OpenAccountWithImport
            };

            var builder = Builders<Block>.Filter;
            var filterDefinition = builder.And(builder.In("BlockType", p1), builder.And(builder.Eq("ImportedAccountId", ImportedAccountId)));

            var result = await (await _blocks.FindAsync(filterDefinition)).FirstOrDefaultAsync();
            if (result == null)
                return false;

            return (result as ImportAccountBlock).AccountID == AccountId;
        }

        public Block FindBlockByHash(string hash)
        {
            if (string.IsNullOrEmpty(hash))
                return null;

            var filter = Builders<Block>.Filter.Eq("Hash", hash);

            var block = _blocks.Find(filter).FirstOrDefault();
            return block;
        }

        public Task<Block> FindBlockByHashAsync(string hash)
        {
            //if (string.IsNullOrEmpty(hash))
            //    return Task.FromResult((Block)null);

            //var blk = _blocks
            //        .AsQueryable()
            //        .Where(a => a.Hash == hash)
            //        .FirstOrDefault();

            return Task.FromResult(FindBlockByHash(hash));

            //if (string.IsNullOrEmpty(hash))
            //    return null;

            //var options = new FindOptions<Block, Block>
            //{
            //    Limit = 1,
            //};
            //var filter = Builders<Block>.Filter.Eq("Hash", hash);

            //var block = await (await _blocks.FindAsync(filter, options)).FirstOrDefaultAsync();
            //return block;
        }

        private async Task<List<Block>> GetBlocksInConsByHeightAsync(long height)
        {
            var cons = await FindConsolidationBlockByIndexAsync(height);
            if (cons == null)
                return new List<Block>();

            // all blocks, append with the consolidation block
            PipelineDefinition<Block, BsonDocument> pipeline = new BsonDocument[]
            {
                new BsonDocument("$match",
                    new BsonDocument("Hash", cons.Hash)),
                new BsonDocument("$lookup",
                    new BsonDocument
                    {
                        { "from", _networkId + "_blocks" },
                        { "localField", "blockHashes" },
                        { "foreignField", "Hash" },
                        { "as", "blks" }
                    }),
                new BsonDocument("$unwind",
                    new BsonDocument("path", "$blks")),
                new BsonDocument("$replaceRoot",
                    new BsonDocument("newRoot", "$blks")),
                new BsonDocument("$sort", new BsonDocument("TimeStamp", 1))
            };

            var q1 = _blocks.Aggregate(pipeline);

            var x = await q1.ToListAsync();

            return x.Select(a => BsonSerializer.Deserialize<Block>(a))           
                .Append(cons)
                .ToList();
        }
        public async Task<List<Block>> GetMultipleConsByHeightAsync(long height, int count)
        {
            var list = new List<Block>();
            int total = 0;
            int max = (count < 1 || count > 100) ? 100 : count;
            for(var i = 0; i < max; i++)
            {
                var blklist = await GetBlocksInConsByHeightAsync(height + i);
                if (!blklist.Any())
                    break;

                if (list.Count == 0)
                    list.AddRange(blklist);
                else
                    list.AddRange(blklist.Skip(1));

                if (list.Count >= max)
                    break;
            }
            return list;
        }


        public async Task<Block> FindBlockByHashAsync(string AccountId, string hash)
        {
            var builder = Builders<Block>.Filter;
            var filterDefinition = builder.And(builder.Eq("Hash", hash), builder.Eq("AccountID", AccountId));

            var block = await (await _blocks.FindAsync(filterDefinition)).FirstOrDefaultAsync();
            return block as TransactionBlock;
        }

        public async Task<List<NonFungibleToken>> GetIssuedNFTInstancesAsync(bool GetOnlySendBlocks, string AccountId, string TokenCode)
        {
            var p1 = new BsonArray
            {
                BlockTypes.SendTransfer,
                BlockTypes.ExecuteTradeOrder
            };

            if (!GetOnlySendBlocks)
            {
                p1.Add(BlockTypes.ReceiveTransfer);
                p1.Add(BlockTypes.OpenAccountWithReceiveTransfer);
            }

            var builder = Builders<Block>.Filter;
            var filterDefinition = builder.And(builder.In("BlockType", p1), builder.And(builder.Eq("AccountID", AccountId), builder.Ne("NonFungibleToken", BsonNull.Value)));

            var find_result = await _blocks.FindAsync(filterDefinition);
            if (find_result == null)
                return null;

            var block_list = await find_result.ToListAsync();

            var the_list = new List<NonFungibleToken>();

            foreach (TransactionBlock block in block_list)
                if (block.NonFungibleToken.TokenCode == TokenCode)
                    the_list.Add(block.NonFungibleToken);

            if (the_list.Count > 0)
                return the_list;

            return null;
        }



        // returns true if the account owns an instance of collectible NFT with given ticker and serial number
        public async Task<bool> DoesAccountHaveCollectibleNFTInstanceAsync(string owner_account_id, TokenGenesisBlock token_block, string serial_number)
        {
            var non_fungible_tokens = await GetIssuedNFTInstancesAsync(GetOnlySendBlocks: false, owner_account_id, token_block.Ticker);
            if (non_fungible_tokens == null)
                return false;

            int block_count = 0;

            foreach (var nft in non_fungible_tokens)
                if (nft.SerialNumber == serial_number)
                    block_count++;

            // the issuer's account has one extra send block created when the NFT instance was issued 
            if (owner_account_id == token_block.AccountID)
                block_count--;

            if (block_count <= 0)
                return false;


            // even number (block_count mod 2 result is zero) means that there was at least a couple of blocks with the serila number, 
            // which means that there was receive and send for the same serial number. So the token was received and sent to another account.So there is no token on this account.
            if (block_count % 2 == 0)
                return false;
            return true;
        }


        public async Task<List<NonFungibleToken>> GetNonFungibleTokensAsync(string AccountId)
        {
            var p1 = new BsonArray
            {
                BlockTypes.ReceiveTransfer,
                BlockTypes.OpenAccountWithReceiveTransfer
            };
            //p1.Add(BlockTypes.OpenAccountWithImport);

            var builder = Builders<Block>.Filter;
            var filterDefinition = builder.And(builder.In("BlockType", p1), builder.And(builder.Eq("AccountID", AccountId), builder.Ne("NonFungibleToken", BsonNull.Value)));

            var allNonFungibleReceiveBlocks = await (await _blocks.FindAsync(filterDefinition)).ToListAsync();

            var the_list = new List<NonFungibleToken>();

            foreach (TransactionBlock receiveBlock in allNonFungibleReceiveBlocks)
            {
                var token_genesis = await FindTokenGenesisBlockAsync(null, receiveBlock.NonFungibleToken.TokenCode);
                if (token_genesis == null)
                    continue;
                if (token_genesis.ContractType != ContractTypes.Collectible)
                {
                    the_list.Add(receiveBlock.NonFungibleToken);
                }
                else // for collecvtible NFT we need to make sure the account still contain the instance (it wasn't send to different account) as only one acount can own the NFT instance
                { 
                    bool is_owner = await DoesAccountHaveCollectibleNFTInstanceAsync(AccountId, token_genesis, receiveBlock.NonFungibleToken.SerialNumber);
                    if (is_owner && !the_list.ContainsNFTInstance(receiveBlock.NonFungibleToken))
                        the_list.Add(receiveBlock.NonFungibleToken);
                }
            }

            if (the_list.Count > 0)
                return the_list;

            return null;
        }

        public Task<TransactionBlock> FindBlockByPreviousBlockHashAsync(string previousBlockHash)
        {
            var builder = Builders<Block>.Filter;
            var filterDefinition = builder.Eq("PreviousHash", previousBlockHash);
            var result = _blocks.Find(filterDefinition)
                .Limit(1)
                .FirstOrDefault();

            return Task.FromResult(result as TransactionBlock);
        }

        /// <summary>
        /// Ignores fee blocks!
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        public Task<ReceiveTransferBlock> FindBlockBySourceHashAsync(string hash)
        {
            var blk = _blocks.OfType<ReceiveTransferBlock>()
                    .AsQueryable()
                    .Where(a => a.SourceHash == hash 
                        && a.BlockType != BlockTypes.OpenAccountWithReceiveFee 
                        && a.BlockType != BlockTypes.ReceiveFee)
                    .FirstOrDefault();

            return Task.FromResult(blk);

            //var builder = Builders<Block>.Filter;
            //var filterDefinition = builder.Eq("SourceHash", hash);

            //var result = await (await _blocks.FindAsync(filterDefinition)).ToListAsync();

            //foreach (var block in result)
            //{
            //    if (block.BlockType == BlockTypes.OpenAccountWithReceiveFee || block.BlockType == BlockTypes.ReceiveFee)
            //        continue;
            //    else
            //        return block as ReceiveTransferBlock;
            //}
            //return null;
        }

        public Task<List<Block>> FindBlocksByRelatedTxAsync(string hash)
        {
            //var options = new FindOptions<Block, Block>
            //{
            //    Limit = 1,
            //};
            var builder = new FilterDefinitionBuilder<Block>();
            var filterDefinition = builder.Eq("RelatedTx", hash);

            var result = _blocks
                .Find(filterDefinition)
                .ToList();

            return Task.FromResult(result);
        }

        public Task<TransactionBlock?> FindBlockByIndexAsync(string AccountId, long index)
        {
            var builder = new FilterDefinitionBuilder<Block>();
            var filterDefinition = builder.And(builder.Eq("AccountID", AccountId),
                builder.Eq("Height", index));

            var block = _blocks.Find(filterDefinition).FirstOrDefault();
            return Task.FromResult(block as TransactionBlock);
        }

        public TransactionBlock? FindBlockByIndex(string AccountId, long index)
        {
            var q = _blocks.OfType<TransactionBlock>()
                .Find(a => a.AccountID == AccountId && a.Height == index)
                .FirstOrDefault();

            return q;
        }

        public async Task<ServiceBlock> FindServiceBlockByIndexAsync(Int64 index)
        {
            var options = new FindOptions<ServiceBlock, ServiceBlock>
            {
                Limit = 1,
            };
            var builder = new FilterDefinitionBuilder<ServiceBlock>();
            var filterDefinition = builder.Eq("Height", index);

            var result = await _blocks
                .OfType<ServiceBlock>()
                .FindAsync(filterDefinition, options);

            return await result.FirstOrDefaultAsync();
        }

        public async Task<ConsolidationBlock> FindConsolidationBlockByIndexAsync(Int64 index)
        {
            var options = new FindOptions<ConsolidationBlock, ConsolidationBlock>
            {
                Limit = 1,
            };
            var builder = new FilterDefinitionBuilder<ConsolidationBlock>();
            var filterDefinition = builder.Eq("Height", index);

            var result = await _blocks
                .OfType<ConsolidationBlock>()
                .FindAsync(filterDefinition, options);

            return await result.FirstOrDefaultAsync();
        }

        private ReceiveTransferBlock FindLastReceiveBlock(string AccountId)
        {
            // must exclude token genesis
            // fees also has no sourcehash. consider it.
            var options = new FindOptions<ReceiveTransferBlock, ReceiveTransferBlock>
            {
                Limit = 1,
                Sort = Builders<ReceiveTransferBlock>.Sort.Descending(o => o.Height)
            };

            var builder1 = Builders<ReceiveTransferBlock>.Filter;
            var filterDefinition1 = builder1.And(
                builder1.Eq("AccountID", AccountId),
                builder1.Ne<string>("SourceHash", null));   // filter tokengenesis and receive node fee block

            var finds = _blocks.OfType<ReceiveTransferBlock>()
                .Find(filterDefinition1)
                .SortByDescending(a => a.Height)
                .Limit(1)
                .FirstOrDefault();

            return finds;
        }

        public async Task<SendTransferBlock> FindUnsettledSendBlockAsync(string AccountId)
        {
            //if (await WasAccountImportedAsync(AccountId))
            //    return null;

            // First  let's check the "main" account
            var send_block = await FindUnsettledSendBlockByDestinationAccountIdAsync(AccountId);
            if (send_block != null)
                return send_block;

            // Now let's check if there is anything sent to the imported accounts linked to this account
            var import_blocks = await GetImportedAccountBlocksAsync(AccountId);
            //if (import_blocks == null || import_blocks.Count == 0)
            //    return null;

            foreach (ImportAccountBlock importBlock in import_blocks)
            {
                send_block = await FindUnsettledSendBlockForImportedAccountAsync(importBlock.ImportedAccountId, AccountId);
                if (send_block != null)
                    return send_block;
            }

            return null;
        }

        // look up by destination account
        public async Task<SendTransferBlock> FindUnsettledSendBlockByDestinationAccountIdAsync(string AccountId)
        {
            // assuming all receive are based on time order, so we can do shallow scan to save resources.
            // default do 'shallow' scan.
            // will implement deep scan in future.
            // get last settled receive block
            var timeToScan = DateTime.MinValue;
            var lastRecvBlock = FindLastReceiveBlock(AccountId);
            if (lastRecvBlock != null)
            {
                var send = FindBlockByHash(lastRecvBlock.SourceHash);
                if(send != null)    // genesis has no send
                    timeToScan = send.TimeStamp;
            }

            // First, let find all send blocks:
            // (It can be optimzed as it's going to be growing, so it can be called with munimum Service Chain Height parameter to look only for recent blocks) 
            var options = new FindOptions<Block, Block>
            {
                Limit = 10,
                Sort = Builders<Block>.Sort.Ascending(o => o.TimeStamp)
            };

            var builder = Builders<Block>.Filter;
            var filterDefinition = builder.And(builder.Gt("TimeStamp", timeToScan), builder.Eq("DestinationAccountId", AccountId));

            var allSendBlocks = await (await _blocks.FindAsync(filterDefinition, options)).ToListAsync();

            foreach (SendTransferBlock sendBlock in allSendBlocks)
            {
                //// Now, let's try to fetch the corresponding receive block:
                //var p1 = new BsonArray();
                //p1.Add((int)BlockTypes.ReceiveTransfer);
                //p1.Add((int)BlockTypes.OpenAccountWithReceiveTransfer);
                
                //var builder1 = Builders<Block>.Filter;
                //var filterDefinition1 = builder1.And(builder1.In("BlockType", p1), builder1.And(builder1.Eq("AccountID", AccountId), builder1.Eq("SourceHash", sendBlock.Hash)));

                //var result = await (await _blocks.FindAsync(filterDefinition1)).FirstOrDefaultAsync();

                var result = await FindReceiveBlockAsync(AccountId, sendBlock.Hash);

                if (result == null)
                    return sendBlock;
            }
            return null;
        }

        // look up by receive blocks that were sent to imported account
        private async Task<SendTransferBlock> FindUnsettledSendBlockForImportedAccountAsync(string ImportedAccountId, string AccountId)
        {

            // First, let find all send blocks:
            // (It can be optimzed as it's going to be growing, so it can be called with munimum Service Chain Height parameter to look only for recent blocks) 
            var builder = Builders<Block>.Filter;
            var filterDefinition = builder.Eq("DestinationAccountId", ImportedAccountId);
            var allSendBlocks = await (await _blocks.FindAsync(filterDefinition)).ToListAsync();

            foreach (SendTransferBlock sendBlock in allSendBlocks)
            {
                //// Now, let's try to fetch the corresponding receive block:
                var result = await FindReceiveBlockAsync(AccountId, sendBlock.Hash);
                if (result == null)
                {
                    // let's make sure this transfer was not received BEFORE the account was imported!
                    result = await FindReceiveBlockAsync(ImportedAccountId, sendBlock.Hash);
                    if (result == null)
                        return sendBlock;
                }
            }
            return null;
        }

        private async Task<ReceiveTransferBlock> FindReceiveBlockAsync(string AccountId, string SourceHash)
        {
            var builder1 = Builders<ReceiveTransferBlock>.Filter;
            var filterDefinition1 = builder1.And(builder1.Eq("AccountID", AccountId), builder1.Eq("SourceHash", SourceHash));

            var finds = await _blocks.OfType<ReceiveTransferBlock>()
                .FindAsync(filterDefinition1);

            return await finds.FirstOrDefaultAsync();
        }

        // Check if the account has any imported accounts and return the list of them if they exist
        public async Task<List<Block>> GetImportedAccountBlocksAsync(string AccountId)
        {
            var p1 = new BsonArray
            {
                (int)BlockTypes.ImportAccount,
                (int)BlockTypes.OpenAccountWithImport
            };

            var builder = Builders<Block>.Filter;
            var filterDefinition = builder.Eq("AccountID", AccountId) & builder.In("BlockType", p1);

            var import_blocks = await(await _blocks.FindAsync(filterDefinition)).ToListAsync();
            return import_blocks;
        }

        public async Task<UnSettledFees> FindUnsettledFeesAsync(string AuthorizerAccountId, string pftid)
        {
            // !!! TO DO - take care of fees for imported accounts!!!!
            // get the latest feeblock
            // get all new service since the latest feeblock
            long startHeight = 1;

            if (_networkId == "testnet")
                startHeight = 4800;
            else if (_networkId == "mainnet")
                startHeight = 8888;

            var options = new FindOptions<ReceiveNodeProfitBlock, ReceiveNodeProfitBlock>
            {
                Limit = 1,
                Sort = Builders<ReceiveNodeProfitBlock>.Sort.Descending(o => o.Height)
            };
            var builder = new FilterDefinitionBuilder<ReceiveNodeProfitBlock>();
            var filterDefinition = builder.Eq("AccountID", pftid);

            long fromHeight = 1;
            var latestFb = await (await _blocks
                .OfType<ReceiveNodeProfitBlock>()
                .FindAsync(filterDefinition, options))
                .FirstOrDefaultAsync();
            if (latestFb != null)
            {
                fromHeight = latestFb.ServiceBlockEndHeight + 1;
            }

            if (fromHeight < startHeight)
                fromHeight = startHeight;

            var endHeight = (await GetLastServiceBlockAsync()).Height;

            if (endHeight < fromHeight)
                return null;

            return await FindUnsettledFeesAsync(AuthorizerAccountId, pftid, fromHeight, endHeight);
        }

        public async Task<UnSettledFees> FindUnsettledFeesAsync(string AuthorizerAccountId, string pftid, long fromHeight, long endHeight)
        {
            var builder2 = new FilterDefinitionBuilder<ServiceBlock>();
            //var builder3 = new FilterDefinitionBuilder<KeyValuePair<string, string>>();
            //var nodeFilter = builder2.ElemMatch("Authorizers", builder3.Eq("k", AuthorizerAccountId));
            var heightFilter = builder2.Gte("Height", fromHeight);
            var heightFilter2 = builder2.Lte("Height", endHeight);
            //            var feeFilter = builder2.Gt("FeesGenerated", LyraGlobal.MAXIMUM_AUTHORIZERS);    // make sure that every node has a minimal share

            var options2 = new FindOptions<ServiceBlock, ServiceBlock>
            {
                Limit = 1024,
                Sort = Builders<ServiceBlock>.Sort.Ascending(o => o.Height)
            };

            var sbs = await _blocks
                .OfType<ServiceBlock>()
                .FindAsync(builder2.And(heightFilter, heightFilter2), options2);

            var sblist = sbs.ToList();

            long lastSbHeight = fromHeight;
            decimal totalFees = 0;

            for (int i = 0; i < sblist.Count - 1; i++)
            {
                if (sblist[i].Authorizers.ContainsKey(AuthorizerAccountId))
                {
                    if (sblist[i + 1].FeesGenerated > sblist[i].Authorizers.Count)
                    {
                        totalFees += sblist[i + 1].FeesGenerated.ToBalanceDecimal() / sblist[i].Authorizers.Count;
                    }
                    lastSbHeight = sblist[i].Height;
                }
            }

            if (totalFees > 0)
            {
                return new UnSettledFees
                {
                    AccountId = pftid,
                    ServiceBlockStartHeight = fromHeight,
                    ServiceBlockEndHeight = lastSbHeight,
                    TotalFees = totalFees
                };
            }
            else
            {
                return null;
            }
        }

        public async Task<decimal> GetPendingReceiveAsync(string accountId)
        {
            var timeToScan = DateTime.MinValue;
            var lastRecvBlock = FindLastReceiveBlock(accountId);
            if (lastRecvBlock != null)
            {
                var send = await FindBlockByHashAsync(lastRecvBlock.SourceHash);
                if (send != null)    // genesis has no send
                    timeToScan = send.TimeStamp;
            }

            var q1 = _blocks.OfType<SendTransferBlock>()
                .Aggregate()
                .Match(a => a.TimeStamp > timeToScan && a.DestinationAccountId == accountId)
                .Lookup(_networkId + "_blocks", "Hash", "SourceHash", "asSource")
                .Project(x => new
                {
                    SendHash = x["Hash"],
                    DstAccountId = x["DestinationAccountId"],
                    RecvHash = x["asSource"],
                    Time = x["TimeStamp"]
                });

            var x = q1.ToList();

            var amts = from s in x
                       join a in _accountChanges.AsQueryable() on s.SendHash equals a.TxHash
                       select a.LyrChg;

            var total = -1 * amts.Sum();        // send block has neg values
            return total;
        }

        public async Task<PendingStats> GetPendingStatsAsync(string accountId)
        {
            decimal pfee = 0;
            var pfts = await FindAllProfitingAccountForOwnerAsync(accountId);
            var pft = pfts.Where(a => a.PType == ProfitingType.Node).FirstOrDefault();
            if (pft != null)
            {
                var uf = await FindUnsettledFeesAsync(accountId, pft.AccountID);
                pfee = uf.TotalFees;
            }
            var ps = new PendingStats
            {
                AccountId = accountId,
                PendingFunds = Math.Round(await GetPendingReceiveAsync(accountId), 8),
                PendingFees = Math.Round(pfee, 8)
            };
            return ps;
        }
        /*
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

        public async Task<List<TradeOrderBlock>> GetSellTradeOrdersAsync(string SellTokenCode, string BuyTokenCode)
        {
            var list = new List<TradeOrderBlock>();

            var options = new FindOptions<Block, Block>
            {
                Limit = 1000,
                Sort = Builders<Block>.Sort.Descending(o => o.TimeStamp)
            };

            var builder = Builders<Block>.Filter;

            var filterDefinition = builder.And(builder.Eq("BlockType", BlockTypes.TradeOrder), builder.Eq("OrderType", TradeOrderTypes.Sell), builder.Eq("SellTokenCode", SellTokenCode), builder.Eq("BuyTokenCode", BuyTokenCode));

            var trade_blocks = await _blocks.Find(filterDefinition).ToListAsync();

            foreach (TradeOrderBlock block in trade_blocks)
                list.Add(block);

            return list;
        }

        public async Task<List<TradeOrderBlock>> GetSellTradeOrdersForTokenAsync(string BuyTokenCode)
        {
            var list = new List<TradeOrderBlock>();

            var options = new FindOptions<Block, Block>
            {
                Limit = 1000,
                Sort = Builders<Block>.Sort.Descending(o => o.TimeStamp) 
            };

            var builder = Builders<Block>.Filter;

            var filterDefinition = builder.And(builder.Eq("BlockType", BlockTypes.TradeOrder), builder.Eq("OrderType", TradeOrderTypes.Sell), builder.Eq("BuyTokenCode", BuyTokenCode));

            var trade_blocks = await _blocks.Find(filterDefinition).ToListAsync();

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

        public async Task<CancelTradeOrderBlock> GetCancelTradeOrderBlockAsync(string TradeOrderId)
        {
            var builder = Builders<Block>.Filter;
            var filterDefinition = builder.And(builder.Eq("BlockType", BlockTypes.CancelTradeOrder), builder.Eq("TradeOrderId", TradeOrderId));
            var block = await _blocks.Find(filterDefinition).FirstOrDefaultAsync();
            return block as CancelTradeOrderBlock;
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

        public async Task<ExecuteTradeOrderBlock> GetExecuteTradeOrderBlockAsync(string TradeOrderId)
        {
            var builder = Builders<Block>.Filter;
            var filterDefinition = builder.And(builder.Eq("BlockType", BlockTypes.ExecuteTradeOrder), builder.Eq("TradeOrderId", TradeOrderId));
            var block = await _blocks.Find(filterDefinition).FirstOrDefaultAsync();
            return block as ExecuteTradeOrderBlock;
        }

        */

        private async Task UpdateSnapshotAsync(TransactionBlock tx)
        {
            await _snapshots.ReplaceOneAsync(p => p.AccountID == tx.AccountID,
                tx,
                new ReplaceOptions { IsUpsert = true });
        }

        public async Task<bool> DupCheckAsync(Block block)
        {
            // make it one step operation
            FilterDefinition<Block> query;
            if (block is TransactionBlock tx)
            {
                var filter = Builders<Block>.Filter;
                query = filter.Or(
                        filter.Eq("Hash", block.Hash),
                        filter.And(
                            filter.Eq("AccountID", tx.AccountID),
                            filter.Eq("Height", block.Height))
                        );
            }
            else
            {
                var filter = Builders<Block>.Filter;
                query = filter.Or(
                        filter.Eq("Hash", block.Hash),
                        filter.And(
                            filter.Eq("BlockType", block.BlockType),
                            filter.Eq("Height", block.Height))
                        );
            }

            var results = await _blocks.FindAsync(query);
            return results.Any();
        }

        public async Task<bool> AddBlockAsync(Block block)
        {
            // make it one step operation
            FilterDefinition<Block> query;
            if (block is TransactionBlock tx)
            {
                var filter = Builders<Block>.Filter;
                query = filter.Or(
                        filter.Eq("Hash", block.Hash),
                        filter.And(
                            filter.Eq("AccountID", tx.AccountID),
                            filter.Eq("Height", block.Height))
                        );
            }
            else
            {
                var filter = Builders<Block>.Filter;
                query = filter.Or(
                        filter.Eq("Hash", block.Hash),                    
                        filter.And(
                            filter.Eq("BlockType", block.BlockType),
                            filter.Eq("Height", block.Height))
                        );
            }

            var exists = _blocks.Find(query);
            if(exists.Any())
            {
                if(block.Hash != "8PjzRhffAeUXLzzkLabcrukoSjvRpmGzzzCf9Xuwn8sU" && block.Hash != "2hLjaKdW1EMqyMaQ6LgK5nZWSuJrZ25W9HVJo61NyvNB")    //mainnet wrong svc block
                {
                    // hack: testnet db error, dup service block height 8896
                    if (_networkId == "testnet" && block.BlockType == BlockTypes.Service && block.Height <= 8896)
                    {
                        // no action, just add it.
                        _log.LogWarning($"AccountCollection=>AddBlock: Service {block.Height} dup tolerant");
                    }
                    else
                    {
                        var blk = exists.FirstOrDefault();
                        _log.LogWarning($"AccountCollection=>AddBlock: Block exists! {block.BlockType}, {block.Hash}");
                        _log.LogWarning($"AccountCollection=>AddBlock: Existing one: {blk.BlockType}, {blk.Hash}, {blk.Signature}");
                        return false;
                    }
                }
            }

            try
            {
                _blocks.InsertOne(block);

                if(block is TransactionBlock t)
                    await UpdateSnapshotAsync(t);

                return true;
            }
            catch(Exception e)
            {
                _log.LogWarning($"AccountCollection=>AddBlock: {block.BlockType}, {block.Hash}, {e.Message}");
                return false;
            }            
        }

        public async Task RemoveBlockAsync(string hash)
        {
            var ret = await _blocks.DeleteOneAsync(a => a.Hash == hash);
            if (ret.IsAcknowledged && ret.DeletedCount == 1)
            {
                _log.LogWarning($"RemoveBlockAsync Block {hash} removed.");
                await _snapshots.DeleteOneAsync(a => a.Hash == hash);
            }
            else
                _log.LogWarning($"RemoveBlockAsync Block {hash} failed.");
        }

        public void Dispose()
        {
            // nothing to dispose
        }

        //public async Task<bool> ConsolidateBlock(string hash)
        //{
        //    var options = new FindOptions<Block, Block>
        //    {
        //        Limit = 1,
        //    };
        //    var filter = Builders<Block>.Filter.Eq("Hash", hash);

        //    var updateDef = Builders<Block>.Update.Set(o => o.Consolidated, true);
        //    var result = await _blocks.UpdateOneAsync(filter, updateDef);
        //    return result.ModifiedCount == 1;
        //}

        public async Task<List<Block>> GetAllUnConsolidatedBlocksAsync()
        {
            var options = new FindOptions<Block, Block>
            {
                Limit = 1000,
                Sort = Builders<Block>.Sort.Ascending(o => o.TimeStamp),
                Projection = Builders<Block>.Projection.Include(a => a.Hash)
            };
            var filter = Builders<Block>.Filter.Eq("Consolidated", false);
            var result = await _blocks.FindAsync(filter, options);
            return await result.ToListAsync();
        }

        //public async Task<IEnumerable<string>> GetAllUnConsolidatedBlockHashesAsync()
        //{
        //    var options = new FindOptions<Block, BsonDocument>
        //    {
        //        Limit = 1000,
        //        Sort = Builders<Block>.Sort.Ascending(o => o.TimeStamp),
        //        Projection = Builders<Block>.Projection.Include(a => a.Hash)
        //    };
        //    var filter = Builders<Block>.Filter.Eq("Consolidated", false);
        //    var result = await _blocks.FindAsync(filter, options);
        //    return (await result.ToListAsync()).Select(a => a["Hash"].AsString);
        //}

        async Task<ServiceBlock> IAccountCollectionAsync.GetServiceGenesisBlockAsync()
        {
            var options = new FindOptions<Block, Block>
            {
                Limit = 1,
                Sort = Builders<Block>.Sort.Ascending(o => o.Height)
            };
            var filter = Builders<Block>.Filter;
            var filterDefination = filter.Eq("BlockType", BlockTypes.Service);

            var finds = await _blocks.FindAsync(filterDefination, options);
            return await finds.FirstOrDefaultAsync() as ServiceBlock;
        }

        async Task<LyraTokenGenesisBlock> IAccountCollectionAsync.GetLyraTokenGenesisBlockAsync()
        {
            var options = new FindOptions<Block, Block>
            {
                Limit = 1
                //Sort = Builders<Block>.Sort.Ascending(o => o.Height)
            };
            var filter = Builders<Block>.Filter;
            var filterDefination = filter.Eq("BlockType", BlockTypes.LyraTokenGenesis);

            var finds = await _blocks.FindAsync(filterDefination, options);
            return await finds.FirstOrDefaultAsync() as LyraTokenGenesisBlock;
        }

        // >= startTime <= endTime, count max 1000
        public async Task<List<TransactionDescription>> SearchTransactionsAsync(string accountId, DateTime startTime, DateTime endTime, int count)
        {
            var options = new FindOptions<TransactionBlock, TransactionBlock>
            {
                Sort = Builders<TransactionBlock>.Sort.Ascending(o => o.TimeStamp),
                Limit = count > 1000 ? 1000 : count
            };
            var builder = Builders<TransactionBlock>.Filter;
            var filter = builder.And(builder.Eq("AccountID", accountId),
                builder.Gte("TimeStamp.Ticks", startTime.Ticks), builder.Lte("TimeStamp.Ticks", endTime.Ticks));
            var result = await _blocks
                .OfType<TransactionBlock>()
                .FindAsync(filter, options);
            var txes = await result.ToListAsync();

            // convert it into tx desc
            List<TransactionDescription> transactions = new List<TransactionDescription>();
            Dictionary<string, long> oldBalance = null;

            // fill oldBalance if there is previous block
            if(txes.Count > 0 && txes.First().Height > 1)
            {
                var prevTx = await FindBlockByHashAsync(txes.First().PreviousHash) as TransactionBlock;
                oldBalance = prevTx.Balances;
            }

            for (int i = 0; i < txes.Count; i++)
            {
                var block = txes[i];
                var tx = new TransactionDescription
                {
                    Height = block.Height,
                    TimeStamp = block.TimeStamp,
                    Balances = block.Balances
                };
                if (block is SendTransferBlock sb)
                {
                    tx.IsReceive = false;

                    tx.SendAccountId = block.AccountID;
                    tx.SendHash = block.Hash;

                    tx.RecvAccountId = sb.DestinationAccountId;
                    var recvBlock = await FindReceiveBlockAsync(sb.DestinationAccountId, block.Hash);
                    if (recvBlock != null)
                        tx.RecvHash = recvBlock.Hash;
                }                    
                else if (block is ReceiveTransferBlock rb)
                {
                    tx.IsReceive = true;

                    tx.RecvAccountId = block.AccountID;
                    tx.RecvHash = block.Hash;

                    if (rb.SourceHash == null)
                    {
                        tx.SendAccountId = null;    // Genesis
                        tx.SendHash = null;
                    }
                    else
                    {
                        var from = await FindBlockByHashAsync(rb.SourceHash);
                        if(from is TransactionBlock txs)
                        {
                            tx.SendAccountId = txs.AccountID;
                        }
                        else
                        {
                            tx.SendAccountId = from.BlockType.ToString();
                        }
                        tx.SendHash = from.Hash;
                    }
                }

                if (oldBalance == null)
                {
                    if (block.Height == 1)
                        tx.Changes = block.Balances;
                    else
                    {
                        _log.LogError("SearchTransactionsAsync: oldBalance missing. Should not happens.");
                    }                        
                }
                else
                {
                    tx.Changes = new Dictionary<string, long>();
                    foreach (var kvp in block.Balances)
                    {
                        long oldValue = 0;
                        if (oldBalance.ContainsKey(kvp.Key))
                        {
                            oldValue = oldBalance[kvp.Key];
                        }

                        var delta = kvp.Value - oldValue;
                        if (delta != 0)
                        {
                            tx.Changes.Add(kvp.Key, delta);
                        }
                    }

                    foreach(var kvp in oldBalance)
                    {
                        if(!block.Balances.ContainsKey(kvp.Key))        // the balance dispeared!
                        {
                            tx.Changes.Add(kvp.Key, 0 - kvp.Value);
                        }
                    }
                }

                oldBalance = block.Balances;

                transactions.Add(tx);
            }

            return transactions;
        }

        // >= startTime < endTime
        public async Task<List<Block>> GetBlocksByTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            var options = new FindOptions<Block, Block>
            {
                Sort = Builders<Block>.Sort.Ascending(o => o.TimeStamp)
            };
            var builder = Builders<Block>.Filter;
            var filter = builder.And(builder.Gte("TimeStamp", startTime), builder.Lt("TimeStamp", endTime));
            var result = await _blocks.FindAsync(filter, options);
            return await result.ToListAsync();
        }

        public async Task<List<string>> GetBlockHashesByTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            var builder = Builders<Block>.Filter;
            var filter = builder.And(builder.Gte("TimeStamp.Ticks", startTime.Ticks), builder.Lt("TimeStamp.Ticks", endTime.Ticks));
            var q = await _blocks.Find(filter)
                .SortBy(o => o.TimeStamp)
                //.Project(a => a.Hash)
                .ToListAsync();

            return q.Select(a => a.Hash).ToList();

            //var options = new FindOptions<Block, BsonDocument>
            //{
            //    Sort = Builders<Block>.Sort.Ascending(o => o.TimeStamp),
            //    Projection = Builders<Block>.Projection.Include(a => a.Hash)
            //};
            //var builder = Builders<Block>.Filter;
            //var filter = builder.And(builder.Gte("TimeStamp.Ticks", startTime.Ticks), builder.Lt("TimeStamp.Ticks", endTime.Ticks));
            //var result = await _blocks.FindAsync(filter, options);
            //return (await result.ToListAsync()).Select(a => a["Hash"].AsString).ToList();
        }

        private class VoteInfo
        {
            public string AccountID { get; set; }
            public Dictionary<string, long> Balances { get; set; }
            public long Height { get; set; }
            public string VoteFor { get; set; }
        }

        public List<Voter> GetVoters(List<string> posAccountIds, DateTime endTime)
        {
            var importedAccounts = FindAllImportedAccountID();

            // find last one tx block
            var perAtrVotes = _blocks.OfType<TransactionBlock>()//atrVotes
                .AsQueryable()

                //.Select(a => BsonSerializer.Deserialize<VoteInfo>(a))
                .OrderByDescending(a => a.Height)
                .GroupBy(a => a.AccountID)      // this time select the latest block of account
                .Select(g => new Voter
                {
                    AccountId = g.Key,
                    //Balance = g.First().Balances[LyraGlobal.OFFICIALTICKERCODE],
                    Balance2 = g.First().Balances,//.ContainsKey(LyraGlobal.OFFICIALTICKERCODE) ? g.First().Balances[LyraGlobal.OFFICIALTICKERCODE] : 0,
                    VoteFor = g.First().VoteFor
                })
                .Where(x => posAccountIds.Contains(x.VoteFor))
                .Where(x => !importedAccounts.Contains(x.AccountId))

                .ToList();

            return perAtrVotes;
        }

        public List<Vote> FindVotes(List<string> posAccountIds, DateTime endTime)
        {
            var importedAccounts = FindAllImportedAccountID();

            // find last one tx block
            var perAtrVotes = _blocks.OfType<TransactionBlock>()//atrVotes
                .AsQueryable()

                //.Select(a => BsonSerializer.Deserialize<VoteInfo>(a))
                .OrderByDescending(a => a.Height)
                .GroupBy(a => a.AccountID)      // this time select the latest block of account
                .Select(g => new Voter
                {
                    AccountId = g.Key,
                    //Balance = g.First().Balances[LyraGlobal.OFFICIALTICKERCODE],
                    Balance2 = g.First().Balances,//.ContainsKey(LyraGlobal.OFFICIALTICKERCODE) ? g.First().Balances[LyraGlobal.OFFICIALTICKERCODE] : 0,
                    VoteFor = g.First().VoteFor
                })
                .Where(x => posAccountIds.Contains(x.VoteFor))
                .Where(x => !importedAccounts.Contains(x.AccountId))
                .AsEnumerable()
                .GroupBy(a => a.VoteFor)        // this time aggregate the total votes
                .Select(g => new Vote { AccountId = g.Key, Amount = g.Sum(a => a.LYR) / LyraGlobal.TOKENSTORAGERITO })
                //.Select(g => new Vote { AccountId = g.Key, Amount = g.Sum(a => a.Balance) / LyraGlobal.TOKENSTORAGERITO })
                .OrderByDescending(a => a.Amount)
                .ToList();


            return perAtrVotes;
        }

        public Task<FeeStats> GetFeeStatsAsync()
        {
            var sbs = _blocks.OfType<ServiceBlock>()
                    .Aggregate()
                    .SortBy(x => x.Height)
                    .ToList();

            decimal totalFeeConfirmed = sbs.Sum(a => a.FeesGenerated.ToBalanceDecimal());

            var builder = Builders<TransactionBlock>.Filter;
            var projection = Builders<TransactionBlock>.Projection;

            var txFilter = builder.And(builder.Gt("TimeStamp", sbs.Last().TimeStamp));

            var unTxs = _blocks.OfType<TransactionBlock>()
                .Aggregate()
                .Match(txFilter)
                .ToList();

            decimal totalFeeUnConfirmed = unTxs.Sum(a => a.Fee);

            // confirmed earns
            IEnumerable<RevnuItem> GetRevnuFromSb(decimal fees, ServiceBlock sb)
            {
                return sb.Authorizers.Keys.Select(a => new RevnuItem { AccId = a, Revenue = Math.Round(fees / sb.Authorizers.Count, 8) });
            };

            static IEnumerable<RevnuItem> Merge(IEnumerable<RevnuItem> List1, IEnumerable<RevnuItem> List2)
            {
                var list3 = List1.Concat(List2)
                             .GroupBy(x => x.AccId)
                             .Select( g =>
                                 new RevnuItem
                                 {
                                     AccId = g.Key,
                                     Revenue = Math.Round(g.Sum(x => x.Revenue), 8)
                                 });
                return list3;
            };
            var confimed = Enumerable.Empty<RevnuItem>();
            for(int i = sbs.Count - 1; i > 0; i--)
            {
                confimed = Merge(confimed, GetRevnuFromSb(sbs[i].FeesGenerated.ToBalanceDecimal(), sbs[i - 1]));
            }

            // unconfirmed
            var unconfirm = sbs.Last().Authorizers.Keys.Select(a => new RevnuItem { AccId = a, Revenue = Math.Round(totalFeeUnConfirmed / sbs.Last().Authorizers.Count, 8) });

            return Task.FromResult(new FeeStats { TotalFeeConfirmed = totalFeeConfirmed,
                TotalFeeUnConfirmed = totalFeeUnConfirmed,
                ConfirmedEarns = confimed.OrderByDescending(a => a.Revenue).ToList(),
                UnConfirmedEarns = unconfirm.OrderByDescending(a => a.Revenue).ToList()
            });
        }

        public async Task<PoolFactoryBlock> GetPoolFactoryAsync()
        {
            var options = new FindOptions<Block, Block>
            {
                Limit = 1,
                Sort = Builders<Block>.Sort.Descending(o => o.Height)
            };
            var filter = Builders<Block>.Filter;
            var filterDefination = filter.Eq("BlockType", BlockTypes.PoolFactory);

            var finds = await _blocks.FindAsync(filterDefination, options);
            return await finds.FirstOrDefaultAsync() as PoolFactoryBlock;
        }

        public PoolGenesisBlock GetPoolByID(string poolid)
        {
            var pool = _blocks.OfType<PoolGenesisBlock>()
                .AsQueryable()
                .Where(a => a.AccountID == poolid)
                .FirstOrDefault();

            return pool;
        }

        public async Task<PoolGenesisBlock> GetPoolAsync(string token0, string token1)
        {
            // get token gensis to make the token name proper
            var token0Gen = await FindTokenGenesisBlockAsync(null, token0);
            var token1Gen = await FindTokenGenesisBlockAsync(null, token1);

            if (token0Gen == null || token1Gen == null)
            {
                return null;
            }

            var arrStr = new[] { token0Gen.Ticker, token1Gen.Ticker };
            Array.Sort(arrStr);

            var builder = Builders<Block>.Filter;
            var poolFilter = builder.And(builder.Eq("Token0", arrStr[0]), builder.Eq("Token1", arrStr[1]));
            var pool = _blocks.Find(poolFilter)
                .Limit(1)
                .FirstOrDefault();

            return pool as PoolGenesisBlock;
        }

        public async Task<List<Block>> GetAllBrokerAccountsForOwnerAsync(string ownerAccount)
        {
            var filter = Builders<Block>.Filter;
            var filterDefination = filter.And(filter.Or(
                filter.Eq("BlockType", BlockTypes.ProfitingGenesis),
                filter.Eq("BlockType", BlockTypes.StakingGenesis),
                filter.Eq("BlockType", BlockTypes.OrgnizationGenesis)
                ), filter.Eq("OwnerAccountId", ownerAccount));

            var finds = await _blocks.FindAsync(filterDefination);
            var gens = finds.ToList();
            return gens;
        }

        public long GetCurrentView()
        {
            var filter = Builders<Block>.Filter;
            var filterDefination = filter.Eq("BlockType", BlockTypes.Service);

            var finds = _blocks.Find(filterDefination).SortByDescending(a => a.Height).FirstOrDefault();

            return finds == null ? 0 : finds.Height;
        }

        public async Task<ProfitingStats> GetAccountStatsAsync(string accountId, DateTime begin, DateTime end)
        {
            var stk = await FindFirstBlockAsync(accountId);
            if (stk == null)
                return null;

            var filter = Builders<AccountChange>.Filter;
            var filterDefination = filter.And(
                filter.Eq("AccountID", accountId),
                filter.Gte("Time", begin),
                filter.Lte("Time", end),
                filter.Gt("LyrChg", 0)
                );

            var finds = await _accountChanges
                .Find(filterDefination)
                .ToListAsync();

            var total = finds.Sum(a => a.LyrChg);
            var stats = new ProfitingStats
            {
                ProfitingID = accountId,
                Begin = begin,
                End = end,
                Total = total
            };
            return stats;
        }

        public async Task<ProfitingStats> GetBenefitStatsAsync(string pftid, string stkid, DateTime begin, DateTime end)
        {
            var pft = await FindFirstBlockAsync(pftid);
            if (pft == null)
                return null;

            var stk = await FindFirstBlockAsync(stkid);
            if (stk == null)
                return null;

            var bnfts = _blocks.AsQueryable()
                .Where(a => a is BenefitingBlock)
                //.Cast<BenefitingBlock>()
                .Where(a => (a as BenefitingBlock).AccountID == pftid && (a as BenefitingBlock).StakingAccountId == stkid
                        && a.TimeStamp > begin && a.TimeStamp < end);

            var query = from b in bnfts
                        join c in _accountChanges.AsQueryable()
                            on b.Hash equals c.TxHash
                        select c.LyrChg;

            var queryy = query.ToList();

            return new ProfitingStats
            {
                ProfitingID = stkid,
                Begin = begin,
                End = end,
                Total = -1 * queryy.Sum()
            };
        }

        // StakingAccountId -> UserAccountId
        public List<Staker> FindAllStakings(string pftid, DateTime timeBefore)
        {
            var filter = Builders<Block>.Filter;
            var filterDefination = filter.Eq("Voting", pftid);
            var finds = _blocks.Find(filterDefination);

            var stakings = finds.ToList()
                .Cast<TransactionBlock>()
                .OrderByDescending(a => a.Height)
                .GroupBy(a => a.AccountID)      // this time select the latest block of account
                .Select(g => new
                {
                    AccountId = g.Key,
                    //Balance = g.First().Balances[LyraGlobal.OFFICIALTICKERCODE],
                    Balance2 = g.First().Balances,//.ContainsKey(LyraGlobal.OFFICIALTICKERCODE) ? g.First().Balances[LyraGlobal.OFFICIALTICKERCODE] : 0,
                    Owner = ((IBrokerAccount)g.First()).OwnerAccountId,
                    Start = ((IStaking)g.First()).Start,
                    Days = ((IStaking)g.First()).Days,
                    CompoundMode = ((IStaking)g.First()).CompoundMode
                });

            return stakings
                .Where(a => a.Start < timeBefore && a.Start.AddDays(a.Days) > timeBefore)
                .OrderByDescending(x => x.Balance2[LyraGlobal.OFFICIALTICKERCODE])
                .ThenBy(x => x.AccountId)
                .Select(a => new Staker
                {
                    StkAccount = a.AccountId,
                    OwnerAccount = a.Owner,
                    Amount = a.Balance2[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal(),
                    Time = a.Start,
                    Days = a.Days,
                    CompoundMode = a.CompoundMode
                })
                .ToList();
        }

        public async Task<List<Profiting>> FindAllProfitingAccountsAsync(DateTime begin, DateTime end)
        {
            var q = _blocks.OfType<ProfitingGenesis>()
                .AsQueryable()
                .Where(a => a.TimeStamp > begin && a.TimeStamp < end)
                .ToList();

            var rets = new List<Profiting>();
            foreach(var gen in q)
            {
                var stats = await GetAccountStatsAsync(gen.AccountID, begin, end);
                rets.Add(new Profiting
                {
                    gens = gen,
                    totalprofit = stats.Total
                });
            }
            return rets;
        }

        public ProfitingGenesis FindProfitingAccountsByName(string Name)
        {
            var q = _blocks.OfType<ProfitingGenesis>()
                .AsQueryable()
                .Where(a => a.Name == Name)
                .ToList();

            var rets = q.FirstOrDefault();
            return rets;
        }

        public async Task<List<ProfitingGenesis>> FindAllProfitingAccountForOwnerAsync(string ownerAccountId)
        {
            //var filter = Builders<Block>.Filter;
            //var filterDefination = filter.Eq("OwnerAccountId", ownerAccountId);
            //var finds = await _blocks.FindAsync(filterDefination);

            var q = await _blocks.OfType<ProfitingGenesis>()
                .FindAsync(a => a.OwnerAccountId == ownerAccountId);

            return q.ToList();
        }

        public async Task<List<StakingGenesis>> FindAllStakingAccountForOwnerAsync(string ownerAccountId)
        {
            //var filter = Builders<Block>.Filter;
            //var filterDefination = filter.Eq("OwnerAccountId", ownerAccountId);
            //var finds = await _blocks.FindAsync(filterDefination);

            var q = await _blocks.OfType<StakingGenesis>()
                .FindAsync(a => a.OwnerAccountId == ownerAccountId);

            return await q.ToListAsync();
        }

        public async Task<List<TransactionBlock>> GetAllDexWalletsAsync(string owner)
        {
            var wgens = await _blocks.OfType<DexWalletGenesis>()
                .Find(a => a.OwnerAccountId == owner)
                .ToListAsync();

            var all = new List<TransactionBlock>();
            foreach(var w in wgens)
            {
                var dw = await FindLatestBlockAsync(w.AccountID) as TransactionBlock;
                if (dw != null)
                    all.Add(dw);
                else
                    _log.LogCritical("Not IDexWallet!!!");
            }
            return all;
        }

        public async Task<TransactionBlock?> FindDexWalletAsync(string owner, string symbol, string provider)
        {
            var all = await GetAllDexWalletsAsync(owner);
            return all.Cast<IDexWallet>()
                .FirstOrDefault(a => a.ExtSymbol == symbol && a.ExtProvider == provider)
                as TransactionBlock;
        }

        // Fiat wallet
        public async Task<TransactionBlock?> FindFiatWalletAsync(string owner, string symbol)
        {
            var all = await GetAllFiatWalletsAsync(owner);
            return all.Cast<IFiatWallet>()
                .FirstOrDefault(a => a.ExtSymbol == symbol)
                as TransactionBlock;
        }

        public async Task<List<TransactionBlock>> GetAllFiatWalletsAsync(string owner)
        {
            var wgens = await _blocks.OfType<FiatWalletGenesis>()
                .Find(a => a.OwnerAccountId == owner)
                .ToListAsync();

            var all = new List<TransactionBlock>();
            foreach (var w in wgens)
            {
                var dw = await FindLatestBlockAsync(w.AccountID) as TransactionBlock;
                if (dw != null)
                    all.Add(dw);
                else
                    _log.LogCritical("Not IFiatWallet!!!");
            }
            return all;
        }

        public async Task<List<TransactionBlock>> GetAllDaosAsync(int page, int pageSize)
        {
            var filter = Builders<TransactionBlock>.Filter;
            var filterDefination = filter.Exists("Treasure", true);

            var result = await _snapshots
                .Find(filterDefination)
                .ToListAsync();

            return result;
        }

        public Block GetDaoByName(string name)
        {
            var regexFilter = Regex.Escape(name);
            var filter = Builders<DaoGenesisBlock>.Filter.Regex(u => u.Name, new BsonRegularExpression("/^" + regexFilter + "$/i"));
            var genResult = _blocks.OfType<DaoGenesisBlock>()
                .Find(filter)
                .FirstOrDefault();

            if (genResult != null)
                return FindLatestBlock(genResult.AccountID);

            return null;
        }

        public Block GetDealerByName(string name)
        {
            var regexFilter = Regex.Escape(name);
            var filter = Builders<DealerGenesisBlock>.Filter.Regex(u => u.Name, new BsonRegularExpression("/^" + regexFilter + "$/i"));
            var genResult = _blocks.OfType<DealerGenesisBlock>()
                .Find(filter)
                .FirstOrDefault();

            if (genResult != null)
                return FindLatestBlock(genResult.AccountID);

            return null;
        }

        public Block GetDealerByAccountId(string accountId)
        {
            var q = _blocks.OfType<DealerGenesisBlock>()
                .Find(a => a.OwnerAccountId == accountId)                
                .FirstOrDefault();

            return q;
        }

        public async Task<List<Block>> GetOtcOrdersByOwnerAsync(string accountId)
        {
            var q = _blocks.OfType<OTCOrderGenesisBlock>()
                .Find(a => a.OwnerAccountId == accountId)
                .ToList();

            var blks = new List<Block>();
            foreach(var x in q)
            {
                var b = await FindLatestBlockAsync(x.AccountID);
                blks.Add(b);
            }
            return blks;
        }

        public async Task<List<TransactionBlock>> FindOtcTradeAsync(string accountId, bool onlyOpenTrade, int page, int pageSize)
        {
            var filter = Builders<TransactionBlock>.Filter;
            var filterDefination = filter.And(
                filter.Exists("OTStatus"),
                filter.Or(
                    filter.Eq("OwnerAccountId", accountId),
                    filter.Eq("Trade.orderOwnerId", accountId)
                    )                
                );

            var q = await _snapshots
                .FindAsync(filterDefination);

            return q.ToList();
        }

        public async Task<List<TransactionBlock>> FindOtcTradeByStatusAsync(string daoid, OTCTradeStatus status, int page, int pageSize)
        {
            var filter = Builders<TransactionBlock>.Filter;
            var filterDefination = filter.And(
                    filter.Eq("Trade.daoId", daoid),
                    filter.Eq("OTStatus", status)
                );

            var q = await _snapshots
                .FindAsync(filterDefination);

            return q.ToList();
        }

        public async Task<List<TransactionBlock>> FindOtcTradeForOrderAsync(string orderid)
        {
            var filter = Builders<TransactionBlock>.Filter;
            var filterDefination = filter.Eq("Trade.orderId", orderid);

            var q = await _snapshots
                .FindAsync(filterDefination);

            return q.ToList();
        }

        public async Task<List<TradeStats>> GetOtcTradeStatsForUsersAsync(List<string> accountIds)
        {
            var stats = new List<TradeStats>();
            // 1, as trade owner; 2, as order owner; 
            foreach(var accountId in accountIds)
            {
                var trades = await FindOtcTradeAsync(accountId, false, -1, -1); // so if the api changes, modify here
                stats.Add(new TradeStats
                {
                    AccountId = accountId,
                    TotalTrades = trades.Count,
                    FinishedCount = trades.Where(a => (a as IOtcTrade).OTStatus == OTCTradeStatus.CryptoReleased).Count(),
                });
            }
            return stats;
        }

        #region Universal order/trade
        public async Task<List<TransactionBlock>> GetUniOrdersByOwnerAsync(string accountId)
        {
            var q = _blocks.OfType<UniOrderGenesisBlock>()
                .Find(a => a.OwnerAccountId == accountId)
                //.SortByDescending(a => a.TimeStamp)
                .ToList();

            var blks = new List<TransactionBlock>();
            foreach (var x in q)
            {
                blks.Add(x);
                var b = await FindLatestBlockAsync(x.AccountID) as TransactionBlock;
                if(b.Hash != x.Hash)
                    blks.Add(b);
            }
            return blks.OrderByDescending(a => a.TimeStamp).ToList();
        }

        /// <summary>
        /// get order details
        /// </summary>
        /// <param name="orderId">the AccountID of order</param>
        /// <returns>3 blocks in list: order's latest block, offering token genesis, biding token genesis</returns>
        public async Task<List<TransactionBlock>?> GetUniOrderByIdAsync(string orderId)
        {
            var latest = await FindLatestBlockAsync(orderId) as TransactionBlock;
            if(latest != null && latest is IUniOrder orderBlock)
            {
                var offeringGens = await FindTokenGenesisBlockAsync(null, orderBlock.Order.offering);
                var bidingGens = await FindTokenGenesisBlockAsync(null, orderBlock.Order.biding);
                var daoOnTheTime = await FindLatestBlockByTimeAsync(orderBlock.Order.daoId, orderBlock.TimeStamp);

                var blks = new List<TransactionBlock>()
                {
                    latest as TransactionBlock, offeringGens, bidingGens, daoOnTheTime as TransactionBlock
                };
                return blks;
            }

            return null;
        }

        public async Task<BsonDocument> FindTradableUniOrders2Async(string ? catalog)
        {
            var arr = new BsonDocument[]
{
    new BsonDocument("$facet",
    new BsonDocument
        {
            { "OverStats",
    new BsonArray
            {
                new BsonDocument("$match",
                new BsonDocument("UOStatus",
                new BsonDocument("$exists", true))),
                new BsonDocument("$bucket",
                new BsonDocument
                    {
                        { "groupBy", "$UOStatus" },
                        { "boundaries",
                new BsonArray
                        {
                            0,
                            10,
                            30,
                            50
                        } },
                        { "default", new BsonDocument("Count", new BsonDocument("$sum", 0)) },
                        { "output",
                new BsonDocument("Count",
                new BsonDocument("$sum", 1)) }
                    })
            } },
            { "OwnerStats",
    new BsonArray
            {
                new BsonDocument("$match",
                new BsonDocument("UOStatus",
                new BsonDocument("$exists", true))),
                new BsonDocument("$group",
                new BsonDocument
                    {
                        { "_id",
                new BsonDocument
                        {
                            { "Owner", "$OwnerAccountId" },
                            { "State", "$UOStatus" }
                        } },
                        { "Count",
                new BsonDocument("$sum", 1) }
                    })
            } },
            { "Daos",
    new BsonArray
            {
                new BsonDocument("$match",
                new BsonDocument("$or",
                new BsonArray
                        {
                            new BsonDocument("UOStatus", 0),
                            new BsonDocument("UOStatus", 10)
                        })),
                new BsonDocument("$lookup",
                new BsonDocument
                    {
                        { "from", _networkId + "_snapshots" },
                        { "localField", "Order.daoId" },
                        { "foreignField", "AccountID" },
                        { "as", "DaoInfo" }
                    }),
                new BsonDocument("$group",
                new BsonDocument
                    {
                        { "_id", "$DaoInfo.AccountID" },
                        { "Info",
                new BsonDocument("$first", "$DaoInfo") }
                    })
            } },
            { "Orders",
    new BsonArray
            {
                new BsonDocument("$match",
                new BsonDocument("$or",
                new BsonArray
                        {
                            new BsonDocument("UOStatus", 0),
                            new BsonDocument("UOStatus", 10)
                        })),
                new BsonDocument("$lookup",
                new BsonDocument
                    {
                        { "from", _networkId + "_blocks" },
                        { "localField", "Order.offering" },
                        { "foreignField", "Ticker" },
                        { "as", "OfferingGens" }
                    }),
                new BsonDocument("$lookup",
                new BsonDocument
                    {
                        { "from", _networkId + "_blocks" },
                        { "localField", "Order.biding" },
                        { "foreignField", "Ticker" },
                        { "as", "BidingGens" }
                    }),
                new BsonDocument("$project",
                new BsonDocument
                    {
                        { "AccountID", 1 },
                        { "OwnerAccountId", 1 },
                        { "Order", 1 },
                        { "UOStatus", 1 },
                         { "TimeStamp", 1 },
                        { "OfferingCat",
                new BsonDocument("$first", "$OfferingGens.DomainName") },
                        { "OfferingName",
                new BsonDocument("$first", "$OfferingGens.Custom1") },
                        { "OfferingDesc",
                new BsonDocument("$first", "$OfferingGens.Description") },
                        { "OfferingUrl",
                new BsonDocument("$first", "$OfferingGens.Custom2") },
                        { "BidingCat",
                new BsonDocument("$first", "$BidingGens.DomainName") },
                        { "BidingName",
                new BsonDocument("$first", "$BidingGens.Custom1") },
                        { "BidingDesc",
                new BsonDocument("$first", "$BidingGens.Description") },
                        { "BidingUrl",
                new BsonDocument("$first", "$BidingGens.Custom2") }
                    })
            } }
        })
};

            if (catalog != null && catalog != "All")
            {
                var type = catalog switch
                {
                    "Token" => HoldTypes.Token,
                    "NFT" => HoldTypes.NFT,
                    "Fiat" => HoldTypes.Fiat,
                    "Service" => HoldTypes.SVC,
                    _ => HoldTypes.TOT,
                };

                arr = arr.Prepend(new BsonDocument("$match", new BsonDocument("Order.offerby", type))).ToArray();
            }

            PipelineDefinition<TransactionBlock, BsonDocument> pipeline = arr;

            try
            {
                var q1 = _snapshots.Aggregate(pipeline);

                var x = await q1.FirstOrDefaultAsync();

                return x;
            }
            catch(Exception ex)
            {
                // when no tradable error, there is a exception
                _log.LogInformation($"Mongodb in find orders: {ex}");
                return BsonDocument.Parse("{}");
            }
        }

        /// <summary>
        /// find current tradable orders. 
        /// </summary>
        /// <param name="catalog">null or 'All' for all catalog, 'Token', 'Fiat' or so for other catalogs.</param>
        /// <returns>a mix of order and dao. user should treat them separately.</returns>
        public async Task<List<Dictionary<string, object>>> FindTradableUniOrdersAsync(string? catalog)
        {
            var filter = Builders<TransactionBlock>.Filter;
            var filterDefination = filter.Or(
                filter.Eq("UOStatus", UniOrderStatus.Open),
                filter.Eq("UOStatus", UniOrderStatus.Partial)
                );

            if (catalog != null && catalog != "All")
            {
                var type = catalog switch
                {
                    "Token" => HoldTypes.Token,
                    "NFT" => HoldTypes.NFT,
                    "Fiat" => HoldTypes.Fiat,
                    "Service" => HoldTypes.SVC,
                    _ => HoldTypes.TOT,
                };

                filterDefination = filter.And(filterDefination, filter.Eq("Order.offerby", type));
            }

            // use mongodb Lookup to query _snapshots on (TransactionBlock as IUniOrder).Order.daoId == TransactionBlock.AccountID
            var q2 = await _snapshots
                .Aggregate()
                .Match(filterDefination)
                .Lookup(_snapshotsCollectionName, "Order.daoId", "AccountID", "DaoInfo")
                .Unwind("DaoInfo")
                .Project("{AccountID: 1, OwnerAccountId: 1, Order: 1, UOStatus: 1, DaoName: '$DaoInfo.Name'}")

                .ToListAsync();

            var arrDict = q2.ConvertAll(BsonTypeMapper.MapToDotNetValue);
            var arrDict2 = arrDict.Select(a => (a as Dictionary<string, object>)).ToList();

            var dictuids = new Dictionary<string, (long total, long finished)>();
            foreach(var dict in arrDict2)
            {
                var userid = dict["OwnerAccountId"].ToString();

                if (dictuids.ContainsKey(userid))
                {
                    dict.Add("Total", dictuids[userid].total);
                    dict.Add("Finished", dictuids[userid].finished);

                    continue;
                }
                
                var filterDefinationTotal = filter.And(
                    filter.Exists("UOStatus"),
                    filter.Or(
                        filter.Eq("OwnerAccountId", userid),
                        filter.Eq("Trade.orderOwnerId", userid)
                        )
                    );

                var filterDefinationFinished = filter.And(
                    filter.Eq("UOStatus", UniTradeStatus.Closed),
                    filter.Or(
                        filter.Eq("OwnerAccountId", userid),
                        filter.Eq("Trade.orderOwnerId", userid)
                        )
                    );

                var total = _snapshots
                    .Aggregate()
                    .Match(filterDefinationTotal)
                    .Count()
                    .Single()
                    .Count;

                // finished may be empty
                var finishedx = _snapshots
                    .Aggregate()
                    .Match(filterDefinationFinished)
                    .Count();

                var finished = 0L;
                if (finishedx.Any())
                {
                    finished = finishedx.Single().Count;
                }

                dict.Add("Total", total);
                dict.Add("Finished", finished);

                dictuids.Add(userid, (total, finished));
            }

            return arrDict2;
        }

        public class OrderDaoCombo
        {
            public string offering { get; set; }
            public string daoName { get; set; }
    }

        public async Task<Dictionary<string, List<TransactionBlock>>> FindTradableOrdersAsync()
        {
            var ords = await FindTradableUniOrdersAsync("ALL");
            var daoIds = ords.Cast<IUniOrder>()
                .Select(a => a.Order.daoId)
                .Distinct()
                .ToList();

            var daos = _snapshots
                .AsQueryable()
                .Where(a => daoIds.Contains(a.AccountID))
                .ToList();

            return new Dictionary<string, List<TransactionBlock>>
            {
                { "orders", ords.Cast<TransactionBlock>().ToList() },
                { "daos", daos },
            };
        }

        private async Task<List<TransactionBlock>> FindTradableUniOrdersAsync()
        {
            var filter = Builders<TransactionBlock>.Filter;
            var filterDefination = filter.Or(
                filter.Eq("UOStatus", UniOrderStatus.Open),
                filter.Eq("UOStatus", UniOrderStatus.Partial)
                );

            var q = await _snapshots
                .FindAsync(filterDefination);

            return q.ToList().OrderByDescending(a => a.TimeStamp).ToList();
        }

        public async Task<Dictionary<string, List<TransactionBlock>>> FindTradableUniAsync()
        {
            var ords = await FindTradableUniOrdersAsync();
            var daoIds = ords.Cast<IUniOrder>()
                .Select(a => a.Order.daoId)
                .Distinct()
                .ToList();

            var daos = _snapshots
                .AsQueryable()
                .Where(a => daoIds.Contains(a.AccountID))
                .ToList();

            return new Dictionary<string, List<TransactionBlock>>
            {
                { "orders", ords },
                { "daos", daos },
            };
        }

        public async Task<List<TransactionBlock>> FindUniTradeAsync(string accountId, bool onlyOpenTrade, int page, int pageSize)
        {
            var filter = Builders<TransactionBlock>.Filter;
            var filterDefination = filter.And(
                filter.Exists("UTStatus"),
                filter.Or(
                    filter.Eq("OwnerAccountId", accountId),
                    filter.Eq("Trade.orderOwnerId", accountId)
                    )
                );
            var sort = Builders<TransactionBlock>.Sort.Descending("TimeStamp");
            var options = new FindOptions<TransactionBlock>
            {
                Sort = sort
            };

            var q = await _snapshots
                .FindAsync(filterDefination, options)                
                ;

            return q.ToList();
        }

        public async Task<List<TransactionBlock>> FindUniTradeByStatusAsync(string daoid, UniTradeStatus status, int page, int pageSize)
        {
            var filter = Builders<TransactionBlock>.Filter;
            var filterDefination = filter.And(
                    filter.Eq("Trade.daoId", daoid),
                    filter.Eq("UTStatus", status)
                );

            var q = await _snapshots
                .FindAsync(filterDefination);

            return q.ToList();
        }

        public async Task<List<TransactionBlock>> FindUniTradeForOrderAsync(string orderid)
        {
            var filter = Builders<Block>.Filter;
            var filterDefination = filter.And(
                filter.Eq("Trade.orderId", orderid),
                filter.Eq("BlockType", BlockTypes.UniTradeGenesis));

            var q = _blocks
                .Find(filterDefination)
                .SortByDescending(a => a.TimeStamp)
                .ToList();

            var blks = new List<TransactionBlock>();
            foreach (var x in q)
            {
                blks.Add(x as TransactionBlock);
                var b = await FindLatestBlockAsync((x as TransactionBlock).AccountID) as TransactionBlock;
                if(b.Hash != x.Hash)
                    blks.Add(b);
            }
            return blks;
        }

        public async Task<List<TradeStats>> GetUniTradeStatsForUsersAsync(List<string> accountIds)
        {
            var stats = new List<TradeStats>();
            // 1, as trade owner; 2, as order owner; 
            foreach (var accountId in accountIds)
            {
                var trades = await FindUniTradeAsync(accountId, false, -1, -1); // so if the api changes, modify here
                stats.Add(new TradeStats
                {
                    AccountId = accountId,
                    TotalTrades = trades.Count,
                    FinishedCount = trades.Where(a => (a as IUniTrade).UTStatus == UniTradeStatus.Closed).Count(),
                });
            }
            return stats;
        }
        #endregion

        public async Task<List<TransactionBlock>> FindAllVotesByDaoAsync(string daoid, bool openOnly)
        {
            var filter = Builders<TransactionBlock>.Filter;
            FilterDefinition<TransactionBlock> filterDefination;

            if(openOnly)
                filterDefination = filter.And(
                    filter.Eq("BlockType", BlockTypes.VoteGenesis),
                    filter.Eq("Subject.DaoId", daoid)
                );
            else
                filterDefination = filter.And(
                    filter.Eq("BlockType", BlockTypes.VoteGenesis),
                    filter.Eq("Subject.DaoId", daoid),
                    filter.Eq("VoteState", VoteStatus.InProgress)
                );

            var q = await _blocks.OfType<TransactionBlock>()
                .FindAsync(filterDefination);

            return q.ToList();
        }

        public async Task<List<TransactionBlock>> FindAllVoteForTradeAsync(string tradeid)
        {
            var myvotes = new List<TransactionBlock>();

            var tradeblk = await FindLatestBlockAsync(tradeid) as IUniTrade;
            if (tradeblk == null)
                return myvotes;

            var allvotes = await FindAllVotesByDaoAsync(tradeblk.Trade.daoId, false);
            foreach(var vote in allvotes)
            {
                if((vote as IVoting).Proposal.pptype == ProposalType.DisputeResolution)
                {
                    var pp = (vote as IVoting).Proposal.Deserialize() as ODRResolution;
                    if (pp != null && pp.TradeId == tradeid)
                        myvotes.Add(vote);
                }
            }

            return myvotes;
        }

        public async Task<VotingSummary> GetVoteSummaryAsync(string voteid)
        {
            // get all votes
            var latestblk = await FindLatestBlockAsync(voteid);
            if (latestblk != null)
            {
                if (latestblk.BlockType != BlockTypes.VoteGenesis
                    && latestblk.BlockType != BlockTypes.Voting)
                    throw new Exception("Invalid Vote ID");

                var vt = latestblk as IVoting;
                var blk = latestblk as TransactionBlock;

                // load
                var votes = new List<VotingBlock>();
                var genret = await FindBlockByIndexAsync(voteid, 1);
                var vg = genret as VotingGenesisBlock;

                for (int i = 2; i <= blk.Height; i++)
                {
                    var vret = await FindBlockByIndexAsync(voteid, i);
                    votes.Add(vret as VotingBlock);
                }

                return new VotingSummary
                {
                    Spec = vg,
                    Votes = votes,
                };
            }
            else
            {
                return null;
            }
        }

        public async Task<TransactionBlock> FindExecForVoteAsync(string voteid)
        {
            var filter = Builders<TransactionBlock>.Filter;
            FilterDefinition<TransactionBlock> filterDefination;

            filterDefination = filter.And(
                    filter.Or(filter.Eq("BlockType", BlockTypes.OrgnizationChange),
                        filter.Eq("BlockType", BlockTypes.UniTradeResolutionRecv)),
                    filter.Eq("voteid", voteid)
                );

            var q = await _blocks.OfType<TransactionBlock>()
                .FindAsync(filterDefination);

            return await q.FirstOrDefaultAsync();
        }

        // genesis account -> send: balance[nft/0000-00..] -= 1
        // receive -> balance[nft/0000-00..#serial] += 1
        // only this pair of block has .NonFungibleToken property. this property is not inheritable. 
        public async Task<SendTransferBlock> FindNFTGenesisSendAsync(string accountId, string ticker, string serial)
        {
            var q = _blocks.OfType<SendTransferBlock>()
                .AsQueryable()
                .Where(a => a.AccountID == accountId && a.NonFungibleToken != null
                        && a.NonFungibleToken.TokenCode == ticker && a.NonFungibleToken.SerialNumber == serial
                    );

            return await q.FirstOrDefaultAsync();
        }

        public async Task<List<BsonDocument>> GetBalanceAsync(string accountId)
        {
            PipelineDefinition<TransactionBlock, BsonDocument> pipeline = new BsonDocument[]
            {
                new BsonDocument("$match",
                new BsonDocument("AccountID", accountId)),
                new BsonDocument("$unwind",
                new BsonDocument("path", "$Balances")),
                new BsonDocument("$lookup",
                new BsonDocument
                    {
                        { "from", _networkId + "_blocks" },
                        { "localField", "Balances.k" },
                        { "foreignField", "Ticker" },
                        { "as", "result" }
                    }),
                new BsonDocument("$unwind",
                new BsonDocument("path", "$result")),
                new BsonDocument("$project",
                new BsonDocument
                    {
                        { "_id", 0 },
                        { "Author", "$result.AccountID" },
                        { "Time", "$result.TimeStamp.DateTime" },
                        { "Ticker", "$result.Ticker" },
                        { "Balance",
                new BsonDocument("$divide",
                new BsonArray
                            {
                                "$Balances.v",
                                LyraGlobal.TOKENSTORAGERITO
                            }) },
                        { "Domain", "$result.DomainName" },
                        { "Desc", "$result.Description" },
                        { "Name", "$result.Custom1" },
                        { "Url", "$result.Custom2" }
                    })
            };

            var q1 = _snapshots.Aggregate( pipeline);                

            var x = await q1.ToListAsync();

            return x;
        }

        public async Task<Block> FindLatestBlockByTimeAsync(string accountId, DateTime time)
        {
            var filter = Builders<Block>.Filter;
            var filterDefination = filter.And(
                filter.Eq("AccountID", accountId),
                filter.Lte("TimeStamp", time)
                );

            var q = _blocks
                .Find(filterDefination)
                .SortByDescending(a => a.TimeStamp)
                .Limit(1);

            return await q.FirstOrDefaultAsync();   // has account id, it must be a transaction block
        }

        public async Task FixDbRecordAsync()
        {
            //var filter = Builders<Block>.Filter;
            //var filterDefination = filter.Exists("TradeOrderId");

            //var q = _blocks
            //    .Find(filterDefination)
            //    .ToList();

            //foreach(var b in q)
            //{
            //    var tx = b as CancelTradeOrderBlock;
            //    if(tx != null && tx.TradeOrderId == null)
            //    {
            //        //Console.WriteLine($"Will process {tx.Hash}");
            //        var json = JsonConvert.SerializeObject(tx);
            //        var result = new BlockAPIResult
            //        {
            //            BlockData = json,
            //            ResultBlockType = tx.BlockType,
            //        };
            //        var txnew = result.GetBlock();

            //        await RemoveBlockAsync(tx.Hash);
            //        await Task.Delay(1);
            //        await AddBlockAsync(txnew);

            //        //Console.WriteLine($"Converted to {txnew.BlockType}");
            //    }
            //}

        }

    }
    public static class MyExtensions
    {
        public static bool ContainsNFTInstance(this List<NonFungibleToken> list, NonFungibleToken nft)
        {
            foreach (var item in list)
                if (item.Hash == nft.Hash)
                    return true;
            return false;
        }
    }

}