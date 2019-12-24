using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lyra.Authorizer.Decentralize;
using Lyra.Core.Accounts;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Service;
using Lyra.Core.Cryptography;
using Microsoft.Extensions.Options;
using Orleans;
using Lyra.Core.Utils;

namespace Lyra.Authorizer.Services
{
    public class ServiceAccount
    {
        public const string SERVICE_ACCOUNT_NAME = "service_account";
        public string DatabasePath { get; set; }

        Timer timer = null;
        private LyraNodeConfig _config;

        ISignatures _signr;
        BaseAccount _ba;

        public bool IsNodeFullySynced { get; set; }

        //public Dictionary<string, string> TokenGenesisBlocks { get; set; }

        public ServiceAccount(IClusterClient client, IAccountDatabase storage, IOptions<LyraNodeConfig> config) 
        {
            IsNodeFullySynced = false;
            _config = config.Value;

            _signr = client.GetGrain<ISignaturesForGrain>(0);
            _ba = new BaseAccount(_signr, SERVICE_ACCOUNT_NAME, storage, _config.Lyra.NetworkId);

            Task.Run(() => StartAsync(true, null));            
        }

        public ServiceBlock GetLastServiceBlock()
        {
            //var lstServiceBlock = base._storage. _blocks.FindOne(Query.And(Query.EQ("AccountID", AccountId), Query.EQ("SourceHash", sendBlock.Hash)));
            Block lastBlock = _ba.GetLatestBlock();
            if (lastBlock.BlockType == BlockTypes.Service)
                return lastBlock as ServiceBlock;
            if (lastBlock == null)
                return null;
            string hash = (lastBlock as SyncBlock).LastServiceBlockHash;
            ServiceBlock lastServiceBlock = _ba.FindBlockByHash(hash) as ServiceBlock;
            return lastServiceBlock;
        }

        public async Task InitializeServiceAccountAsync(string Path)
        {
            await _ba.CreateAccountAsync(Path, SERVICE_ACCOUNT_NAME, AccountTypes.Service);
            //_blocks.EnsureIndex(x => x.AccountID);
            //_blocks.EnsureIndex(x => x.Index);

            ServiceBlock firstServiceBlock = new ServiceBlock()
            {
                Authorizers = new List<NodeInfo>(),                //new Dictionary<short, NodeInfo>(),
                TransferFee = 1,  // 1 LYR
                TokenGenerationFee = 100, // 100 LYR
                TradeFee = 0.1m, // 0.1 LYR
                IsPrimaryShard = true,
                AcceptedShards = new List<string> { "Primary" },
            };

            firstServiceBlock.Authorizers.Add(new NodeInfo() { PublicKey = _ba.AccountId, IPAddress = "127.0.0.1" });
            firstServiceBlock.InitializeBlock(_signr, null, _ba.PrivateKey, _config.Lyra.NetworkId);

            await firstServiceBlock.SignAsync(_signr, _ba.PrivateKey);

            //firstServiceBlock.Signature = Signatures.GetSignature(PrivateKey, firstServiceBlock.Hash);
            _ba.AddBlock(firstServiceBlock);
        }

        private async Task StartAsync(bool ModeConsensus, string Path)
        {            
            if (!_ba.AccountExistsLocally(Path, SERVICE_ACCOUNT_NAME))
                await InitializeServiceAccountAsync(Path);
            else
                _ba.OpenAccount(Path, SERVICE_ACCOUNT_NAME);
            DatabasePath = Path;

            // begin sync node

            if(!ModeConsensus)
            {
                timer = new Timer(async _ =>
                {
                    await TimingSyncAsync();
                },
                null, 10 * 1000, 10 * 60 * 1000);
            }
        }

        public async Task TimingSyncAsync()
        {
            try
            {
                Block latestBlock = _ba.GetLatestBlock();
                if (latestBlock == null)
                    throw new Exception("Last service chain block not found!");

                ServiceBlock latestServiceBlock;
                if (latestBlock.BlockType != BlockTypes.Service)
                {
                    latestServiceBlock = GetLastServiceBlock();
                    if (latestServiceBlock == null)
                        throw new Exception("Latest service block not found!");
                }
                else
                    latestServiceBlock = latestBlock as ServiceBlock;

                SyncBlock sync = new SyncBlock();
                sync.LastServiceBlockHash = latestServiceBlock.Hash;
                sync.InitializeBlock(_signr, latestBlock, _ba.PrivateKey, _ba.NetworkId);
                await sync.SignAsync(_signr, _ba.PrivateKey);

                //sync.Signature = Signatures.GetSignature(PrivateKey, sync.Hash);
                _ba.AddBlock(sync);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in StartSingleNodeTestnet timer procedure: " + e.Message);
            }
        }

        public Block GetLatestBlock() => _ba.GetLatestBlock();
        public string AccountId => _ba.AccountId;
        public string PrivateKey => _ba.PrivateKey;
        public string NetworkId => _ba.NetworkId;
    }

}

