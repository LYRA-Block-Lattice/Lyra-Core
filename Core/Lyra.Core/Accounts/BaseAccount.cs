//using System;
//using Lyra.Data.Crypto;
//using Lyra.Core.Blocks;
//using System.Threading.Tasks;
//using Lyra.Shared;
//using Lyra.Core.API;

//namespace Lyra.Core.Accounts
//{
//    /// <summary>
//    /// An abstraction of a single persistant Lyra account data management.
//    /// Both Wallet (client) and ServiceAccount (node) are derive from this class.
//    /// The storage can be any database module that implements IAccountDatabase interface:
//    /// InMemory (wallet only), LiteDB (both wallet and node), or MongoDB/Cosmos (node only).
//    /// </summary>
//    public class BaseAccount : IDisposable
//    {
//        protected readonly IAccountDatabase _storage;

//        public string Path { get; set; }

//        public string AccountName { get; }

//        public string PrivateKey => _storage.PrivateKey;

//        public string AccountId => _storage.AccountId;

//        public string VoteFor => _storage.VoteFor;

//        public string NetworkId => _storage.NetworkId;

//        public static string GetFullFolderName(string NetworkId, string FolderName)
//        {
//            return $"{Utilities.GetLyraDataDir(NetworkId, LyraGlobal.OFFICIALDOMAIN)}{Utilities.PathSeperator}{FolderName}{Utilities.PathSeperator}";
//        }

//        public BaseAccount(IAccountDatabase storage)
//        {
//            _storage = storage;
//        }

//        public bool AccountExistsLocally(string accountName)
//        {
//            return _storage.Exists(accountName);
//        }

//        public virtual string OpenAccount(string path, string accountName)
//        {
//            _storage.Open(path, accountName);
//            AccountName = accountName;
//            Path = path;
//            PrivateKey = _storage.GetPrivateKey();
//            AccountId = _storage.GetAccountId();
      
//            return null;
//        }

//        // Create a new account (AccountName is the local wallet name)
//        // Returns wallet address
//        public string CreateAccount(string path, string accountName, AccountTypes accountType)
//        {
//            if (AccountExistsLocally(path, accountName))
//                throw new Exception(String.Format(@"Account with name ""{0}"" already exists", AccountName));

//            _storage.Open(path, accountName);
//            AccountName = accountName;
//            Path = path;

//            var keys = Signatures.GenerateWallet();
//            PrivateKey = keys.privateKey;
//            _storage.StorePrivateKey(PrivateKey);
//            AccountId = keys.AccountId;
//            _storage.StoreAccountId(AccountId);

//            return AccountId;
//        }

//        public virtual void Dispose()
//        {
//            if (_storage != null)
//                _storage.Dispose();
//        }
//    }
//}
