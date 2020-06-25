using System;
using Lyra.Core.Blocks;

namespace Lyra.Core.Accounts
{
    /// <summary>
    /// hold wallets
    /// </summary>
    public interface IAccountDatabase : IDisposable
    {
        bool Exists(string path, string accountName);

        // Opens account database; creates a new one if it does not already exists
        void Open(string path, string accountName);

        Block FindFirstBlock();

        Block FindLatestBlock();

        Block FindBlockByHash(string hash);

        Block FindBlockByIndex(long index);

        TokenGenesisBlock FindTokenGenesisBlockByTicker(string Ticker);
      
        long GetBlockCount();

        void AddBlock(Block block);

        // To DO - add encryption with user password
        void StorePrivateKey(string PrivateKey);

        string GetPrivateKey();

        // This is currently Public Key
        void StoreAccountId(string AccountId);

        string GetAccountId();

        void StoreVoteFor(string VoteFor);
        string GetVoteFor();

        void SaveTokenInfo(TokenGenesisBlock tokenGewnesisBlock);

        TokenGenesisBlock GetTokenInfo(string token);

        void Delete(string DatabaseName = null);

        void Reset();
    }
}