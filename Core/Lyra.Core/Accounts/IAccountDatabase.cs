using System;
//using Java.Security;
using Lyra.Core.Blocks;

namespace Lyra.Core.Accounts
{
    /// <summary>
    /// hold wallets
    /// </summary>
    public interface IAccountDatabase : IDisposable
    {
        public string PrivateKey { get; }
        public string NetworkId { get; }
        public string AccountId { get; }
        public string VoteFor { get; set; }
        bool Exists(string accountName);
        void Open(string accountName, string password);
        bool Create(string accountName, string password, string networkId, string privateKey, string accountId, string voteFor);
        void Delete(string accountName);
    }
}