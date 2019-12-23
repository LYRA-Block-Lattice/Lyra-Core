using System;
using Lyra.Core.Cryptography;
using Lyra.Core.Blocks;

namespace Lyra.Core.Accounts
{
    /// <summary>
    /// An abstraction of a single persistant Lyra account data management.
    /// Both Wallet (client) and ServiceAccount (node) are derive from this class.
    /// The storage can be any database module that implements IAccountDatabase interface:
    /// InMemory (wallet only), LiteDB (both wallet and node), or MongoDB/Cosmos (node only).
    /// </summary>
    public abstract class BaseAccount : IDisposable
    {
        protected readonly IAccountDatabase _storage;

        public string Path { get; set; }

        public string AccountName { get; set; }

        public string PrivateKey { get; set; }

        public string AccountId { get; set; }

        public string NetworkId { get; set; }

        private static string GetHomePath()
        {
            return (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX) ?
                Environment.GetEnvironmentVariable("HOME") + @"/" :
                Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%") + @"\" ;
        }

        public static string GetFullPath(string FullFolderName)
        {

            return (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX) ?
                FullFolderName + @"/" :
                FullFolderName + @"\";
        }

        public static string GetFullFolderName(string FolderName)
        {
            return GetHomePath() + FolderName;
        }

        protected BaseAccount(string accountName, IAccountDatabase storage, string NetworkId)
        {
            AccountName = accountName;
            _storage = storage; //new AccountDatabase();
            this.NetworkId = NetworkId;
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
            if (_storage.FindBlockByIndex(block.Index) != null)
                throw new Exception("BaseAccount: Block with such Index already exists!");

            _storage.AddBlock(block);
            //LatestBlock = block;
            Console.WriteLine(string.Format("Stored block -> type: {0} index: {1} ", block.BlockType.ToString(), block.Index.ToString()));
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

            //OpenBlock block = new OpenBlock
            //{
            generateAccountKeys();
            //AccountType = accountType;
            //};
            //block.InitializeBlock(null);
            //_storage.AddBlock(block);
            //FirstBlock = block;
            //LatestBlock = block;
            return AccountId;

            void generateAccountKeys()
            {
                ISignatures signr = new Signatures();
                PrivateKey = signr.GenerateWallet().privateKey;
                _storage.StorePrivateKey(PrivateKey);
                AccountId = signr.GetAccountIdFromPrivateKey(PrivateKey);
                _storage.StoreAccountId(AccountId);

            }

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
