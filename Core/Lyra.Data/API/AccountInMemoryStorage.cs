using System;
using System.Linq;
using System.Collections.Generic;
using Lyra.Core.Blocks;

namespace Lyra.Core.Accounts
{
    // use it in client wallet and node's service account as a single account database
    public class AccountInMemoryStorage : IAccountDatabase
    {
        protected string? _privateKey;

        protected string? _accountId;

        protected string? _accountName;

        protected string? _voteFor;

        protected string? _networkId;

        public string? Name => _accountName;

        public string? PrivateKey => _privateKey;

        public string? NetworkId => _networkId;

        public string? AccountId => _accountId;

        public string? VoteFor { get => _voteFor; set => _voteFor = value; }

        public AccountInMemoryStorage()
        { }

        public void Delete(string? DatabaseName = null)
        {
            // Do nothing for now
            _accountName = null;
        }

        public void Open(string path, string accountName)
        {
            _accountName = accountName;
        }

        public bool Exists(string accountName)
        {
            return _accountName != null;
        }

        public bool Create(string accountName, string password, string networkId, string privateKey, string accountId, string voteFor)
        {
            _accountName = accountName;
            _privateKey = privateKey;
            _accountId = accountId;
            _voteFor = voteFor;
            _networkId = networkId;
            return true;
        }

        public void Dispose()
        {
            
        }
    }

}
