using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Accounts
{
    public class InMemWallet
    {
        private Wallet _wallet = null;
        private string _privatekey = null;
        private string _accountid = null;

        public Wallet Wallet { get { return _wallet; } }
        public string PrivateKey { get { return _wallet != null ? _wallet.PrivateKey : _privatekey; } }
        public string AccountId { get { return _wallet != null ? _wallet.AccountId : _accountid; } }

        public InMemWallet()
        {

        }

        public void GenerateWallet()
        {
            (_privatekey, _accountid) = Signatures.GenerateWallet();
        }

        public async Task<APIResultCodes> RestoreAsync(string NetworkId, string NodeAPIURL, string PrivateKey)
        {
            try
            {
                var storage = new AccountInMemoryStorage();
                _wallet = Wallet.Create(storage, string.Empty, string.Empty, NetworkId, PrivateKey);

                var rpcClient = LyraRestClient.Create(NetworkId, "Windows", $"{LyraGlobal.PRODUCTNAME} Client Cli", "1.0a", NodeAPIURL);
                var syncResult = await _wallet.SyncAsync(rpcClient);
                if (syncResult != APIResultCodes.Success)
                    _wallet = null;
                return syncResult;
            }
            catch 
            {
                return APIResultCodes.FailedToSyncAccount;
            }

        }
    }
}
