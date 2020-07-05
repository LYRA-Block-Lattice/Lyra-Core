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
        private LyraRestClient _client;

        public LyraClientForNode(LyraRestClient client)
        {
            _client = client;
        }

        public async Task<string> SignAPICallAsync()
        {
            var syncInfo = await _client.GetSyncHeight();
            return Signatures.GetSignature(DagSystem.Singleton.PosWallet.PrivateKey, syncInfo.SyncHash, DagSystem.Singleton.PosWallet.AccountId);
        }

        internal async Task<BlockAPIResult> GetLastConsolidationBlockAsync()
        {            
            return await _client.GetLastConsolidationBlock(DagSystem.Singleton.PosWallet.AccountId, await SignAPICallAsync());
        }

        internal async Task<MultiBlockAPIResult> GetBlocksByConsolidation(string consolidationHash)
        {
            return await _client.GetBlocksByConsolidation(DagSystem.Singleton.PosWallet.AccountId, await SignAPICallAsync(), consolidationHash);
        }

        internal async Task<MultiBlockAPIResult> GetConsolidationBlocks(long startConsHeight)
        {
            return await _client.GetConsolidationBlocks(DagSystem.Singleton.PosWallet.AccountId, await SignAPICallAsync(), startConsHeight);
        }

        public async Task<BlockAPIResult> GetBlockByHash(string Hash)
        {
            return await _client.GetBlockByHash(DagSystem.Singleton.PosWallet.AccountId, Hash, await SignAPICallAsync());
        }

        public async Task<GetSyncStateAPIResult> GetSyncState()
        {
            return await _client.GetSyncState();
        }

        public async Task<GetListStringAPIResult> GetUnConsolidatedBlocks()
        {
            return await _client.GetUnConsolidatedBlocks(DagSystem.Singleton.PosWallet.AccountId, await SignAPICallAsync());
        }
    }
}
