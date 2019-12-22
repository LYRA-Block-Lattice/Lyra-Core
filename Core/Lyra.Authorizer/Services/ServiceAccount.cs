using System;
using System.Collections.Generic;
using System.Threading;
using Lyra.Authorizer.Decentralize;
using Lyra.Core.Accounts;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Service;
using Microsoft.Extensions.Options;

//using Lyra.Core.Cryptography;

namespace Lyra.Authorizer.Services
{
    public class ServiceAccount : BaseAccount
    {
        public const string SERVICE_ACCOUNT_NAME = "service_account";
        public string DatabasePath { get; set; }

        Timer timer = null;
        private LyraConfig _config;

        //public Dictionary<string, string> TokenGenesisBlocks { get; set; }

        public ServiceAccount(IAccountDatabase storage, IOptions<LyraConfig> config) 
            : base(SERVICE_ACCOUNT_NAME, storage, config.Value.NetworkId)
        {
            _config = config.Value;
            Start(true, null);
        }

        /// <summary>
        /// Delete service account database storage (for unit testing only)
        /// </summary>
        /// <param name="DatabaseName">
        /// Full name including path and file name
        /// </param>
        public void Delete(string DatabaseName)
        {
            _storage.Delete(DatabaseName);
        }

        public ServiceBlock GetLastServiceBlock()
        {
            //var lstServiceBlock = base._storage. _blocks.FindOne(Query.And(Query.EQ("AccountID", AccountId), Query.EQ("SourceHash", sendBlock.Hash)));
            Block lastBlock = GetLatestBlock();
            if (lastBlock.BlockType == BlockTypes.Service)
                return lastBlock as ServiceBlock;
            if (lastBlock == null)
                return null;
            string hash = (lastBlock as SyncBlock).LastServiceBlockHash;
            ServiceBlock lastServiceBlock = _storage.FindBlockByHash(hash) as ServiceBlock;
            return lastServiceBlock;
        }

        public void InitializeServiceAccount(string Path)
        {
            CreateAccount(Path, SERVICE_ACCOUNT_NAME, AccountTypes.Service);
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

            firstServiceBlock.Authorizers.Add(new NodeInfo() { PublicKey = AccountId, IPAddress = "127.0.0.1" });
            firstServiceBlock.InitializeBlock(null, PrivateKey, _config.NetworkId);

            firstServiceBlock.Sign(PrivateKey);

            //firstServiceBlock.Signature = Signatures.GetSignature(PrivateKey, firstServiceBlock.Hash);
            AddBlock(firstServiceBlock);
        }

        private void Start(bool ModeConsensus, string Path)
        {            
            if (!AccountExistsLocally(Path, SERVICE_ACCOUNT_NAME))
                InitializeServiceAccount(Path);
            else
                OpenAccount(Path, SERVICE_ACCOUNT_NAME);
            DatabasePath = Path;

            if(!ModeConsensus)
            {
                timer = new Timer(_ =>
                {
                    TimingSync();
                },
                null, 10 * 1000, 10 * 60 * 1000);
            }
        }

        public void TimingSync()
        {
            try
            {
                Block latestBlock = GetLatestBlock();
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
                sync.InitializeBlock(latestBlock, PrivateKey, NetworkId);
                sync.Sign(PrivateKey);

                //sync.Signature = Signatures.GetSignature(PrivateKey, sync.Hash);
                AddBlock(sync);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in StartSingleNodeTestnet timer procedure: " + e.Message);
            }
        }

    }

}

