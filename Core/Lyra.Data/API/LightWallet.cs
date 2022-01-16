using Lyra.Core.API;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Net;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API
{
    public class LightWallet
    {
        SecureString _securePrivateKey;
        string _accountId;
        string _networkId;
        LyraJsonRPCClient _client;

        public string AccountId => _accountId;
        public string NetworkId => _networkId;

        public LightWallet(SecureString privateKey, string networkId)
        {
            _securePrivateKey = privateKey;
            _networkId = networkId;
            _accountId = Signatures.GetAccountIdFromPrivateKey(new NetworkCredential("", _securePrivateKey).Password);
            _client = new LyraJsonRPCClient(_networkId, (s) => 
                Task.FromResult(Signatures.GetSignature(new NetworkCredential("", _securePrivateKey).Password, s, _accountId)));
        }

        public async Task<BalanceResult> GetBalanceAsync()
        {
            BalanceResult result = null;
            await _client.TestProcAsync(async (jsonRpc, cancellationToken) =>
            {
                result = await jsonRpc.InvokeWithCancellationAsync<BalanceResult>("Balance", new object[] { _accountId }, cancellationToken);
            }).ConfigureAwait(true);
            return result;
        }
        public async Task<BalanceResult> SendAsync(decimal amount, string dstAccountId, string ticker)
        {
            BalanceResult result = null;
            await _client.TestProcAsync(async (jsonRpc, cancellationToken) =>
            {
                result = await jsonRpc.InvokeWithCancellationAsync<BalanceResult>("Send", new object[] { _accountId, amount, dstAccountId, ticker }, cancellationToken);
            }).ConfigureAwait(true);
            return result;
        }

        public async Task<BalanceResult> ReceiveAsync()
        {
            BalanceResult result = null;
            await _client.TestProcAsync(async (jsonRpc, cancellationToken) =>
            {
                result = await jsonRpc.InvokeWithCancellationAsync<BalanceResult>("Receive", new object[] { _accountId }, cancellationToken);
            }).ConfigureAwait(true);
            return result;
        }
    }
}
