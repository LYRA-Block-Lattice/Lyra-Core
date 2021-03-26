using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.API;
using Lyra.Data.Crypto;
using Neo;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize
{
    // this class never fail.
    // if failed, then all seeds are failed. if all seeds failed, then why should this exists.
    public class LyraClientForNode
    {
        DagSystem _sys;
        private LyraAggregatedClient _client;

        public LyraAggregatedClient Client { get => _client; set => _client = value; }

        public LyraClientForNode(DagSystem sys)
        {
            _sys = sys;
        }

        internal async Task<BlockAPIResult> GetLastConsolidationBlockAsync()
        {
            try
            {
                if (_client == null)
                    _client = await FindValidSeedForSyncAsync();

                return await _client.GetLastConsolidationBlock();
            }
            catch (Exception ex)
            {
                if (ex is TaskCanceledException || ex is HttpRequestException || ex.Message == "Web Api Failed.")
                {
                    // retry
                    _client = await FindValidSeedForSyncAsync();
                    return await GetLastConsolidationBlockAsync();
                }
                else
                    throw ex;
            }
            
        }

        internal async Task<MultiBlockAPIResult> GetBlocksByConsolidation(string consolidationHash)
        {
            try
            {
                if (_client == null)
                    _client = await FindValidSeedForSyncAsync();

                var result = await _client.GetBlocksByConsolidation(_sys.PosWallet.AccountId, null, consolidationHash);
                if (result.ResultCode == APIResultCodes.APISignatureValidationFailed)
                {
                    return await GetBlocksByConsolidation(consolidationHash);
                }
                else
                    return result;
            }
            catch (Exception ex)
            {
                if (ex is TaskCanceledException || ex is HttpRequestException || ex.Message == "Web Api Failed.")
                {
                    // retry
                    _client = await FindValidSeedForSyncAsync();
                    return await GetBlocksByConsolidation(consolidationHash);
                }
                else
                    throw ex;
            }
            
        }

        internal async Task<MultiBlockAPIResult> GetConsolidationBlocks(long startConsHeight)
        {
            try
            {
                if (_client == null)
                    _client = await FindValidSeedForSyncAsync();

                var result = await _client.GetConsolidationBlocks(_sys.PosWallet.AccountId, null, startConsHeight, 10);
                if (result.ResultCode == APIResultCodes.APISignatureValidationFailed)
                {
                    return await GetConsolidationBlocks(startConsHeight);
                }
                else
                    return result;
            }
            catch (Exception ex)
            {
                if (ex is TaskCanceledException || ex is HttpRequestException || ex.Message == "Web Api Failed.")
                {
                    // retry
                    _client = await FindValidSeedForSyncAsync();
                    return await GetConsolidationBlocks(startConsHeight);
                }
                else
                    throw ex;
            }
        }

        public async Task<BlockAPIResult> GetBlockByHash(string Hash)
        {
            try
            {
                if (_client == null)
                    _client = await FindValidSeedForSyncAsync();

                return await _client.GetBlockByHash(_sys.PosWallet.AccountId, Hash, "");
            }
            catch (Exception ex)
            {
                if (ex is TaskCanceledException || ex is HttpRequestException || ex.Message == "Web Api Failed.")
                {
                    // retry
                    _client = await FindValidSeedForSyncAsync();
                    return await GetBlockByHash(Hash);
                }
                else
                    throw ex;
            }            
        }

        public async Task<GetSyncStateAPIResult> GetSyncState()
        {
            try
            {
                if (_client == null)
                    _client = await FindValidSeedForSyncAsync();

                return await _client.GetSyncState();
            }
            catch (Exception ex)
            {
                if (ex is TaskCanceledException || ex is HttpRequestException || ex.Message == "Web Api Failed.")
                {
                    // retry
                    _client = await FindValidSeedForSyncAsync();
                    return await GetSyncState();
                }
                else
                    throw ex;
            }
        }

        public async Task<MultiBlockAPIResult> GetBlocksByTimeRange(DateTime startTime, DateTime endTime)
        {
            try
            {
                if (_client == null)
                    _client = await FindValidSeedForSyncAsync();

                return await _client.GetBlocksByTimeRange(startTime, endTime);
            }
            catch (Exception ex)
            {
                if (ex is TaskCanceledException || ex is HttpRequestException || ex.Message == "Web Api Failed.")
                {
                    // retry
                    _client = await FindValidSeedForSyncAsync();
                    return await GetBlocksByTimeRange(startTime, endTime);
                }
                else
                    throw ex;
            }
        }

        public async Task<GetListStringAPIResult> GetBlockHashesByTimeRange(DateTime startTime, DateTime endTime)
        {
            try
            {
                if (_client == null)
                    _client = await FindValidSeedForSyncAsync();

                return await _client.GetBlockHashesByTimeRange(startTime, endTime);
            }
            catch (Exception ex)
            {
                if (ex is TaskCanceledException || ex is HttpRequestException || ex.Message == "Web Api Failed.")
                {
                    // retry
                    _client = await FindValidSeedForSyncAsync();
                    return await GetBlockHashesByTimeRange(startTime, endTime);
                }
                else
                    throw ex;
            }
        }

        public async Task<BlockAPIResult> GetServiceGenesisBlock()
        {
            try
            {
                if (_client == null)
                    _client = await FindValidSeedForSyncAsync();

                return await _client.GetServiceGenesisBlock();
            }
            catch (Exception ex)
            {
                if (ex is TaskCanceledException || ex is HttpRequestException || ex.Message == "Web Api Failed.")
                {
                    // retry
                    _client = await FindValidSeedForSyncAsync();
                    return await GetServiceGenesisBlock();
                }
                else
                    throw ex;
            }
        }

        public async Task<BlockAPIResult> GetLastServiceBlock()
        {
            try
            {
                if (_client == null)
                    _client = await FindValidSeedForSyncAsync();

                return await _client.GetLastServiceBlock();
            }
            catch (Exception ex)
            {
                if (ex is TaskCanceledException || ex is HttpRequestException || ex.Message == "Web Api Failed.")
                {
                    // retry
                    _client = await FindValidSeedForSyncAsync();
                    return await GetLastServiceBlock();
                }
                else
                    throw ex;
            }
        }

        public async Task<LyraAggregatedClient> FindValidSeedForSyncAsync()
        {
            var client = new LyraAggregatedClient(Neo.Settings.Default.LyraNode.Lyra.NetworkId);
            await client.InitAsync();
            return client;
        }
    }
}
