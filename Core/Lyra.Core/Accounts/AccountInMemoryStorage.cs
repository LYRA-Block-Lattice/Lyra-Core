using System;
using System.Linq;
using System.Collections.Generic;
using Lyra.Core.Blocks;

namespace Lyra.Core.Accounts
{
    // use it in client wallet and node's service account as a single account database
    public class AccountInMemoryStorage : IAccountDatabase
    {
        //        protected LiteDatabase _db = null;

        private List<Block> _blocks = new List<Block>();

        //protected LiteCollection<Param> _params = null;

        private List<TokenGenesisBlock> _tokeninfo = new List<TokenGenesisBlock>();

        private string _privateKey;

        private string _accountId;

        private string _accountName;

        public AccountInMemoryStorage()
        { }

        public void Delete(string DatabaseName = null)
        {
            // Do nothing for now
        }

        public AccountInMemoryStorage(string PrivateKey, string AccountId, string AccountName)
        {
            _privateKey = PrivateKey;
            _accountId = AccountId;
            _accountName = AccountName;
        }

        public bool Exists(string path, string accountName)
        {
            if (string.IsNullOrEmpty(accountName))
                return false;

            if (string.IsNullOrEmpty(_accountName))
                return false;

            if (_accountName != accountName)
                return false;

            return true;
        }

        public void Open(string path, string accountName)
        {
            _accountName = accountName;
            //throw new Exception("Not supported");
            //if (_db == null)
            //{
            //    string fileName = accountName + ".db";
            //    string connectionString = "Filename=" + fileName + ";Mode=Exclusive";
            //    _db = new LiteDatabase(connectionString);
            //    _blocks = _db.GetCollection<Block>("blocks");

            //    _blocks.EnsureIndex(x => x.Index);
            //    _blocks.EnsureIndex(x => x.Hash);
            //    _blocks.EnsureIndex(x => x.BlockType);
            //}
        }

        public Block FindFirstBlock()
        {
            if (_blocks.Count == 0)
                return null;

            var result = _blocks.Find(x => x.Index.Equals(1));
            return result;
        }

        public Block FindLatestBlock()
        {
            if (_blocks.Count == 0)
                return null;

            var max = _blocks.Max(x => x.Index);

            if (max > 0)
                return FindBlockByIndex(max);
            return null;
        }

        public TokenGenesisBlock FindTokenGenesisBlockByTicker(string Ticker)
        {
            if (!string.IsNullOrEmpty(Ticker))
            {
                if (_tokeninfo.Count == 0)
                    return null;

                return _tokeninfo.Find(x => x.Ticker.Equals(Ticker, StringComparison.InvariantCultureIgnoreCase));
            }

            return null;
        }

        public Block FindBlockByHash(string hash)
        {
            if (_blocks.Count == 0)
                return null;

            var result = _blocks.Find(x => x.Hash.Equals(hash));
            return result;
        }

        public Block FindBlockByIndex(long index)
        {
            if (_blocks.Count == 0)
                return null;

            var result = _blocks.Find(x => x.Index.Equals(index));
            return result;
        }

        public long GetBlockCount()
        {
            return _blocks.Count();
        }

        public void AddBlock(Block block)
        {
            _blocks.Add(block);
        }

        // To DO - add encryption with user password
        public void StorePrivateKey(string PrivateKey)
        {
            _privateKey = PrivateKey;
        }

        public void StoreAccountId(string AccountId)
        {
            _accountId = AccountId;
        }

        public string GetPrivateKey()
        {
            return _privateKey;
        }

        public string GetAccountId()
        {
            return _accountId;
        }

        public void SaveTokenInfo(TokenGenesisBlock tokenGewnesisBlock)
        {
            if (tokenGewnesisBlock != null && GetTokenInfo(tokenGewnesisBlock.Ticker) == null)
                _tokeninfo.Add(tokenGewnesisBlock);
        }

        public TokenGenesisBlock GetTokenInfo(string token)
        {
            if (!string.IsNullOrEmpty(token))
            {
                if (_tokeninfo.Count == 0)
                    return null;

                var result = _tokeninfo.Find(x => x.Ticker.Equals(token));
                return result;
            }

            return null;
        }

        public void Dispose()
        {
        }
    }

}
