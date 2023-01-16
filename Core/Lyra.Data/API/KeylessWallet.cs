using Lyra.Core.API;
using Lyra.Core.Blocks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lyra.Data.Utils;
using Lyra.Data.API;
using Lyra.Data.Blocks;
using Lyra.Core.Accounts;
using Org.BouncyCastle.Crypto.Agreement.Srp;

namespace Lyra.Data.API
{
    public class KeylessWallet : Wallet
    {
        private string _accountId;
        private Func<string, Task<string>> _signer;
        private string _networkId;

        public override string AccountId => _accountId;
        public override string PrivateKey => throw new InvalidOperationException("This is keyless wallet.");
        public override string NetworkId => _networkId;

        public KeylessWallet(string accountId, Func<string, Task<string>> signer, string networkId)
        {
            _accountId = accountId;
            _signer = signer;
            _networkId = networkId;

            // try always use localhost
            var port = networkId == "mainnet" ? 5504 : 4504;
            _rpcClient = LyraRestClient.Create(networkId,
                                Environment.OSVersion.Platform.ToString(),
                                "JsonServer", "1.0",
                                $"https://localhost:{port}/api/Node/"
                            );
        }

        //public async Task<Dictionary<string, long>> GetBalanceAsync()
        //{
        //    var lastTx = await GetLatestBlockAsync();
        //    if (lastTx == null)
        //        return null;
        //    return lastTx.Balances;
        //}

        protected override Task InitBlockAsync(Block block, Block prevBlock)
        {
            return block.InitializeBlockAsync(prevBlock, _signer);
        }

        //private async Task<string[]> GetProperTokenNameAsync(string[] tokenNames)
        //{
        //    var result = await tokenNames.SelectAsync(async a => await _rpcClient.GetTokenGenesisBlockAsync(AccountId, a, null));
        //    return result.Select(a => a.GetBlock() as TokenGenesisBlock)
        //        .Select(b => b?.Ticker)
        //        .OrderBy(a => a)
        //        .ToArray();
        //}

        //public async Task<PoolInfoAPIResult> GetLiquidatePoolAsync(string token0, string token1)
        //{
        //    var result = await _node.GetPoolAsync(token0, token1);
        //    return result;
        //}
    }
}