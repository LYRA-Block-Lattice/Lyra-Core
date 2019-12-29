using System;
using System.IO;
using Lyra.Core.Blocks;
using Lyra.Core.Accounts;
using LiteDB;


namespace Lyra.Core.LiteDB
{

  
    // use it in client wallet and node's service account as a single account database
    public class LiteAccountDatabase : IAccountDatabase
    {
        protected LiteDatabase _db = null;

        private LiteCollection<Block> _blocks = null;

        protected LiteCollection<AccountParam> _params = null;

        protected LiteCollection<TokenGenesisBlock> _tokeninfo = null;

        protected string _DatabaseName; 

        public void Delete(string Database = null)
        {
            string filename;
            if (Database == null)
                filename = _DatabaseName + ".db";
            else
                filename = Database + ".db";

            if (!string.IsNullOrWhiteSpace(filename))
                if (File.Exists(filename))
                    File.Delete(filename);
        }

        public bool Exists(string path, string accountName)
        {
            if (string.IsNullOrEmpty(accountName))
                return false;
            string fileName = accountName + ".db";
            if (!string.IsNullOrWhiteSpace(path))
                fileName = path + fileName;

            return File.Exists(fileName);
        }

        public void Open(string path, string accountName)
        {
            if (_db == null)
            {
                _DatabaseName = accountName;
                var fileName = accountName + ".db";
                if (!string.IsNullOrWhiteSpace(path))
                    fileName = path + fileName;
                string connectionString = "Filename=" + fileName + ";Mode=Exclusive";
                _db = new LiteDatabase(connectionString);
                _blocks = _db.GetCollection<Block>("blocks");

                _blocks.EnsureIndex(x => x.Index);
                _blocks.EnsureIndex(x => x.Hash);
                _blocks.EnsureIndex(x => x.BlockType);
            }
        }

        public Block FindFirstBlock()
        {
            var result = _blocks.FindOne(x => x.Index.Equals(1));
            return result;
        }

        public Block FindLatestBlock()
        {
            //var count = GetBlockCount();
            //var result = FindBlockByIndex(count);
            int max = 0;

            var m = _blocks.Max(x => x.Index);
            if (m != null)
                max = m.AsInt32;

            if (max > 0)
                return (Block)_blocks.FindOne(Query.EQ("Index", max));
            return null;
        }

        public TokenGenesisBlock FindTokenGenesisBlockByTicker(string Ticker)
        {
            // to do - try to replace this by indexed search using BlockType indexed field (since we can't index Ticker field):
            // find all GenesysBlocks first, then check if one of them has the right ticker
            if (!string.IsNullOrEmpty(Ticker))
            {
                var result = _blocks.FindOne(Query.EQ("Ticker", Ticker));
                if (result != null)
                    return result as TokenGenesisBlock;
            }

            return null;
        }

        public Block FindBlockByHash(string hash)
        {
            var result = _blocks.FindOne(x => x.Hash.Equals(hash));
            return (Block)result;
        }

        public Block FindBlockByIndex(long index)
        {
            var result = _blocks.FindOne(x => x.Index.Equals(index));
            return (Block)result;
        }

        public long GetBlockCount()
        {
            if (_blocks != null)
                return _blocks.Count();
            return 0;
        }

        public void AddBlock(Block block)
        {
            _blocks.Insert(block);
        }

        // To DO - add encryption with user password
        public void StorePrivateKey(string PrivateKey)
        {
            GetParamsCollection().Insert(new AccountParam() { Name = "PrivateKey", Value = PrivateKey });
        }

        public void StoreAccountId(string AccountId)
        {
            GetParamsCollection().Insert(new AccountParam() { Name = "AccountId", Value = AccountId });
        }

        public string GetPrivateKey()
        {
            var result = GetParamsCollection().FindOne(x => x.Name.Equals("PrivateKey"));
            if (result != null)
                return result.Value;
            else
                return null;
        }

        public string GetAccountId()
        {
            var result = GetParamsCollection().FindOne(x => x.Name.Equals("AccountId"));
            if (result != null)
                return result.Value;
            else
                return null;
        }

        public void SaveTokenInfo(TokenGenesisBlock tokenGewnesisBlock)
        {
            if (tokenGewnesisBlock != null && GetTokenInfo(tokenGewnesisBlock.Ticker) == null)
                GetTokenInfoCollection().Insert(tokenGewnesisBlock);
        }

        public TokenGenesisBlock GetTokenInfo(string token)
        {
            if (!string.IsNullOrEmpty(token))
            {
                var result = GetTokenInfoCollection().FindOne(Query.EQ("Ticker", token));
                if (result != null)
                    return result as TokenGenesisBlock;
            }

            return null;
        }

        private LiteCollection<AccountParam> GetParamsCollection()
        {
            if (_params == null)
                _params = _db.GetCollection<AccountParam>("params");
            return _params;
        }

        private LiteCollection<TokenGenesisBlock> GetTokenInfoCollection()
        {
            if (_tokeninfo == null)
                _tokeninfo = _db.GetCollection<TokenGenesisBlock>("tokeninfo");
            return _tokeninfo;
        }


        public void Dispose()
        {
            if (_db != null)
                _db.Dispose();
        }
    }

}
