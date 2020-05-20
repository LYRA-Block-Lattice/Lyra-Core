using System;
using Lyra.Core.Cryptography;
using Lyra.Core.Blocks;
using System.Threading.Tasks;
using Lyra.Shared;

namespace Lyra.Core.Accounts
{
    /// <summary>
    /// An abstraction of a single persistant Lyra account data management.
    /// Both Wallet (client) and ServiceAccount (node) are derive from this class.
    /// The storage can be any database module that implements IAccountDatabase interface:
    /// InMemory (wallet only), LiteDB (both wallet and node), or MongoDB/Cosmos (node only).
    /// </summary>
    public class BaseAccount : IDisposable
    {
        protected readonly IAccountDatabase _storage;

        public string Path { get; set; }

        public string AccountName { get; set; }

        public string PrivateKey { get; set; }

        public string AccountId { get; set; }

        public string NetworkId { get; set; }

        public static string GetFullFolderName(string NetworkId, string FolderName)
        {
            return $"{Utilities.GetLyraDataDir(NetworkId)}{Utilities.PathSeperator}{FolderName}{Utilities.PathSeperator}";
        }

        public BaseAccount(string accountName, IAccountDatabase storage, string NetworkId)
        {
            AccountName = accountName;
            _storage = storage; //new AccountDatabase();
            this.NetworkId = NetworkId;
        }

        /// <summary>
        /// Delete service account database storage (for unit testing only)
        /// </summary>
        /// <param name="DatabaseName">
        /// Full name including path and file name
        /// </param>
        public void Delete(string DatabaseName)
        {
            _storage.Delete(DatabaseName);
        }

        public Block GetFirstBlock()
        {
                return _storage.FindFirstBlock();
        }

        public Block GetLatestBlock()
        {      
            return _storage.FindLatestBlock();
        }

        //public int GetBlockCount()
        //{
        //        Block latestblock = GetLatestBlock();
        //        if (latestblock != null)
        //            return latestblock.Index;
        //        else
        //            return 0;
        //}

        public virtual void AddBlock(Block block)
        {
            if (_storage.FindBlockByHash(block.Hash) != null)
                throw new Exception("BaseAccount: Block with such Hash already exists!");
            if (_storage.FindBlockByIndex(block.Height) != null)
                throw new Exception("BaseAccount: Block with such Index already exists!");

            _storage.AddBlock(block);
            //LatestBlock = block;
            //Console.WriteLine(string.Format("Stored block -> type: {0} index: {1} ", block.BlockType.ToString(), block.Index.ToString()));
        }

        //public bool AccountExistsLocally()
        //{
        //    return _storage.Exists(Path, AccountName);
        //}

        public bool AccountExistsLocally(string path, string accountName)
        {
            return _storage.Exists(path, accountName);
        }

        public virtual string OpenAccount(string path, string accountName)
        {
            _storage.Open(path, accountName);
            AccountName = accountName;
            Path = path;
            PrivateKey = _storage.GetPrivateKey();
            AccountId = _storage.GetAccountId();
      
            return null;
        }

        // Create a new account (AccountName is the local wallet name)
        // Returns wallet address
        public string CreateAccount(string path, string accountName, AccountTypes accountType)
        {
            if (AccountExistsLocally(path, accountName))
                throw new ApplicationException(String.Format(@"Account with name ""{0}"" already exists", AccountName));

            _storage.Open(path, accountName);
            AccountName = accountName;
            Path = path;

            var keys = Signatures.GenerateWallet();
            PrivateKey = keys.privateKey;
            _storage.StorePrivateKey(PrivateKey);
            AccountId = keys.AccountId;
            _storage.StoreAccountId(AccountId);

            return AccountId;
        }

        public Block FindBlockByHash(string hash)
        {
            return _storage.FindBlockByHash(hash);
        }

        //public int SendBlock(TransactionBlock block)
        //{

        //    return 0;
        //}

        public virtual void Dispose()
        {
            if (_storage != null)
                _storage.Dispose();
        }
    }
}
