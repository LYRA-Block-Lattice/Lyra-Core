using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Cryptography;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize
{
    public class LyraClientForNode
    {
        DagSystem _sys;
        private LyraRestClient _client;

        public LyraClientForNode(DagSystem sys, LyraRestClient client)
        {
            _sys = sys;
            _client = client;
        }

        public async Task<string> SignAPICallAsync()
        {
            var syncInfo = await _client.GetSyncHeight();
            return Signatures.GetSignature(_sys.PosWallet.PrivateKey, syncInfo.SyncHash, _sys.PosWallet.AccountId);
        }

        internal async Task<BlockAPIResult> GetLastConsolidationBlockAsync()
        {            
            return await _client.GetLastConsolidationBlock();
        }

        internal async Task<MultiBlockAPIResult> GetBlocksByConsolidation(string consolidationHash)
        {
            return await _client.GetBlocksByConsolidation(_sys.PosWallet.AccountId, await SignAPICallAsync(), consolidationHash);
        }

        internal async Task<MultiBlockAPIResult> GetConsolidationBlocks(long startConsHeight)
        {
            return await _client.GetConsolidationBlocks(_sys.PosWallet.AccountId, await SignAPICallAsync(), startConsHeight);
        }

        public async Task<BlockAPIResult> GetBlockByHash(string Hash)
        {
            return await _client.GetBlockByHash(_sys.PosWallet.AccountId, Hash, await SignAPICallAsync());
        }

        public async Task<GetSyncStateAPIResult> GetSyncState()
        {
            return await _client.GetSyncState();
        }

        public async Task<GetListStringAPIResult> GetUnConsolidatedBlocks()
        {
            return await _client.GetUnConsolidatedBlocks(_sys.PosWallet.AccountId, await SignAPICallAsync());
        }
    }
}
