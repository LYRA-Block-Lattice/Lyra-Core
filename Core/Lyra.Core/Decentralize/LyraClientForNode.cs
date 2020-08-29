using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Cryptography;
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
        private LyraRestClient _client;

        public LyraClientForNode(DagSystem sys)
        {
            _sys = sys;
        }

        public async Task<string> SignAPICallAsync()
        {
            try
            {
                if(_client == null)
                    _client = await FindValidSeedForSyncAsync(_sys);

                var syncInfo = await _client.GetSyncHeight();
                return Signatures.GetSignature(_sys.PosWallet.PrivateKey, syncInfo.SyncHash, _sys.PosWallet.AccountId);
            }
            catch(Exception ex)
            {
                if (ex is TaskCanceledException || ex is HttpRequestException || ex.Message == "Web Api Failed.")
                {
                    // retry
                    _client = await FindValidSeedForSyncAsync(_sys);
                    return await SignAPICallAsync();
                }
                else
                    throw ex;
            }
        }

        internal async Task<BlockAPIResult> GetLastConsolidationBlockAsync()
        {
            try
            {
                if (_client == null)
                    _client = await FindValidSeedForSyncAsync(_sys);

                return await _client.GetLastConsolidationBlock();
            }
            catch (Exception ex)
            {
                if (ex is TaskCanceledException || ex is HttpRequestException || ex.Message == "Web Api Failed.")
                {
                    // retry
                    _client = await FindValidSeedForSyncAsync(_sys);
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
                    _client = await FindValidSeedForSyncAsync(_sys);

                return await _client.GetBlocksByConsolidation(_sys.PosWallet.AccountId, await SignAPICallAsync(), consolidationHash);
            }
            catch (Exception ex)
            {
                if (ex is TaskCanceledException || ex is HttpRequestException || ex.Message == "Web Api Failed.")
                {
                    // retry
                    _client = await FindValidSeedForSyncAsync(_sys);
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
                    _client = await FindValidSeedForSyncAsync(_sys);

                return await _client.GetConsolidationBlocks(_sys.PosWallet.AccountId, await SignAPICallAsync(), startConsHeight);
            }
            catch (Exception ex)
            {
                if (ex is TaskCanceledException || ex is HttpRequestException || ex.Message == "Web Api Failed.")
                {
                    // retry
                    _client = await FindValidSeedForSyncAsync(_sys);
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
                    _client = await FindValidSeedForSyncAsync(_sys);

                return await _client.GetBlockByHash(_sys.PosWallet.AccountId, Hash, await SignAPICallAsync());
            }
            catch (Exception ex)
            {
                if (ex is TaskCanceledException || ex is HttpRequestException || ex.Message == "Web Api Failed.")
                {
                    // retry
                    _client = await FindValidSeedForSyncAsync(_sys);
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
                    _client = await FindValidSeedForSyncAsync(_sys);

                return await _client.GetSyncState();
            }
            catch (Exception ex)
            {
                if (ex is TaskCanceledException || ex is HttpRequestException || ex.Message == "Web Api Failed.")
                {
                    // retry
                    _client = await FindValidSeedForSyncAsync(_sys);
                    return await GetSyncState();
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
                    _client = await FindValidSeedForSyncAsync(_sys);

                return await _client.GetBlockHashesByTimeRange(startTime, endTime);
            }
            catch (Exception ex)
            {
                if (ex is TaskCanceledException || ex is HttpRequestException || ex.Message == "Web Api Failed.")
                {
                    // retry
                    _client = await FindValidSeedForSyncAsync(_sys);
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
                    _client = await FindValidSeedForSyncAsync(_sys);

                return await _client.GetServiceGenesisBlock();
            }
            catch (Exception ex)
            {
                if (ex is TaskCanceledException || ex is HttpRequestException || ex.Message == "Web Api Failed.")
                {
                    // retry
                    _client = await FindValidSeedForSyncAsync(_sys);
                    return await GetServiceGenesisBlock();
                }
                else
                    throw ex;
            }
        }

        public static async Task<LyraRestClient> FindValidSeedForSyncAsync(DagSystem sys)
        {
            do
            {
                var rand = new Random();
                int ndx;

                using (RNGCryptoServiceProvider rg = new RNGCryptoServiceProvider())
                {
                    do
                    {
                        byte[] rno = new byte[5];
                        rg.GetBytes(rno);
                        int randomvalue = BitConverter.ToInt32(rno, 0);

                        ndx = randomvalue % ProtocolSettings.Default.SeedList.Length;
                    } while (ndx < 0 || sys.PosWallet.AccountId == ProtocolSettings.Default.StandbyValidators[ndx]);
                }

                var addr = ProtocolSettings.Default.SeedList[ndx].Split(':')[0];
                var apiUrl = $"http://{addr}:4505/api/Node/";
                //_log.LogInformation("Platform {1} Use seed node of {0}", apiUrl, Environment.OSVersion.Platform);
                var client = LyraRestClient.Create(Neo.Settings.Default.LyraNode.Lyra.NetworkId, Environment.OSVersion.Platform.ToString(), "LyraNoded", "1.7", apiUrl);
                var mode = await client.GetSyncState();
                if (mode.ResultCode == APIResultCodes.Success)
                {
                    return client;
                }
                await Task.Delay(10000);    // incase of hammer
            } while (true);
        }
    }
}
