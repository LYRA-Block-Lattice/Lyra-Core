using System;
using System.IO;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;
using Lyra.Core.Blocks.Service;
using Lyra.Core.Accounts;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace Lyra.Authorizer
{

  
    // use it in client wallet and node's service account as a single account database
    public class MongoServiceAccountDatabase : IAccountDatabase
    {
        //protected LiteDatabase _db = null;
        private MongoClient _Client;

        private IMongoCollection<Block> _blocks;

        protected IMongoCollection<AccountParam> _params;

        readonly string _ConnectionString = null;
        //string _NetworkId = null;
        //string _ShardId = null;
        //string _AccountName = null;

        IMongoDatabase _db;

        readonly string _DatabaseName;

        readonly string _BlockCollectionName;
        readonly string _ParamsCollectionName;

        public MongoServiceAccountDatabase(string ConnectionString, string DatabaseName, string AccountName, string NetworkId, string ShardId = "Primary")
        {
            _ConnectionString = ConnectionString;

            _DatabaseName = DatabaseName;
            //_NetworkId = NetworkId;
            //_ShardId = ShardId;
            //_AccountName = AccountName;

            _BlockCollectionName = NetworkId + "-" + ShardId + "-" + AccountName + "-blocks";
            _ParamsCollectionName = NetworkId + "-" + ShardId + "-" + AccountName + "-params";

            BsonClassMap.RegisterClassMap<SyncBlock>();
            BsonClassMap.RegisterClassMap<ServiceBlock>();
        }

        public void Delete(string Database = null)
        {
            if (GetClient() == null)
                return;

            if (GetDatabase() == null)
                return;

            GetDatabase().DropCollection(_BlockCollectionName);
            GetDatabase().DropCollection(_ParamsCollectionName);
        }

        private MongoClient GetClient()
        {
            if (_Client == null)
                _Client = new MongoClient(_ConnectionString);
            return _Client;
        }

        private IMongoDatabase GetDatabase()
        {
            if (_db == null)
                _db = GetClient().GetDatabase(_DatabaseName);
            return _db;
        }

        public bool Exists(string path, string accountName)
        {
            if (GetClient() == null)
                return false;

            if (GetDatabase() == null)
                return false;

            var collList = GetDatabase().ListCollectionNames().ToList();

            if (!collList.Contains(_ParamsCollectionName))
                return false;

            if (!collList.Contains(_BlockCollectionName))
                return false;

            return true;
        }

        public void Open(string path, string accountName)
        {
            _blocks = GetDatabase().GetCollection<Block>(_BlockCollectionName);
            _params = GetDatabase().GetCollection<AccountParam>(_ParamsCollectionName);

            //_blocks = _db.GetCollection<Block>("blocks");

            //    _blocks.EnsureIndex(x => x.Index);
            //    _blocks.EnsureIndex(x => x.Hash);
            //    _blocks.EnsureIndex(x => x.BlockType);
            //}
        }

        public Block FindFirstBlock()
        {
            var result = _blocks.Find(x => x.Index.Equals(1));
            if (result.Any())
                return result.First();
            else
                return null;
        }

        public Block FindLatestBlock()
        {
            //var builder = Builders<TransactionBlock>.Filter;
            //var filterDefinition = builder.Eq("Ticker", Ticker);

            //var result = _blocks.Find(filterDefinition);
            //if (result.Any())
            //    return result.First()



            var result = _blocks.Find(x => true).SortByDescending(y => y.Index).Limit(1);
            //var result = _blocks.Find(x => true).SortByDescending(y => y.Index);
            if (result.Any())
                return result.First();
            else
                return null;
        }

        public TokenGenesisBlock FindTokenGenesisBlockByTicker(string Ticker)
        {
            throw new ApplicationException("Not supported");
        }

        public Block FindBlockByHash(string hash)
        {
            var result = _blocks.Find(x => x.Hash.Equals(hash));
            if (result.Any()) //  CountDocuments() == 1)
                return result.First();
            else
                return null;
        }

        public Block FindBlockByIndex(int index)
        {
            var result = _blocks.Find(x => x.Index.Equals(index));
            if (result.Any()) 
                return result.First();
            else
                return null;
        }

        public long GetBlockCount()
        {
            if (_blocks != null)
                return _blocks.CountDocuments(x => true);
            return 0;
        }

        public void AddBlock(Block block)
        {
            if (FindBlockByHash(block.Hash) != null)
                throw new Exception("ServiceAccountDatabase=>AddBlock: Block with such Hash already exists!");

            if (FindBlockByIndex(block.Index) != null)
                throw new Exception("ServiceAccountDatabase=>AddBlock: Block with such Index already exists!");

            _blocks.InsertOne(block);
        }

        // To DO - add encryption with user password
        public void StorePrivateKey(string PrivateKey)
        {
            _params.InsertOne(new AccountParam() { Name = "PrivateKey", Value = PrivateKey });
        }

        public void StoreAccountId(string AccountId)
        {
            _params.InsertOne(new AccountParam() { Name = "AccountId", Value = AccountId });
        }

        public string GetPrivateKey()
        {
            var result = _params.Find(x => x.Name.Equals("PrivateKey")).First();
            if (result != null)
                return result.Value;
            else
                return null;
        }

        public string GetAccountId()
        {
            var result = _params.Find(x => x.Name.Equals("AccountId")).First();
            if (result != null)
                return result.Value;
            else
                return null;
        }

        public void SaveTokenInfo(TokenGenesisBlock tokenGewnesisBlock)
        {
            throw new ApplicationException("Not supported");
        }

        public TokenGenesisBlock GetTokenInfo(string token)
        {
            throw new ApplicationException("Not supported");
        }

        public void Dispose()
        {
            // do nothing here
        }
    }

}
